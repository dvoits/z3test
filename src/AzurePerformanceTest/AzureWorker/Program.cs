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
using Microsoft.WindowsAzure.Storage.Queue;
using System.Runtime.Serialization.Formatters.Binary;

namespace AzureWorker
{
    class Program
    {
        const string defaultContainerUri = "default";
        const string totalBenchmarksPrefix = "Total benchmarks: ";

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

        static async Task CollectResults(string[] args)
        {
            int experimentId = int.Parse(args[0]);

            var storage = new AzureExperimentStorage(Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);
            var queue = storage.GetResultsQueueReference(experimentId);
            List<BenchmarkResult> results = new List<BenchmarkResult>();
            int totalBenchmarks = -1;
            int processedBenchmarks = 0;
            var formatter = new BinaryFormatter();
            do
            {
                var messages = queue.GetMessages(32, TimeSpan.FromMinutes(5));
                foreach (CloudQueueMessage message in messages)
                {
                    using (var ms = new MemoryStream(message.AsBytes))
                    {
                        var typeByte = ms.ReadByte();
                        if (totalBenchmarks == -1 && typeByte == 2)
                        {
                            totalBenchmarks = (int)formatter.Deserialize(ms);
                            await storage.SetTotalBenchmarks(experimentId, totalBenchmarks);
                        }
                        else
                        {
                            results.Add((BenchmarkResult)formatter.Deserialize(ms));
                            ++processedBenchmarks;
                        }
                    }
                }
                await storage.PutExperimentResultsWithBlobnames(experimentId, results.ToArray(), true);
                await storage.SetCompletedBenchmarks(experimentId, processedBenchmarks);
                foreach (CloudQueueMessage message in messages)
                {
                    queue.DeleteMessage(message);
                }
            }
            while (totalBenchmarks == -1 || processedBenchmarks < totalBenchmarks);
            await storage.DeleteResultsQueue(experimentId);
        }

        static async Task AddTasks(string[] args)
        {
            int experimentId = int.Parse(args[0]);
            string benchmarkContainer = args[1];
            string benchmarksPath = args[2];
            string executable = args[3];
            string arguments = args[4];
            TimeSpan timeout = TimeSpan.FromSeconds(double.Parse(args[5]));
            double memoryLimit = 0; // no limit
            long? outputLimit = null;
            long? errorLimit = null;
            if (args.Length > 6)
            {
                memoryLimit = double.Parse(args[6]);
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

            var queue = await storage.CreateResultsQueue(experimentId);
            Trace.Write("Created queue");

            using (BatchClient batchClient = BatchClient.Open(batchCred))
            {
                //starting results collection
                string taskCommandLine = string.Format("cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\AzureWorker.exe --collect-results {0}", experimentId);
                CloudTask task = new CloudTask("resultCollection", taskCommandLine);
                await batchClient.JobOperations.AddTaskAsync(jobId, task);

                BlobContinuationToken continuationToken = null;
                BlobResultSegment resultSegment = null;
                
                List<Task> starterTasks = new List<Task>();
                int batchNo = 0;
                int totalBenchmarks = 0;
                do
                {
                    resultSegment = await benchmarkStorage.ListBlobsSegmentedAsync(benchmarksPath, continuationToken);
                    Trace.WriteLine("Got some blobs");
                    starterTasks.Add(StartTasksForSegment(execResourceFile, timeout.TotalSeconds.ToString(), experimentId, executable, arguments, memoryLimit, outputLimit, errorLimit, jobId, batchClient, resultSegment.Results, batchNo, benchmarkStorage));
                    
                    continuationToken = resultSegment.ContinuationToken;
                    ++batchNo;
                    totalBenchmarks += resultSegment.Results.Count();
                }
                while (continuationToken != null);

                using (var ms = new MemoryStream())
                {
                    ms.WriteByte(2);//This is number of benchmarks
                    (new BinaryFormatter()).Serialize(ms, totalBenchmarks);
                    await queue.AddMessageAsync(new CloudQueueMessage(ms.ToArray()));
                }
                    //await queue.AddMessageAsync(new CloudQueueMessage(totalBenchmarksPrefix + totalBenchmarks.ToString()));

                await Task.WhenAll(starterTasks.ToArray());
            }
        }

        private static async Task StartTasksForSegment(ResourceFile execResourceFile, string timeout, int experimentId, string executable, string arguments, double memoryLimit, long? outputLimit, long? errorLimit, string jobId, BatchClient batchClient, IEnumerable<IListBlobItem> segmentResults, int idPart, AzureBenchmarkStorage benchmarkStorage)
        {
            List<CloudTask> tasks = new List<CloudTask>();
            int blobNo = 0;
            foreach (CloudBlockBlob blobItem in segmentResults)
            {
                string taskId = idPart.ToString() + "_" + blobNo.ToString();
                string[] parts = blobItem.Name.Split('/');
                string shortName = parts[parts.Length - 1];
                string taskCommandLine = String.Format("cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\AzureWorker.exe --measure {0} \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\" \"{8}\"", experimentId, blobItem.Name, executable, arguments, shortName, timeout, memoryLimit, NullableLongToString(outputLimit), NullableLongToString(errorLimit));
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
            double memoryLimit = 0; // no limit
            long? outputLimit = null;
            long? errorLimit = null;
            if (args.Length > 6)
            {
                memoryLimit = double.Parse(args[6]);
                if (args.Length > 7)
                {
                    outputLimit = args[7] == "null" ? null : (long?)long.Parse(args[7]);
                    if (args.Length > 8)
                    {
                        errorLimit = args[8] == "null" ? null : (long?)long.Parse(args[8]);
                    }
                }
            }

            Domain domain = new Z3Domain(); // todo: take custom domain name from `args`
            BenchmarkResult result = LocalExperimentRunner.RunBenchmark(
                experimentId,
                executable,
                arguments,
                benchmarkId,
                Path.GetFullPath(targetFile),
                0,
                timeout,
                memoryLimit,
                outputLimit,
                errorLimit,
                domain,
                1.0,
                "");

            var storage = new AzureExperimentStorage(Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);
            await storage.PutResult(experimentId, result);
        }

        static async Task RunReference(string[] args)
        {
            var storage = new AzureExperimentStorage(Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);
            var exp = await storage.GetReferenceExperiment();
            var execBlob = storage.GetExecutableReference(exp.Definition.Executable);
            await execBlob.DownloadToFileAsync(exp.Definition.Executable, FileMode.Create);

            // todo: use LocalExperimentRunner.RunBenchmark

            List<Measure> measurements = new List<Measure>(exp.Repetitions);
            for (int i = 0; i < exp.Repetitions; ++i)
            {
                measurements.Add(ProcessMeasurer.Measure(exp.Definition.Executable, exp.Definition.Parameters, exp.Definition.BenchmarkTimeout, exp.Definition.MemoryLimitMB));
            }
        }
    }
}
