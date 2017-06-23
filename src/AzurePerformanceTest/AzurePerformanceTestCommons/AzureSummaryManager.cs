using Angara.Data;
using Ionic.Zip;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using PerformanceTest;
using PerformanceTest.Records;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzurePerformanceTest
{
    public class AzureSummaryManager
    {
        private readonly CloudStorageAccount storageAccount;
        private readonly CloudBlobContainer summaryContainer;
        private readonly CloudBlobClient blobClient;
        private readonly AzureExperimentStorage storage;

        private readonly IDomainResolver resolveDomain;
        private readonly IRetryPolicy retryPolicy = new ExponentialRetry(TimeSpan.FromMilliseconds(250), 7);


        private const string summaryContainerName = "summary";


        public AzureSummaryManager(string storageConnectionString)
        {
            var cs = new StorageAccountConnectionString(storageConnectionString).ToString();
            storage = new AzureExperimentStorage(cs);

            storageAccount = CloudStorageAccount.Parse(cs);
            blobClient = storageAccount.CreateCloudBlobClient();
            summaryContainer = blobClient.GetContainerReference(summaryContainerName);

            var cloudEntityCreationTasks = new Task[] {
                summaryContainer.CreateIfNotExistsAsync()
            };

            resolveDomain = MEFDomainResolver.Instance;

            Task.WaitAll(cloudEntityCreationTasks);
        }


        public async Task<Tuple<ExperimentSummary[], RecordsTable>> GetSummariesAndRecords(string summaryName)
        {
            var results = await DownloadSummary(summaryName);
            var records = results.Item2;
            var summaries = ExperimentSummaryStorage.LoadFromTable(results.Item1);

            return Tuple.Create(summaries, records);
        }

        public async Task Update(string summaryName, int experimentId)
        {
            Trace.WriteLine("Downloading experiment results...");
            var all_summaries = await DownloadSummary(summaryName);

            var exp = await storage.GetExperiment(experimentId); // fails if not found
            var domain = resolveDomain.GetDomain(exp.DomainName);

            var results = await storage.GetResults(experimentId);

            Trace.WriteLine("Building summary for the experiment...");
            var catSummary = ExperimentSummary.Build(results, domain);
            var expSummary = new ExperimentSummary(experimentId, DateTimeOffset.Now, catSummary);
            var sumTable = ExperimentSummaryStorage.AppendOrReplace(all_summaries.Item1, expSummary);

            Trace.WriteLine("Updating records...");
            var records = all_summaries.Item2;
            records.Update(results, domain);

            await UploadSummary(summaryName, sumTable, records, all_summaries.Item3);
        }

        private async Task<Tuple<Table, RecordsTable, string>> DownloadSummary(string summaryName)
        {
            var blobName = string.Format("{0}.zip", AzureUtils.ToBinaryPackBlobName(summaryName));
            var blob = summaryContainer.GetBlockBlobReference(blobName);

            Table summary;
            Dictionary<string, Record> records;
            Dictionary<string, CategoryRecord> records_summary;
            string etag = null;

            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    await blob.DownloadToStreamAsync(ms);
                    etag = blob.Properties.ETag;
                    ms.Position = 0;

                    using (ZipFile zip = ZipFile.Read(ms))
                    {
                        var zip_summary = zip["summary.csv"];
                        var zip_records = zip["records.csv"];
                        var zip_records_summary = zip["records_summary.csv"];

                        using (MemoryStream mem = new MemoryStream((int)zip_summary.UncompressedSize))
                        {
                            zip_summary.Extract(mem);
                            mem.Position = 0;
                            summary = ExperimentSummaryStorage.LoadTable(mem);
                        }
                        using (MemoryStream mem = new MemoryStream((int)zip_records.UncompressedSize))
                        {
                            zip_records.Extract(mem);
                            mem.Position = 0;
                            records = RecordsStorage.LoadBenchmarksRecords(mem);
                        }
                        using (MemoryStream mem = new MemoryStream((int)zip_records_summary.UncompressedSize))
                        {
                            zip_records_summary.Extract(mem);
                            mem.Position = 0;
                            records_summary = RecordsStorage.LoadSummaryRecords(mem);
                        }
                    }
                }
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == (int)System.Net.HttpStatusCode.NotFound)
            {
                summary = Table.Empty;
                records = new Dictionary<string, Record>();
                records_summary = new Dictionary<string, CategoryRecord>();
                etag = null;
            }

            return Tuple.Create(summary, new RecordsTable(records, records_summary), etag);
        }

        private async Task UploadSummary(string summaryName, Table summary, RecordsTable records, string etag)
        {
            using (Stream zipStream = new MemoryStream())
            {
                using (ZipFile zip = new ZipFile())
                using (Stream s1 = new MemoryStream())
                using (Stream s2 = new MemoryStream())
                using (Stream s3 = new MemoryStream())
                {
                    ExperimentSummaryStorage.SaveTable(summary, s1);
                    s1.Position = 0;
                    zip.AddEntry("summary.csv", s1);

                    RecordsStorage.SaveBenchmarksRecords(records.BenchmarkRecords, s2);
                    s2.Position = 0;
                    zip.AddEntry("records.csv", s2);

                    RecordsStorage.SaveSummaryRecords(records.CategoryRecords, s3);
                    s3.Position = 0;
                    zip.AddEntry("records_summary.csv", s3);

                    zip.Save(zipStream);
                }

                zipStream.Position = 0;

                var blobName = string.Format("{0}.zip", AzureUtils.ToBinaryPackBlobName(summaryName));
                var blob = summaryContainer.GetBlockBlobReference(blobName);

                await blob.UploadFromStreamAsync(zipStream, etag == null ? AccessCondition.GenerateIfNotExistsCondition() : AccessCondition.GenerateIfMatchCondition(etag),
                    new BlobRequestOptions { RetryPolicy = retryPolicy }, null);
            }
        }
    }
}
