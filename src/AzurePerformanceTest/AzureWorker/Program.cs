using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Measurement;
using System.IO;
using PerformanceTest;
using AzureWorker.Properties;
using Microsoft.Azure.Batch.Auth;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Azure.Batch;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AzureWorker
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                //TODO: Proper help
                Console.WriteLine("Not enough arguments.");
                return 1;
            }

            var subArgs = args.Skip(1).ToArray();
            switch (args[0])
            {
                case "--add-tasks":
                    AddTasks(subArgs).Wait();
                    return 0;
                case "--measure":
                    Measure(subArgs).Wait();
                    return 0;
                case "--reference-run":
                    RunReference(subArgs).Wait();
                    return 0;
                default:
                    Console.WriteLine("Incorrect first parameter.");
                    return 1;
            }
        }

        static async Task AddTasks(string[] args)
        {
            int experimentId = int.Parse(args[0]);
            string benchmarksPath = args[1];
            string executable = Path.GetFullPath(args[2]);
            string arguments = args[3];
            TimeSpan timeout = TimeSpan.FromSeconds(double.Parse(args[4]));
            long? memoryLimit = null;
            long? outputLimit = null;
            long? errorLimit = null;
            if (args.Length > 5)
            {
                memoryLimit = args[5] == "null" ? null : (long?)long.Parse(args[5]);
                if (args.Length > 6)
                {
                    outputLimit = args[6] == "null" ? null : (long?)long.Parse(args[6]);
                    if (args.Length > 7)
                    {
                        errorLimit = args[7] == "null" ? null : (long?)long.Parse(args[7]);
                    }
                }
            }

            string jobId = "exp" + experimentId.ToString();

            var batchCred = new BatchSharedKeyCredentials(Settings.Default.BatchAccountUrl, Settings.Default.BatchAccountName, Settings.Default.BatchAccountKey);
            string storageConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer inputsContainer = blobClient.GetContainerReference("input");

            using (BatchClient batchClient = BatchClient.Open(batchCred))
            {
                BlobContinuationToken continuationToken = null;
                BlobResultSegment resultSegment = null;

                //Call ListBlobsSegmentedAsync and enumerate the result segment returned, while the continuation token is non-null.
                //When the continuation token is null, the last page has been returned and execution can exit the loop.
                List<Task> starterTasks = new List<Task>();
                do
                {
                    //This overload allows control of the page size. You can return all remaining results by passing null for the maxResults parameter,
                    //or by calling a different overload.
                    resultSegment = await inputsContainer.ListBlobsSegmentedAsync(benchmarksPath, true, BlobListingDetails.All, null, continuationToken, null, null);
                    starterTasks.Add(StartTasksForSegment(args[4], experimentId, executable, arguments, memoryLimit, outputLimit, errorLimit, jobId, batchClient, resultSegment.Results));

                    //Get the continuation token.
                    continuationToken = resultSegment.ContinuationToken;
                }
                while (continuationToken != null);

                Task.WaitAll(starterTasks.ToArray());
            }
        }

        private static async Task StartTasksForSegment(string timeout, int experimentId, string executable, string arguments, long? memoryLimit, long? outputLimit, long? errorLimit, string jobId, BatchClient batchClient, IEnumerable<IListBlobItem> segmentResults)
        {
            List<CloudTask> tasks = new List<CloudTask>();
            foreach (CloudBlockBlob blobItem in segmentResults)
            {
                string taskId = blobItem.Name;
                string[] parts = blobItem.Name.Split('/');
                string shortName = parts[parts.Length - 1];
                string taskCommandLine = String.Format("cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\AzureWorker.exe --measure {0} \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\" \"{8}\"", experimentId, blobItem.Name, executable, arguments, shortName, timeout, NullableLongToString(memoryLimit), NullableLongToString(outputLimit), NullableLongToString(errorLimit));
                SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
                {
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(48),
                    Permissions = SharedAccessBlobPermissions.Read
                };
                string sasBlobToken = blobItem.GetSharedAccessSignature(sasConstraints);
                string blobSasUri = String.Format("{0}{1}", blobItem.Uri, sasBlobToken);
                var resourceFile = new ResourceFile(blobSasUri, shortName);
                CloudTask task = new CloudTask(taskId, taskCommandLine);
                task.ResourceFiles = new List<ResourceFile> { resourceFile };
                tasks.Add(task);
            }
            await batchClient.JobOperations.AddTaskAsync(jobId, tasks);
        }

        static string NullableLongToString(long? number)
        {
            return number == null ? "null" : number.Value.ToString();
        }

        static async Task Measure(string[] args)
        {
            int experimentId = int.Parse(args[0]);
            string benchmarkId = args[1];
            string executable = Path.GetFullPath(args[2]);
            string arguments = args[3];
            string targetFile = args[4];
            TimeSpan timeout = TimeSpan.FromSeconds(double.Parse(args[5]));
            long? memoryLimit = null;
            long? outputLimit = null;
            long? errorLimit = null;
            if (args.Length > 6)
            {
                memoryLimit = args[6] == "null" ? null : (long?)long.Parse(args[6]);
                if (args.Length > 7)
                {
                    outputLimit = args[7] == "null" ? null : (long?)long.Parse(args[7]);
                    if (args.Length > 8)
                    {
                        errorLimit = args[8] == "null" ? null : (long?)long.Parse(args[8]);
                    }
                }
            }
            arguments = arguments.Replace("{0}", Path.GetFullPath(targetFile));
            var acquireTime = DateTime.Now;
            var measure = ProcessMeasurer.Measure(executable, arguments, timeout, memoryLimit, outputLimit, errorLimit);
            var result = new BenchmarkResult(experimentId, benchmarkId, "", measure.TotalProcessorTime.TotalSeconds, acquireTime, measure);
        }

        static async Task RunReference(string[] args)
        {

        }
    }
}
