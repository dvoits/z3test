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
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Threading;

namespace AzureWorker
{
    class Program
    {
        static readonly TimeSpan ExtraTimeForOverhead = TimeSpan.FromSeconds(900);

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
                case "--measure":
                    Measure(subArgs).Wait();
                    return 0;
                case "--reference-run":
                    RunReference(subArgs).Wait();
                    return 0;
                case "--manage-tasks":
                    ManageTasks(subArgs).Wait();
                    return 0;
                default:
                    Console.WriteLine("Incorrect first parameter.");
                    return 1;
            }
        }

        const string PerformanceCoefficientFileName = "normal.txt";
        const string SharedDirEnvVariableName = "AZ_BATCH_NODE_SHARED_DIR";
        const string JobIdEnvVariableName = "AZ_BATCH_JOB_ID";
        const string InfrastructureErrorPrefix = "INFRASTRUCTURE ERROR: ";

        static async Task ManageTasks(string[] args)
        {
            int experimentId = int.Parse(args[0]);
            string benchmarkContainerUri = args[1];
            string benchmarkDirectory = args[2];
            string benchmarkCategory = args[3];
            string executable = args[4];
            string arguments = args[5];
            TimeSpan timeout = TimeSpan.FromSeconds(double.Parse(args[6]));
            double memoryLimit = 0; // no limit
            long? outputLimit = null;
            long? errorLimit = null;
            if (args.Length > 7)
            {
                memoryLimit = double.Parse(args[7]);
                if (args.Length > 8)
                {
                    outputLimit = args[8] == "null" ? null : (long?)long.Parse(args[8]);
                    if (args.Length > 9)
                    {
                        errorLimit = args[9] == "null" ? null : (long?)long.Parse(args[9]);
                    }
                }
            }
            Console.WriteLine(String.Format("Params are:\n id: {0}\ncontainer: {8}\ndirectory:{9}\ncategory: {1}\nexec: {2}\nargs: {3}\ntimeout: {4}\nmemlimit: {5}\noutlimit: {6}\nerrlimit: {7}", experimentId, benchmarkCategory, executable, arguments, timeout, memoryLimit, outputLimit, errorLimit, benchmarkContainerUri, benchmarkDirectory));

            string jobId = "exp" + experimentId.ToString();

            var batchCred = new BatchSharedKeyCredentials(Settings.Default.BatchAccountUrl, Settings.Default.BatchAccountName, Settings.Default.BatchAccountKey);

            var storage = new AzureExperimentStorage(Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);
            AzureBenchmarkStorage benchmarkStorage = CreateBenchmarkStorage(benchmarkContainerUri);

            var expInfo = await storage.GetExperiment(experimentId);

            string storageConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);

            var queue = await storage.CreateResultsQueue(experimentId);
            Console.Write("Created queue");

            await FetchSavedResults(experimentId);
            Console.WriteLine("Fetched existing results");
            var collectionTask = CollectResults(experimentId);
            Console.WriteLine("Started collection thread.");

            using (BatchClient batchClient = BatchClient.Open(batchCred))
            {
                var job = await batchClient.JobOperations.GetJobAsync(jobId);
                var pool = await batchClient.PoolOperations.GetPoolAsync(job.PoolInformation.PoolId);
                var vmsize = pool.VirtualMachineSize;
                //starting results collection

                if (expInfo.TotalBenchmarks <= 0)
                {
                    //not all experiments started
                    ODATADetailLevel detailLevel = new ODATADetailLevel();
                    detailLevel.SelectClause = "id,displayName";
                    
                    Console.WriteLine("Listing existing tasks.");
                    var processedBlobs = new SortedSet<string>(batchClient.JobOperations.ListTasks(jobId, detailLevel)
                        .SelectMany(t =>
                        {
                            int id;
                            if (int.TryParse(t.Id, out id))
                            {
                                // we put benchmark file first
                                return new string[] { t.DisplayName };
                            }
                            return new string[] { };
                        }));
                    Console.WriteLine("Done!");

                    BlobContinuationToken continuationToken = null;
                    BlobResultSegment resultSegment = null;

                    List<Task> starterTasks = new List<Task>();
                    int totalBenchmarks = 0;
                    string benchmarksPath = CombineBlobPath(benchmarkDirectory, benchmarkCategory);
                    do
                    {
                        resultSegment = await benchmarkStorage.ListBlobsSegmentedAsync(benchmarksPath, continuationToken);
                        Console.WriteLine("Got some blobs");
                        starterTasks.Add(StartTasksForSegment(timeout.TotalSeconds.ToString(), experimentId, executable, arguments, memoryLimit, outputLimit, errorLimit, jobId, batchClient, resultSegment.Results, totalBenchmarks, processedBlobs, benchmarkStorage));

                        continuationToken = resultSegment.ContinuationToken;
                        totalBenchmarks += resultSegment.Results.Count();
                    }
                    while (continuationToken != null);

                    using (var ms = new MemoryStream())
                    {
                        ms.WriteByte(2);//This is number of benchmarks
                        (new BinaryFormatter()).Serialize(ms, totalBenchmarks);
                        await queue.AddMessageAsync(new CloudQueueMessage(ms.ToArray()));
                    }

                    await Task.WhenAll(starterTasks.ToArray());
                    Console.WriteLine("Finished starting tasks");
                }

                // Monitoring tasks
                ODATADetailLevel monitorLevel = new ODATADetailLevel();
                monitorLevel.FilterClause = "(state eq 'completed') and (executionInfo/exitCode ne 0)";
                monitorLevel.SelectClause = "id,displayName,executionInfo";
                do
                {
                    Console.WriteLine("Fetching failed tasks...");
                    badResults = batchClient.JobOperations.ListTasks(jobId, monitorLevel)
                        .Select(task => new AzureBenchmarkResult
                        {
                            AcquireTime = task.ExecutionInformation.StartTime ?? DateTime.MinValue,
                            BenchmarkFileName = task.DisplayName,
                            ExitCode = task.ExecutionInformation.ExitCode ?? int.MinValue,
                            ExperimentID = experimentId,
                            StdErr = InfrastructureErrorPrefix + task.ExecutionInformation.FailureInformation.Message,
                            StdErrStoredExternally = false,
                            StdOut = "",
                            StdOutStoredExternally = false,
                            NormalizedRuntime = -1,
                            PeakMemorySizeMB = -1,
                            Properties = new Dictionary<string, string>(),
                            Status = ResultStatus.Error,
                            TotalProcessorTime = TimeSpan.Zero,
                            WallClockTime = TimeSpan.Zero,
                            WorkerInformation = vmsize
                        }).ToList();
                    Console.WriteLine("Done fetching failed tasks. Got {0}.", badResults.Count);
                }
                while (!collectionTask.Wait(30000));

                Console.WriteLine("Closing.");
            }
        }

        static List<AzureBenchmarkResult> goodResults = new List<AzureBenchmarkResult>();
        static List<AzureBenchmarkResult> badResults = new List<AzureBenchmarkResult>();

        static async Task FetchSavedResults(int experimentId)
        {
            var storage = new AzureExperimentStorage(Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);
            var results = (await storage.GetAzureExperimentResults(experimentId));
            goodResults = new List<AzureBenchmarkResult>();
            badResults = new List<AzureBenchmarkResult>();
            foreach (var r in results)
            {
                if (r.StdErr.StartsWith(InfrastructureErrorPrefix) || (r.StdErrStoredExternally && Utils.StreamToString(storage.ParseAzureBenchmarkResult(r).StdErr, false).StartsWith(InfrastructureErrorPrefix)))
                    badResults.Add(r);
                else
                    goodResults.Add(r);
            }
        }

        static async Task CollectResults(string[] args)
        {
            int experimentId = int.Parse(args[0]);
            await FetchSavedResults(experimentId);
            await CollectResults(experimentId);
        }

        static async Task CollectResults(int experimentId)
        {
            Console.WriteLine("Started collection.");
            var storage = new AzureExperimentStorage(Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);
            var queue = storage.GetResultsQueueReference(experimentId);
            List<AzureBenchmarkResult> results = new List<AzureBenchmarkResult>();// (await storage.GetAzureExperimentResults(experimentId)).ToList();
            var expInfo = await storage.GetExperiment(experimentId);
            int totalBenchmarks = expInfo.TotalBenchmarks > 0 ? expInfo.TotalBenchmarks : -1;
            int processedBenchmarks = goodResults.Count + badResults.Count;// results.Count;

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
                            goodResults.Add((AzureBenchmarkResult)formatter.Deserialize(ms));
                            ++processedBenchmarks;
                        }
                    }
                }
                int oldCount = results.Count;
                results = goodResults.Concat(badResults).ToList();
                results.Sort((a, b) => string.Compare(a.BenchmarkFileName, b.BenchmarkFileName));
                await storage.PutAzureExperimentResults(experimentId, results.ToArray(), true);
                await storage.SetCompletedBenchmarks(experimentId, results.Count);
                foreach (CloudQueueMessage message in messages)
                {
                    queue.DeleteMessage(message);
                }
                //TODO: consider possible duplicates
                if (oldCount == results.Count)
                    Thread.Sleep(500);
            }
            while (totalBenchmarks == -1 || results.Count < totalBenchmarks);
            await storage.DeleteResultsQueue(experimentId);

            var totalRuntime = results.Sum(r => r.NormalizedRuntime);
            await storage.SetTotalRuntime(experimentId, totalRuntime);
            Console.WriteLine("Collected all results.");
        }

        private static string CombineBlobPath(string benchmarkDirectory, string benchmarkCategory)
        {
            string benchmarksPath;
            if (string.IsNullOrEmpty(benchmarkDirectory))
                benchmarksPath = benchmarkCategory;
            else if (string.IsNullOrEmpty(benchmarkCategory))
                benchmarksPath = benchmarkDirectory;
            else
            {
                var benchmarksDirClear = benchmarkDirectory.TrimEnd('/');
                var benchmarksCatClear = benchmarkCategory.TrimStart('/');
                benchmarksPath = benchmarksDirClear + "/" + benchmarksCatClear;
            }
            return benchmarksPath;
        }

        private static async Task StartTasksForSegment(string timeout, int experimentId, string executable, string arguments, double memoryLimit, long? outputLimit, long? errorLimit, string jobId, BatchClient batchClient, IEnumerable<IListBlobItem> segmentResults, int startTaskId, ICollection<string> processedBlobs, AzureBenchmarkStorage benchmarkStorage)
        {
            List<CloudTask> tasks = new List<CloudTask>();
            int blobNo = startTaskId;
            if (processedBlobs == null)
                processedBlobs = new string[] { };
            foreach (CloudBlockBlob blobItem in segmentResults)
            {
                string taskId = blobNo.ToString();
                if (!processedBlobs.Contains(blobItem.Name))
                {
                    string[] parts = blobItem.Name.Split('/');
                    string shortName = parts[parts.Length - 1];
                    string taskCommandLine = String.Format("cmd /c %" + SharedDirEnvVariableName + "%\\%" + JobIdEnvVariableName + "%\\AzureWorker.exe --measure {0} \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\" \"{8}\"", experimentId, blobItem.Name, executable, arguments, shortName, timeout, memoryLimit, NullableLongToString(outputLimit), NullableLongToString(errorLimit));
                    var resourceFile = new ResourceFile(benchmarkStorage.GetBlobSASUri(blobItem), shortName);
                    CloudTask task = new CloudTask(taskId, taskCommandLine);
                    task.ResourceFiles = new List<ResourceFile> { resourceFile };
                    task.Constraints = new TaskConstraints();
                    task.Constraints.MaxWallClockTime = TimeSpan.FromSeconds(double.Parse(timeout)) + ExtraTimeForOverhead;
                    task.DisplayName = blobItem.Name;
                    tasks.Add(task);
                }

                ++blobNo;
            }
            if (tasks.Count > 0)
            {
                Console.WriteLine("Starting tasks...");
                await batchClient.JobOperations.AddTaskAsync(jobId, tasks);
                Console.WriteLine("Started some tasks");
            }
        }

        static string NullableLongToString(long? number)
        {
            return number == null ? "null" : number.Value.ToString();
        }

        static async Task Measure(string[] args)
        {
            int experimentId = int.Parse(args[0]);
            string benchmarkId = args[1];
            string executable = args[2];
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
            double normal = 1.0;

            string workerDir = Path.Combine(Environment.GetEnvironmentVariable(SharedDirEnvVariableName), Environment.GetEnvironmentVariable(JobIdEnvVariableName));
            executable = Path.Combine(workerDir, "exec", executable);
            string normalFilePath = Path.Combine(workerDir, PerformanceCoefficientFileName);
            if (File.Exists(normalFilePath))
            {
                normal = double.Parse(File.ReadAllText(normalFilePath));
                Trace.WriteLine(string.Format("Normal found within file: {0}", normal));
            }
            else
            {
                Trace.WriteLine("Normal not found within file, computing.");
                normal = await RunReference(new string[] { });
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
                normal,
                "");

            var storage = new AzureExperimentStorage(Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);
            await storage.PutResult(experimentId, result);
        }

        static async Task<double> RunReference(string[] args)
        {
            string workerDir = Path.Combine(Environment.GetEnvironmentVariable(SharedDirEnvVariableName), Environment.GetEnvironmentVariable(JobIdEnvVariableName));
            string normalFilePath = Path.Combine(workerDir, PerformanceCoefficientFileName);
            var storage = new AzureExperimentStorage(Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);
            //var exp = await storage.GetReferenceExperiment();
            //if (exp == null)
            //{
            //    //no reference experiment
            //    File.WriteAllText(normalFilePath, "1.0");
            //    return 1.0;
            //}
            string refJsonPath = Path.Combine(workerDir, "reference.json");
            if (!File.Exists(refJsonPath))
            {
                //no reference experiment
                Trace.WriteLine("Reference.json not found, assuming normal 1.0.");
                File.WriteAllText(normalFilePath, "1.0");
                return 1.0;
            }
            var exp = ParseReferenceExperiment(refJsonPath);
            
            var pathForBenchmarks = Path.Combine(workerDir, "refdata", "data");
            var execPath = Path.Combine(workerDir, "refdata", exp.Definition.Executable);

            Domain domain = new Z3Domain(); // todo: take custom domain name from `args`
            string[] benchmarks = Directory.EnumerateFiles(pathForBenchmarks).Select(fn => Path.Combine(pathForBenchmarks, fn)).ToArray();
            Trace.WriteLine(string.Format("Found {0} benchmarks in folder {1}", benchmarks.Length, pathForBenchmarks));
            BenchmarkResult[] results = new BenchmarkResult[benchmarks.Length];
            for (int i = 0; i < benchmarks.Length; ++i)
            {
                Trace.WriteLine(string.Format("Procssing reference file {0}", benchmarks[i]));
                results[i] = LocalExperimentRunner.RunBenchmark(
                    -1,
                    execPath,
                    exp.Definition.Parameters,
                    "ref",
                    benchmarks[i],
                    exp.Repetitions,
                    exp.Definition.BenchmarkTimeout,
                    exp.Definition.MemoryLimitMB,
                    null,
                    null,
                    domain,
                    1.0,
                    ""
                    );
            }

            var totalRuntime = results.Sum(r => r.NormalizedRuntime);
            double normal = exp.ReferenceValue / totalRuntime;

            File.WriteAllText(normalFilePath, normal.ToString());
            return normal;
        }

        static AzureBenchmarkStorage CreateBenchmarkStorage(string uri)
        {
            if (uri == ExperimentDefinition.DefaultContainerUri)
                return new AzureBenchmarkStorage(Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey, AzureBenchmarkStorage.DefaultContainerName);
            else
                return new AzureBenchmarkStorage(uri);
        }

        private static ReferenceExperiment ParseReferenceExperiment(string filename)
        {
            string content = File.ReadAllText(filename);
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ContractResolver = new PrivatePropertiesResolver();
            ReferenceExperiment reference = JsonConvert.DeserializeObject<ReferenceExperiment>(content, settings);
            return reference;
        }

        internal class PrivatePropertiesResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty prop = base.CreateProperty(member, memberSerialization);
                prop.Writable = true;
                return prop;
            }
        }

    }
}
