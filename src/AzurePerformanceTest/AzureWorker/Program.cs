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
                case "--collect-results":
                    CollectResults(subArgs).Wait();
                    return 0;
                default:
                    Console.WriteLine("Incorrect first parameter.");
                    return 1;
            }
        }

        const string PerformanceCoefficientFileName = "normal.txt";
        const string SharedDirEnvVariableName = "AZ_BATCH_NODE_SHARED_DIR";

        static async Task CollectResults(string[] args)
        {
            int experimentId = int.Parse(args[0]);

            var storage = new AzureExperimentStorage(Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);
            var queue = storage.GetResultsQueueReference(experimentId);
            List<BenchmarkResult> results = (await storage.GetResults(experimentId)).ToList();
            var expInfo = await storage.GetExperiment(experimentId);
            int totalBenchmarks = expInfo.TotalBenchmarks > 0 ? expInfo.TotalBenchmarks : -1;
            int processedBenchmarks = results.Count;
            
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
                results.Sort((a, b) => string.Compare(a.BenchmarkFileName, b.BenchmarkFileName));
                await storage.PutExperimentResultsWithBlobnames(experimentId, results.ToArray(), true);
                await storage.SetCompletedBenchmarks(experimentId, processedBenchmarks);
                foreach (CloudQueueMessage message in messages)
                {
                    queue.DeleteMessage(message);
                }
            }
            while (totalBenchmarks == -1 || processedBenchmarks < totalBenchmarks);
            await storage.DeleteResultsQueue(experimentId);

            var totalRuntime = results.Sum(r => r.NormalizedRuntime);
            await storage.SetTotalRuntime(experimentId, totalRuntime);
        }

        static async Task AddTasks(string[] args)
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
            Trace.WriteLine(String.Format("Params are:\n id: {0}\ncontainer: {8}\ndirectory:{9}\ncategory: {1}\nexec: {2}\nargs: {3}\ntimeout: {4}\nmemlimit: {5}\noutlimit: {6}\nerrlimit: {7}", experimentId, benchmarkCategory, executable, arguments, timeout, memoryLimit, outputLimit, errorLimit, benchmarkContainerUri, benchmarkDirectory));

            string jobId = "exp" + experimentId.ToString();

            var batchCred = new BatchSharedKeyCredentials(Settings.Default.BatchAccountUrl, Settings.Default.BatchAccountName, Settings.Default.BatchAccountKey);

            var storage = new AzureExperimentStorage(Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);
            AzureBenchmarkStorage benchmarkStorage = CreateBenchmarkStorage(benchmarkContainerUri);

            string storageConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", Settings.Default.StorageAccountName, Settings.Default.StorageAccountKey);
            
            var execResourceFile = new ResourceFile(storage.GetExecutableSasUri(executable), executable);
            Trace.WriteLine("Resourced executable");

            var queue = await storage.CreateResultsQueue(experimentId);
            Trace.Write("Created queue");

            using (BatchClient batchClient = BatchClient.Open(batchCred))
            {
                //starting results collection
                string taskCommandLine = string.Format("cmd /c %" + SharedDirEnvVariableName + "%\\AzureWorker.exe --collect-results {0}", experimentId);
                CloudTask task = new CloudTask("resultCollection", taskCommandLine);
                await batchClient.JobOperations.AddTaskAsync(jobId, task);

                BlobContinuationToken continuationToken = null;
                BlobResultSegment resultSegment = null;

                List<Task> starterTasks = new List<Task>();
                int batchNo = 0;
                int totalBenchmarks = 0;
                string benchmarksPath = CombineBlobPath(benchmarkDirectory, benchmarkCategory);
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

        private static async Task StartTasksForSegment(ResourceFile execResourceFile, string timeout, int experimentId, string executable, string arguments, double memoryLimit, long? outputLimit, long? errorLimit, string jobId, BatchClient batchClient, IEnumerable<IListBlobItem> segmentResults, int idPart, AzureBenchmarkStorage benchmarkStorage)
        {
            List<CloudTask> tasks = new List<CloudTask>();
            int blobNo = 0;
            foreach (CloudBlockBlob blobItem in segmentResults)
            {
                string taskId = idPart.ToString() + "_" + blobNo.ToString();
                string[] parts = blobItem.Name.Split('/');
                string shortName = parts[parts.Length - 1];
                string taskCommandLine = String.Format("cmd /c %" + SharedDirEnvVariableName + "%\\AzureWorker.exe --measure {0} \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\" \"{8}\"", experimentId, blobItem.Name, executable, arguments, shortName, timeout, memoryLimit, NullableLongToString(outputLimit), NullableLongToString(errorLimit));
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
            double normal = 1.0;

            string workerDir = Environment.GetEnvironmentVariable(SharedDirEnvVariableName);
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
            string workerDir = Environment.GetEnvironmentVariable(SharedDirEnvVariableName);
            string normalFilePath = Path.Combine(workerDir, PerformanceCoefficientFileName);
            //var random = new Random();
            //System.Threading.Thread.Sleep(random.Next(60000));
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

            //var benchmarkStorage = CreateBenchmarkStorage(exp.Definition.BenchmarkContainerUri);
            //var benchPath = CombineBlobPath(exp.Definition.BenchmarkDirectory, exp.Definition.Category);
            var pathForBenchmarks = Path.Combine(workerDir, "refdata", "data");
            //Directory.CreateDirectory(pathForBenchmarks);
            //var execBlob = storage.GetExecutableReference(exp.Definition.Executable);
            var execPath = Path.Combine(workerDir, "refdata", exp.Definition.Executable);
            //await execBlob.DownloadToFileAsync(execPath, FileMode.Create);

            //BlobContinuationToken continuationToken = null;
            //BlobResultSegment resultSegment = null;

            //List<Task> dlTasks = new List<Task>();
            //int no = 0;
            //do
            //{
            //    resultSegment = await benchmarkStorage.ListBlobsSegmentedAsync(benchPath, continuationToken);
            //    foreach (CloudBlockBlob blob in resultSegment.Results)
            //    {
            //        dlTasks.Add(blob.DownloadToFileAsync(Path.Combine(pathForBenchmarks, no.ToString() + ".test"), FileMode.Create));
            //        ++no;
            //    }

            //    continuationToken = resultSegment.ContinuationToken;
            //}
            //while (continuationToken != null);
            //await Task.WhenAll(dlTasks);
            
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
