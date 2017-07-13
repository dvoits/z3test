using Angara.Data;
using Ionic.Zip;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using PerformanceTest;
using PerformanceTest.Alerts;
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
        private const string fileNameTimeline = "timeline.csv";
        private const string fileNameRecords = "records.csv";
        private const string fileNameRecordsSummary = "records_summary.csv";

        public AzureSummaryManager(string storageConnectionString, IDomainResolver domainResolver)
        {
            if (domainResolver == null) throw new ArgumentNullException(nameof(domainResolver));

            var cs = new StorageAccountConnectionString(storageConnectionString).ToString();
            storage = new AzureExperimentStorage(cs);

            storageAccount = CloudStorageAccount.Parse(cs);
            blobClient = storageAccount.CreateCloudBlobClient();
            summaryContainer = blobClient.GetContainerReference(summaryContainerName);

            var cloudEntityCreationTasks = new Task[] {
                summaryContainer.CreateIfNotExistsAsync()
            };

            resolveDomain = domainResolver;

            Task.WaitAll(cloudEntityCreationTasks);
        }


        public async Task<Tuple<ExperimentSummary[], RecordsTable>> GetTimelineAndRecords(string timelineName)
        {
            var results = await DownloadSummary(timelineName);
            var records = results.Item2;
            var summaries = ExperimentSummaryStorage.LoadFromTable(results.Item1);

            return Tuple.Create(summaries, records);
        }

        public async Task<ExperimentSummary[]> GetTimeline(string timelineName)
        {
            var results = await DownloadSummary(timelineName, true);
            var summaries = ExperimentSummaryStorage.LoadFromTable(results.Item1);
            return summaries;
        }

        public async Task<Tags> GetTags(string timelineName)
        {
            var blobName = string.Format("{0}.tags.csv", AzureUtils.ToBinaryPackBlobName(timelineName));
            var blob = summaryContainer.GetBlockBlobReference(blobName);

            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    await blob.DownloadToStreamAsync(ms, AccessCondition.GenerateEmptyCondition(), new BlobRequestOptions { RetryPolicy = retryPolicy }, null);

                    ms.Position = 0;
                    Tags tags = TagsStorage.Load(ms);
                    return tags;
                }
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == (int)System.Net.HttpStatusCode.NotFound)
            {
                return new Tags();
            }
        }

        public async Task<ExperimentSummary[]> Update(string timelineName, params int[] experiments)
        {
            if (experiments == null) throw new ArgumentNullException(nameof(experiments));

            Trace.WriteLine("Downloading experiment results...");
            var all_summaries = await DownloadSummary(timelineName);

            Table timeline = all_summaries.Item1;
            RecordsTable records = all_summaries.Item2;
            string etag = all_summaries.Item3;

            foreach (var experimentId in experiments)
            {
                var exp = await storage.GetExperiment(experimentId); // fails if not found
                var domain = resolveDomain.GetDomain(exp.DomainName);

                var results = (await storage.GetResults(experimentId)).Benchmarks;

                Trace.WriteLine("Building summary for the experiment " + experimentId);
                var catSummary = ExperimentSummary.Build(results, domain, ExperimentSummary.DuplicateResolution.Ignore);
                var expSummary = new ExperimentSummary(experimentId, DateTimeOffset.Now, catSummary);
                timeline = ExperimentSummaryStorage.AppendOrReplace(timeline, expSummary);

                Trace.WriteLine("Updating records...");
                records.UpdateWith(results, domain);
            }

            await UploadSummary(timelineName, timeline, records, all_summaries.Item3);
            var resultfromTable = ExperimentSummaryStorage.LoadFromTable(timeline);
            Array.Sort(resultfromTable, (el1, el2) => DateTimeOffset.Compare(el2.Date, el1.Date));
            return resultfromTable;
        }

        /// <summary>
        /// Returns summary by statuses for the given experiment.
        /// If summary for the given parameters has not been computed yet,
        /// builds the summary and saves it.
        /// </summary>
        /// <param name="expId">Target experiment.</param>
        /// <param name="refExpId">Optional; another experiment to compare performance with.</param>
        public async Task<ExperimentStatusSummary> GetStatusSummary(int expId, int? refExpId)
        {
            Trace.WriteLine("Check if the summary already exists...");
            ExperimentStatusSummary summary = await TryDownloadStatusSummary(expId, refExpId);
            if (summary != null)
            {
                Trace.WriteLine("Ok, summary found.");
                return summary;
            }

            Trace.WriteLine("Downloading experiment information...");
            var exp = await storage.GetExperiment(expId); // fails if not found
            var domain = resolveDomain.GetDomain(exp.DomainName);

            Trace.WriteLine("Downloading experiment results...");
            BenchmarkResult[] results = (await storage.GetResults(expId)).Benchmarks;

            BenchmarkResult[] refResults = null;
            if (refExpId.HasValue)
            {
                Trace.WriteLine("Downloading another experiment results...");
                refResults = (await storage.GetResults(refExpId.Value)).Benchmarks;
            }

            Trace.WriteLine("Building summary...");
            summary = ExperimentStatusSummary.Build(expId, results, refExpId, refResults, domain);

            Trace.WriteLine("Uploading summary...");
            await UploadStatusSummary(summary);
            return summary;
        }

        public async Task SendReport(ExperimentSummary expSummary, ExperimentSummary refSummary, List<string> recipients, string linkPage)
        {
            var expId = expSummary.Id;
            var refId = refSummary.Id;
            var submissionTime = expSummary.Date;
            var statusSummary = await GetStatusSummary(expId, refId);

            var alerts = new ExperimentAlerts(expSummary, statusSummary, linkPage);


            if (alerts != null && alerts[""].Count > 0 && alerts[""].Level != AlertLevel.None)
            {
                //generate new html report 
                string new_report = "<body>";
                new_report += "<h1>Z3 Nightly Alert Report</h1>";
                new_report += "<p>This are alerts for <a href=" + linkPage + "?job=" + expId + " style='text-decoration:none'>job #" + expId + "</a> (submitted " + submissionTime + ").</p>";

                if (alerts[""].Count == 0)
                {
                    new_report += "<p>";
                    new_report += "<img src='cid:ok'/> ";
                    new_report += "<font color=Green>All is well everywhere!</font>";
                    new_report += "</p>";
                }
                else
                {
                    new_report += "<h2>Alert Summary</h2>";
                    new_report += createReportTable("", alerts);

                    //detailed report
                    new_report += "<h2>Detailed alerts</h2>";
                    foreach (string cat in alerts.Categories)
                    {
                        if (cat != "" && alerts[cat].Count > 0)
                        {
                            new_report += "<h3><a href='" + linkPage + "?job=" + expId + "&cat=" + cat + "' style='text-decoration:none'>" + cat + "</a></h3>";
                            new_report += createReportTable(cat, alerts);
                        }
                    }
                    new_report += "<p>For more information please see the <a href='" + linkPage + "' style='text-decoration:none'>Z3 Nightly Webpage</a>.</p>";
                }
                new_report += "</body>";

                //send emails
                Dictionary<string, string> images = new Dictionary<string, string>();
                images.Add("ok", "ok.png");
                images.Add("warning", "warning.png");
                images.Add("critical", "critical.png");
                foreach (string recipient in recipients)
                {
                    SendMail.Send(recipient, "Z3 Alerts", new_report, null, images, true);
                }
            }
        }
        private string createReportTable(string category, ExperimentAlerts alerts)
        {
            AlertSet alertSet = alerts[category];
            string new_table = "";
            //add table 
            if (alertSet.Count > 0)
            {
                new_table += "<table>";

                foreach (KeyValuePair<AlertLevel, List<string>> kvp in alertSet.Messages)
                {
                    AlertLevel level = kvp.Key;
                    List<string> messages = kvp.Value;

                    new_table += "<tr>";
                    switch (level)
                    {
                        case AlertLevel.None:
                            new_table += "<td align=left valign=top><img src='cid:ok'/></td>";
                            new_table += "<td align=left valign=middle><font color=Green>";
                            break;
                        case AlertLevel.Warning:
                            new_table += "<td align=left valign=top><img src='cid:warning'/></td>";
                            new_table += "<td align=left valign=middle><font color=Orange>";
                            break;
                        case AlertLevel.Critical:
                            new_table += "<td align=left valign=top><img src='cid:critical'/></td>";
                            new_table += "<td align=left valign=middle><font color=Red>";
                            break;
                    }
                    foreach (string m in messages)
                        new_table += m + "<br/>";
                    new_table += "</font></td>";

                    new_table += "<tr>";
                }

                new_table += "</table>";
            }
            return new_table;
        }

        private async Task UploadStatusSummary(ExperimentStatusSummary summary)
        {
            string fileName = GetStatusSummaryFileName(summary.Id, summary.ReferenceId);
            string blobName = GetStatusSummaryBlobName(fileName);
            var blob = summaryContainer.GetBlockBlobReference(blobName);

            using (Stream zipStream = new MemoryStream())
            {
                using (ZipFile zip = new ZipFile())
                using (MemoryStream mem = new MemoryStream())
                {
                    ExperimentStatusSummaryStorage.Save(summary, mem);

                    mem.Position = 0;
                    zip.AddEntry(fileName, mem);
                    zip.Save(zipStream);
                }
                zipStream.Position = 0;
                await blob.UploadFromStreamAsync(zipStream, AccessCondition.GenerateEmptyCondition(), new BlobRequestOptions { RetryPolicy = retryPolicy }, null);
            }
        }

        private static string GetStatusSummaryBlobName(string fileName)
        {
            return string.Concat("_", fileName, ".zip");
        }

        private static string GetStatusSummaryFileName(int expId, int? refExpId)
        {
            return refExpId.HasValue ?
                string.Format("statuses_{0}_{1}.csv", expId, refExpId.Value) :
                string.Format("statuses_{0}.csv", expId);
        }

        private async Task<ExperimentStatusSummary> TryDownloadStatusSummary(int expId, int? refExpId)
        {
            string fileName = GetStatusSummaryFileName(expId, refExpId);
            var blobName = GetStatusSummaryBlobName(fileName);
            var blob = summaryContainer.GetBlockBlobReference(blobName);

            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    await blob.DownloadToStreamAsync(ms);
                    ms.Position = 0;

                    using (ZipFile zip = ZipFile.Read(ms))
                    {
                        var zip_summary = zip[fileName];

                        using (MemoryStream mem = new MemoryStream((int)zip_summary.UncompressedSize))
                        {
                            zip_summary.Extract(mem);
                            mem.Position = 0;
                            return ExperimentStatusSummaryStorage.Load(expId, refExpId, mem);
                        }
                    }
                }
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == (int)System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        private async Task<Tuple<Table, RecordsTable, string>> DownloadSummary(string timelineName, bool onlySummary = false)
        {
            var blobName = string.Format("{0}.zip", AzureUtils.ToBinaryPackBlobName(timelineName));
            var blob = summaryContainer.GetBlockBlobReference(blobName);

            Table summary;
            Dictionary<string, Record> records = null;
            Dictionary<string, CategoryRecord> records_summary = null;
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
                        var zip_summary = zip[fileNameTimeline];
                        using (MemoryStream mem = new MemoryStream((int)zip_summary.UncompressedSize))
                        {
                            zip_summary.Extract(mem);
                            mem.Position = 0;
                            summary = ExperimentSummaryStorage.LoadTable(mem);
                        }

                        if (!onlySummary)
                        {
                            var zip_records = zip[fileNameRecords];
                            using (MemoryStream mem = new MemoryStream((int)zip_records.UncompressedSize))
                            {
                                zip_records.Extract(mem);
                                mem.Position = 0;
                                records = RecordsStorage.LoadBenchmarksRecords(mem);
                            }

                            var zip_records_summary = zip[fileNameRecordsSummary];
                            using (MemoryStream mem = new MemoryStream((int)zip_records_summary.UncompressedSize))
                            {
                                zip_records_summary.Extract(mem);
                                mem.Position = 0;
                                records_summary = RecordsStorage.LoadSummaryRecords(mem);
                            }
                        }
                    }
                }
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == (int)System.Net.HttpStatusCode.NotFound)
            {
                summary = ExperimentSummaryStorage.EmptyTable();
                records = new Dictionary<string, Record>();
                records_summary = new Dictionary<string, CategoryRecord>();
                etag = null;
            }

            return Tuple.Create(summary, new RecordsTable(records, records_summary), etag);
        }

        private async Task UploadSummary(string timelineName, Table summary, RecordsTable records, string etag)
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
                    zip.AddEntry(fileNameTimeline, s1);

                    RecordsStorage.SaveBenchmarksRecords(records.BenchmarkRecords, s2);
                    s2.Position = 0;
                    zip.AddEntry(fileNameRecords, s2);

                    RecordsStorage.SaveSummaryRecords(records.CategoryRecords, s3);
                    s3.Position = 0;
                    zip.AddEntry(fileNameRecordsSummary, s3);

                    zip.Save(zipStream);
                }

                zipStream.Position = 0;

                var blobName = string.Format("{0}.zip", AzureUtils.ToBinaryPackBlobName(timelineName));
                var blob = summaryContainer.GetBlockBlobReference(blobName);

                await blob.UploadFromStreamAsync(zipStream, etag == null ? AccessCondition.GenerateIfNotExistsCondition() : AccessCondition.GenerateIfMatchCondition(etag),
                    new BlobRequestOptions { RetryPolicy = retryPolicy }, null);
            }
        }


    }
}
