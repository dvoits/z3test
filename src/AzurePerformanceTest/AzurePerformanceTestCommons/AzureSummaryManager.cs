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
                using(MemoryStream ms = new MemoryStream())
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

        public async Task<Tuple<int[], DateTimeOffset>> Update(string timelineName, int experimentId)
        {
            Trace.WriteLine("Downloading experiment results...");
            var all_summaries = await DownloadSummary(timelineName);

            var exp = await storage.GetExperiment(experimentId); // fails if not found
            var domain = resolveDomain.GetDomain(exp.DomainName);

            var results = (await storage.GetResults(experimentId)).Benchmarks;

            Trace.WriteLine("Building summary for the experiment...");
            var catSummary = ExperimentSummary.Build(results, domain, ExperimentSummary.DuplicateResolution.Ignore);
            var expSummary = new ExperimentSummary(experimentId, DateTimeOffset.Now, catSummary);
            var sumTable = ExperimentSummaryStorage.AppendOrReplace(all_summaries.Item1, expSummary);

            Trace.WriteLine("Updating records...");
            var records = all_summaries.Item2;
            records.Update(results, domain);
            var table = all_summaries.Item1;
            
            await UploadSummary(timelineName, sumTable, records, all_summaries.Item3);
            var resultfromTable = ExperimentSummaryStorage.LoadFromTable(table);
            Array.Sort(resultfromTable, (el1, el2) => el1.Date < el2.Date ? 1 : 0);
            return Tuple.Create<int[], DateTimeOffset>(resultfromTable.Select(item => item.Id).ToArray(), resultfromTable[0].Date);
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

        public async Task SendReport(int expId, int refId, DateTime submissionTime, List<string> recipients, string linkPage)
        {
            var summary = await GetStatusSummary(expId, refId); 
            if (summary != null && (summary.BugsByCategory.Count > 0 || summary.ErrorsByCategory.Count > 0))
            {
                //generate new html report 
                string new_report = "<body>";
                new_report += "<h1>Z3 Nightly Alert Report</h1>";
                new_report += "<p>This are alerts for <a href=" + linkPage + "?job=" + expId + " style='text-decoration:none'>job #" + expId + "</a> (submitted " + submissionTime + ").</p>";

                //use ExperimentAlerts

                List<string> bugs;
                List<string> errors;
                List<string> dippers;
                bool hasBugs = summary.BugsByCategory.TryGetValue("", out bugs);
                bool hasErrors = summary.ErrorsByCategory.TryGetValue("", out errors);
                bool hasDippers = summary.DippersByCategory.TryGetValue("", out dippers);
                if (!hasBugs && !hasErrors && !hasDippers)
                {
                    new_report += "<p>";
                    new_report += "<img src='cid:ok'/> ";
                    new_report += "<font color=Green>All is well everywhere!</font>";
                    new_report += "</p>";
                }
                else
                {
                    new_report += "<h2>Alert Summary</h2>";
                    new_report += createReportTable("", summary);

                    //detailed report
                    new_report += "<h2>Detailed alerts</h2>";
                    string[] categories = summary.BugsByCategory.Keys.Concat(summary.ErrorsByCategory.Keys).Concat(summary.DippersByCategory.Keys).Distinct().ToArray();
                    foreach (string cat in categories)
                    {
                        if (cat != "")
                        {
                            new_report += "<h3><a href='" + linkPage + "?job=" + expId + "&cat=" + cat + "' style='text-decoration:none'>" + cat + "</a></h3>";
                            new_report += createReportTable(cat, summary);
                        }
                    }
                    new_report += "<p>For more information please see the <a href='" + linkPage + "' style='text-decoration:none'>Z3 Nightly Webpage</a>.</p>";
                }
                new_report += "</body>";
                //send emails
                Dictionary<string, string> images = new Dictionary<string, string>();
                images.Add("ok", "Images/ok.png");
                images.Add("warning", "Images/warning.png");
                images.Add("critical", "Images/critical.png");
                foreach (string recipient in recipients)
                {
                    SendMail.Send(recipient, "Z3 Alerts", new_report, null, images, true);
                }
            }
        }
        private string createReportTable (string category, ExperimentStatusSummary summary)
        {
            List<string> bugs;
            List<string> errors;
            List<string> dippers;
            bool hasBugs = summary.BugsByCategory.TryGetValue(category, out bugs);
            bool hasErrors = summary.ErrorsByCategory.TryGetValue(category, out errors);
            bool hasDippers = summary.DippersByCategory.TryGetValue(category, out dippers);

            //add table 
            string new_table = "<table>";
            new_table += "<tr>";
            if (hasBugs && bugs.Count > 0)
            {
                new_table += "<td align=left valign=top><img src='cid:critical'/></td>";
                new_table += "<td align=left valign=middle><font color=Red>";
                new_table += string.Format("There {0} {1} bug{2} in ", bugs.Count == 1 ? "is" : "are", bugs.Count, bugs.Count == 1 ? "" : "s");
                new_table += "<br/>";
                foreach (var message in bugs)
                {
                    new_table += message + "<br/>";
                }
                new_table += "</font></td>";
                new_table += "<tr>";
            }
            if (hasErrors && errors.Count > 0)
            {
                new_table += "<td align=left valign=top><img src='cid:warning'/></td>";
                new_table += "<td align=left valign=middle><font color=Orange>";
                new_table += string.Format("There {0} {1} error{2} in ", errors.Count == 1 ? "is" : "are", errors.Count, errors.Count == 1 ? "" : "s");
                new_table += "<br/>";
                foreach (var message in errors)
                {
                    new_table += message + "<br/>";
                }
                new_table += "</font></td>";
                new_table += "<tr>";
            }
            if (hasDippers && dippers.Count > 0)
            {
                new_table += "<td align=left valign=top><img src='cid:ok'/></td>";
                new_table += "<td align=left valign=middle><font color=Green>";
                new_table += string.Format("There {0} {1} benchmark{2} that show{3} a dip in performance: ",
                        dippers.Count == 1 ? "is" : "are", dippers.Count,
                        dippers.Count == 1 ? "" : "s",
                        dippers.Count == 1 ? "s" : "");
                new_table += "<br/>";
                foreach (var message in dippers)
                {
                    new_table += message + "<br/>";
                }
                new_table += "</font></td>";
                new_table += "<tr>";
            }
            new_table += "</table>";
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
