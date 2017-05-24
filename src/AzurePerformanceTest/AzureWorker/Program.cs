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
using System.Diagnostics;
using AzurePerformanceTest;

namespace AzureWorker
{
    class Program
    {
        const string defaultContainerUri = "default";

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
                case "--collect-results":
                    CollectResults(subArgs).Wait();
                    return 0;
                default:
                    Console.WriteLine("Incorrect first parameter.");
                    return 1;
            }
        }

        static async Task CollectResults(string[] subArgs)
        {
            throw new NotImplementedException();
        }

        static async Task AddTasks(string[] args)
        {
            int experimentId = int.Parse(args[0]);
            string benchmarkContainer = args[1];
            string benchmarksPath = args[2];
            string executable = args[3];
            string arguments = args[4];
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
            Trace.WriteLine(String.Format("Params are:\n id: {0}\ncontainer: {8}\npath: {1}\nexec: {2}\nargs: {3}\ntimeout: {4}\nmemlimit: {5}\noutlimit: {6}\nerrlimit: {7}", experimentId, benchmarksPath, executable, arguments, timeout, memoryLimit, outputLimit, errorLimit, benchmarkContainer));

            string jobId = "exp" + experimentId.ToString();

            var batchCred = new BatchSharedKeyCredentials(Settings.Default.BatchAccountUrl, Settings.Default.BatchAccountName, Settings.Default.BatchAccountKey);

            var storage = new AzureExperimentStorage(Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);
            AzureBenchmarkStorage benchmarkStorage;
            if (benchmarkContainer == defaultContainerUri)
                benchmarkStorage = new AzureBenchmarkStorage(Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey, AzureBenchmarkStorage.DefaultContainerName);
            else
                benchmarkStorage = new AzureBenchmarkStorage(benchmarkContainer);


            string storageConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);
            
            var execResourceFile = new ResourceFile(storage.GetExecutableSasUri(executable), executable);
            Trace.WriteLine("Resourced executable");

            using (BatchClient batchClient = BatchClient.Open(batchCred))
            {
                BlobContinuationToken continuationToken = null;
                BlobResultSegment resultSegment = null;
                
                List<Task> starterTasks = new List<Task>();
                int batchNo = 0;
                do
                {
                    resultSegment = await benchmarkStorage.ListBlobsSegmentedAsync(benchmarksPath, continuationToken);
                    Trace.WriteLine("Got some blobs");
                    starterTasks.Add(StartTasksForSegment(execResourceFile, timeout.TotalSeconds.ToString(), experimentId, executable, arguments, memoryLimit, outputLimit, errorLimit, jobId, batchClient, resultSegment.Results, batchNo, benchmarkStorage));
                    
                    continuationToken = resultSegment.ContinuationToken;
                    ++batchNo;
                }
                while (continuationToken != null);

                await Task.WhenAll(starterTasks.ToArray());
            }
        }

        private static async Task StartTasksForSegment(ResourceFile execResourceFile, string timeout, int experimentId, string executable, string arguments, long? memoryLimit, long? outputLimit, long? errorLimit, string jobId, BatchClient batchClient, IEnumerable<IListBlobItem> segmentResults, int idPart, AzureBenchmarkStorage benchmarkStorage)
        {
            List<CloudTask> tasks = new List<CloudTask>();
            int blobNo = 0;
            foreach (CloudBlockBlob blobItem in segmentResults)
            {
                string taskId = idPart.ToString() + "_" + blobNo.ToString();
                string[] parts = blobItem.Name.Split('/');
                string shortName = parts[parts.Length - 1];
                string taskCommandLine = String.Format("cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\AzureWorker.exe --measure {0} \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\" \"{8}\"", experimentId, blobItem.Name, executable, arguments, shortName, timeout, NullableLongToString(memoryLimit), NullableLongToString(outputLimit), NullableLongToString(errorLimit));
                var resourceFile = new ResourceFile(benchmarkStorage.GetBlobSASUri(blobItem), shortName);
                CloudTask task = new CloudTask(taskId, taskCommandLine);
                task.ResourceFiles = new List<ResourceFile> { resourceFile, execResourceFile };
                tasks.Add(task);
                ++blobNo;
            }
            Trace.WriteLine("Starting tasks...");
            await batchClient.JobOperations.AddTaskAsync(jobId, tasks);
            Trace.WriteLine("Started some tasks");
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
