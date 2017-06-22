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
            string extensionsString = args[4];
            string domainString = args[5];
            string executable = args[6];
            string arguments = args[7];
            TimeSpan timeout = TimeSpan.FromSeconds(double.Parse(args[8]));
            double memoryLimit = 0; // no limit
            long? outputLimit = null;
            long? errorLimit = null;
            if (args.Length > 9)
            {
                memoryLimit = double.Parse(args[9]);
                if (args.Length > 10)
                {
                    outputLimit = args[10] == "null" ? null : (long?)long.Parse(args[10]);
                    if (args.Length > 11)
                    {
                        errorLimit = args[11] == "null" ? null : (long?)long.Parse(args[11]);
                    }
                }
            }
            Console.WriteLine(String.Format("Params are:\n id: {0}\ncontainer: {8}\ndirectory:{9}\ncategory: {1}\nextensions: {10}\ndomain: {11}\nexec: {2}\nargs: {3}\ntimeout: {4}\nmemlimit: {5}\noutlimit: {6}\nerrlimit: {7}", experimentId, benchmarkCategory, executable, arguments, timeout, memoryLimit, outputLimit, errorLimit, benchmarkContainerUri, benchmarkDirectory, extensionsString, domainString));

            string jobId = "exp" + experimentId.ToString();

            var secretStorage = new SecretStorage(Settings.Default.AADApplicationId, Settings.Default.AADApplicationCertThumbprint, Settings.Default.KeyVaultUrl);
            BatchConnectionString credentials = new BatchConnectionString(await secretStorage.GetSecret(Settings.Default.ConnectionStringSecretId));
            //var storageKey = cre.;
            //var batchKey = await secretStorage.GetSecret(Settings.Default.BatchAccountKeyId);
            Console.WriteLine("Retrieved credentials.");


            var batchCred = new BatchSharedKeyCredentials(credentials.BatchURL, credentials.BatchAccountName, credentials.BatchAccessKey);

            var storage = new AzureExperimentStorage(credentials.WithoutBatchData().ToString());
            AzureBenchmarkStorage benchmarkStorage = CreateBenchmarkStorage(benchmarkContainerUri, storage);

            var expInfo = await storage.GetExperiment(experimentId);

            var queue = await storage.CreateResultsQueue(experimentId);
            Console.Write("Created queue");

            await FetchSavedResults(experimentId, storage);
            Console.WriteLine("Fetched existing results");
            var collectionTask = CollectResults(experimentId, storage);
            Console.WriteLine("Started collection thread.");
            Domain domain = ResolveDomain(domainString);
            SortedSet<string> extensions;
            if (string.IsNullOrEmpty(extensionsString))
                extensions = new SortedSet<string>(domain.BenchmarkExtensions.Distinct());
            else
                extensions = new SortedSet<string>(extensionsString.Split('|').Select(s => s.Trim().TrimStart('.')).Distinct());

            using (BatchClient batchClient = BatchClient.Open(batchCred))
            {
                //var job = await batchClient.JobOperations.GetJobAsync(jobId);
                //var pool = await batchClient.PoolOperations.GetPoolAsync(job.PoolInformation.PoolId);
                //var vmsize = pool.VirtualMachineSize;
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
                    string outputQueueUri = storage.GetOutputQueueSASUri(experimentId, TimeSpan.FromHours(48));
                    string outputContainerUri = storage.GetOutputContainerSASUri(TimeSpan.FromHours(48));
                    do
                    {
                        resultSegment = await benchmarkStorage.ListBlobsSegmentedAsync(benchmarksPath, continuationToken);
                        Console.WriteLine("Got some blobs");
                        CloudBlockBlob[] blobsToProcess = resultSegment.Results.SelectMany(item =>
                        {
                            var blob = item as CloudBlockBlob;
                            if (blob == null || processedBlobs.Contains(blob.Name))
                                return new CloudBlockBlob[] { };

                            var nameParts = blob.Name.Split('/');
                            var shortnameParts = nameParts[nameParts.Length - 1].Split('.');
                            if (shortnameParts.Length == 1 && !extensions.Contains(""))
                                return new CloudBlockBlob[] { };
                            var ext = shortnameParts[shortnameParts.Length - 1];
                            if (!extensions.Contains(ext))
                                return new CloudBlockBlob[] { };

                            return new CloudBlockBlob[] { blob };
                        }).ToArray();
                        starterTasks.Add(StartTasksForSegment(timeout.TotalSeconds.ToString(), experimentId, executable, arguments, memoryLimit, domainString, outputQueueUri, outputContainerUri, outputLimit, errorLimit, jobId, batchClient, blobsToProcess, benchmarksPath, totalBenchmarks, benchmarkStorage));

                        continuationToken = resultSegment.ContinuationToken;
                        totalBenchmarks += blobsToProcess.Length;
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
                            ExitCode = task.ExecutionInformation.ExitCode,
                            ExperimentID = experimentId,
                            StdErr = InfrastructureErrorPrefix + task.ExecutionInformation.FailureInformation.Message,
                            StdErrExtStorageIdx = "",
                            StdOut = "",
                            StdOutExtStorageIdx = "",
                            NormalizedRuntime = 0,
                            PeakMemorySizeMB = 0,
                            Properties = new Dictionary<string, string>(),
                            Status = ResultStatus.InfrastructureError,
                            TotalProcessorTime = TimeSpan.Zero,
                            WallClockTime = TimeSpan.Zero
                        }).ToList();
                    Console.WriteLine("Done fetching failed tasks. Got {0}.", badResults.Count);
                }
                while (!collectionTask.Wait(30000));

                Console.WriteLine("Closing.");
            }
        }

        static List<AzureBenchmarkResult> goodResults = new List<AzureBenchmarkResult>();
        static List<AzureBenchmarkResult> badResults = new List<AzureBenchmarkResult>();

        static async Task FetchSavedResults(int experimentId, AzureExperimentStorage storage)
        {
            var results = (await storage.GetAzureExperimentResults(experimentId));
            goodResults = new List<AzureBenchmarkResult>();
            badResults = new List<AzureBenchmarkResult>();
            foreach (var r in results)
            {
                if (r.StdErr.StartsWith(InfrastructureErrorPrefix) || (!string.IsNullOrEmpty(r.StdErrExtStorageIdx) && Utils.StreamToString(storage.ParseAzureBenchmarkResult(r).StdErr, false).StartsWith(InfrastructureErrorPrefix)))
                    badResults.Add(r);
                else
                    goodResults.Add(r);
            }
        }

        static async Task CollectResults(int experimentId, AzureExperimentStorage storage)
        {
            Console.WriteLine("Started collection.");
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
            benchmarksPath = benchmarksPath.TrimEnd('/');
            if (benchmarksPath.Length > 0)
                benchmarksPath = benchmarksPath + "/";
            return benchmarksPath;
        }

        private static async Task StartTasksForSegment(string timeout, int experimentId, string executable, string arguments, double memoryLimit, string domainName, string queueUri, string containerUri, long? outputLimit, long? errorLimit, string jobId, BatchClient batchClient, IEnumerable<CloudBlockBlob> blobsToProcess, string blobFolderPath, int startTaskId, AzureBenchmarkStorage benchmarkStorage)
        {
            List<CloudTask> tasks = new List<CloudTask>();
            int blobNo = startTaskId;
            int blobFolderPathLength = blobFolderPath.Length;
            foreach (CloudBlockBlob blobItem in blobsToProcess)
            {
                string taskId = blobNo.ToString();
                string[] parts = blobItem.Name.Split('/');
                string shortName = parts[parts.Length - 1];
                string taskCommandLine = String.Format("cmd /c %" + SharedDirEnvVariableName + "%\\%" + JobIdEnvVariableName + "%\\AzureWorker.exe --measure {0} \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\" \"{8}\" \"{9}\" \"{10}\" \"{11}\"", experimentId, blobItem.Name.Substring(blobFolderPathLength), executable, arguments, shortName, timeout, domainName, queueUri, containerUri, memoryLimit, NullableLongToString(outputLimit), NullableLongToString(errorLimit));
                var resourceFile = new ResourceFile(benchmarkStorage.GetBlobSASUri(blobItem), shortName);
                CloudTask task = new CloudTask(taskId, taskCommandLine);
                task.ResourceFiles = new List<ResourceFile> { resourceFile };
                task.Constraints = new TaskConstraints();
                task.Constraints.MaxWallClockTime = TimeSpan.FromSeconds(double.Parse(timeout)) + ExtraTimeForOverhead;
                task.DisplayName = blobItem.Name;
                tasks.Add(task);

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
            string domainName = args[6];
            Uri outputQueueUri = new Uri(args[7]);
            Uri outputBlobContainerUri = new Uri(args[8]);
            double memoryLimit = 0; // no limit
            long? outputLimit = null;
            long? errorLimit = null;
            //if (args.Length > 6)
            //{
            //    workerInfo = args[6];
            if (args.Length > 9)
            {
                memoryLimit = double.Parse(args[9]);
                if (args.Length > 10)
                {
                    outputLimit = args[10] == "null" ? null : (long?)long.Parse(args[10]);
                    if (args.Length > 11)
                    {
                        errorLimit = args[11] == "null" ? null : (long?)long.Parse(args[11]);
                    }
                }
            }
            //}
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

            Domain domain = ResolveDomain(domainName);
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
                normal);

            await AzureExperimentStorage.PutResult(experimentId, result, new CloudQueue(outputQueueUri), new CloudBlobContainer(outputBlobContainerUri));
        }

        static Task<double> RunReference(string[] args)
        {
            string workerDir = Path.Combine(Environment.GetEnvironmentVariable(SharedDirEnvVariableName), Environment.GetEnvironmentVariable(JobIdEnvVariableName));
            string normalFilePath = Path.Combine(workerDir, PerformanceCoefficientFileName);
            string refJsonPath = Path.Combine(workerDir, "reference.json");
            if (!File.Exists(refJsonPath))
            {
                //no reference experiment
                Trace.WriteLine("Reference.json not found, assuming normal 1.0.");
                File.WriteAllText(normalFilePath, "1.0");
                return Task.FromResult(1.0);
            }
            var exp = ParseReferenceExperiment(refJsonPath);

            var pathForBenchmarks = Path.Combine(workerDir, "refdata", "data");
            var execPath = Path.Combine(workerDir, "refdata", exp.Definition.Executable);

            Domain domain = ResolveDomain(exp.Definition.DomainName);
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
                    1.0);
            }

            var totalRuntime = results.Sum(r => r.NormalizedRuntime);
            double normal = exp.ReferenceValue / totalRuntime;

            File.WriteAllText(normalFilePath, normal.ToString());
            return Task.FromResult(normal);
        }

        static AzureBenchmarkStorage CreateBenchmarkStorage(string uri, AzureExperimentStorage experimentStorage)
        {
            if (uri == ExperimentDefinition.DefaultContainerUri)
                return experimentStorage.DefaultBenchmarkStorage;
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

        private static Domain ResolveDomain(string domainName)
        {
            var domainResolver = MEFDomainResolver.Instance;
            return domainResolver.GetDomain(domainName);
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
