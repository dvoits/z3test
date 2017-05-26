using AzurePerformanceTest;
using Measurement;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Z3Data;

namespace ImportTimeline
{
    class Program
    {

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("ImportTimeline.exe <path-to-data> <storage connection string>");
                return;
            }

            string pathToData = args[0];
            string connectionString = args[1];

            AzureExperimentStorage storage = null;
            try
            {
                storage = new AzureExperimentStorage(connectionString);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to connect to the storage: " + ex.Message);
                return;
            }

            Stopwatch sw = Stopwatch.StartNew();

            Console.Write("Uploading experiments table from {0}... ", pathToData);
            var submitted = UploadExperiments(pathToData, storage);
            // for debug: 
            //Dictionary<int, DateTime> submitted = null;

            Console.WriteLine("Uploading results table...");
            UploadResults(pathToData, submitted, storage);

            sw.Stop();

            Console.WriteLine("Done, total time is {0}", sw.Elapsed);
        }

        static Dictionary<int, DateTime> UploadExperiments(string pathToData, AzureExperimentStorage storage)
        {
            Dictionary<int, DateTime> submitted = new Dictionary<int, DateTime>(5000);
            var experiments =
                Directory.EnumerateFiles(pathToData, "*_meta.csv")
                .Select(file =>
                {
                    var metadata = new MetaData(file);
                    var exp = new ExperimentEntity((int)metadata.Id);
                    exp.Submitted = metadata.SubmissionTime;
                    exp.BenchmarkContainer = metadata.BaseDirectory;
                    exp.BenchmarkFileExtension = "smt2";
                    exp.Category = "smtlib-latest";
                    exp.Executable = metadata.BinaryId.ToString();
                    exp.Parameters = metadata.Parameters;
                    exp.BenchmarkTimeout = metadata.Timeout;
                    exp.MemoryLimitMB = metadata.Memoryout / 1024.0 / 1024.0;

                    exp.Flag = false;
                    exp.Creator = "";
                    exp.ExperimentTimeout = 0;
                    exp.GroupName = "";

                    exp.Note = String.Format("Cluster: {0}, cluster job id: {1}, node group: {2}, locality: {3}, finished: {4}, reference: {5}",
                        metadata.Cluster, metadata.ClusterJobId, metadata.Nodegroup, metadata.Locality, metadata.isFinished, metadata.Reference);

                    submitted.Add((int)metadata.Id, exp.Submitted);

                    return exp;
                });
            storage.ImportExperiments(experiments).Wait();
            Console.WriteLine("Done.");
            return submitted;
        }

        static void UploadResults(string pathToData, Dictionary<int, DateTime> submitted, AzureExperimentStorage storage)
        {
            List<int> missingExperiments = new List<int>();
            ConcurrentDictionary<string, string> uploadedOutputs = new ConcurrentDictionary<string, string>();

            var upload =
                Directory.EnumerateFiles(pathToData, "*.zip")
                .Select(async file =>
                {
                    int expId = int.Parse(Path.GetFileNameWithoutExtension(file));

                    DateTime submittedTime;
                    if (submitted == null)
                    {
                        submittedTime = DateTime.Now;
                    }
                    else if (!submitted.TryGetValue(expId, out submittedTime))
                    {
                        missingExperiments.Add(expId);
                        Console.WriteLine("Experiment {0} has results but not metadata");
                        return 0;
                    }
                    Console.WriteLine("Uploading results for {0}... ", expId);

                    CSVData table = new CSVData(file, (uint)expId);
                    var buildResults =
                        table.Rows
                        .Select(async r =>
                        {
                            string stdout = await UploadOutput(r.StdOut, uploadedOutputs, storage, String.Format("imported-stdout-{0}-{1}", expId, r.Filename));
                            string stderr = await UploadOutput(r.StdErr, uploadedOutputs, storage, String.Format("imported-stderr-{0}-{1}", expId, r.Filename));

                            var b = new PerformanceTest.BenchmarkResult(
                                expId, r.Filename, "HPC Cluster node",
                                submittedTime, r.Runtime, TimeSpan.FromSeconds(r.Runtime), TimeSpan.FromSeconds(0), 0,
                                ResultCodeToStatus(r.ResultCode), r.ReturnValue,
                                GenerateStreamFromString(stdout), 
                                GenerateStreamFromString(stderr),
                                new Dictionary<string, string>() { });
                            return b;
                        });

                    var entities = await Task.WhenAll(buildResults);
                    Console.WriteLine("All outputs uploaded for {0}", expId);

                    await storage.PutExperimentResultsWithBlobnames(expId, entities);
                    Console.WriteLine("Done uploading results for {0}.", expId);
                    return 0;
                });

            Task.WhenAll(upload).Wait();
            Console.WriteLine("Done.");

            if(missingExperiments.Count > 0)
            {
                Console.WriteLine("\nFollowing experiments have results but not metadata:");
                foreach (var item in missingExperiments)
                {
                    Console.WriteLine(item);
                }
            }
        }

        private static async Task<string> UploadOutput(string content, ConcurrentDictionary<string, string> uploadedOutputs, AzureExperimentStorage storage, string blobName)
        {
            if (String.IsNullOrEmpty(content))
            {
                return String.Empty;
            }

            string blob = uploadedOutputs.GetOrAdd(content, blobName);
            if (blob != blobName)
            {
                return blob;
            }
            else
            {
                await storage.UploadOutput(blobName, content);
                Trace.WriteLine(string.Format("Output blob uploaded {0}", blobName));

                uploadedOutputs[content] = blobName;
                return blobName;
            }
        }

        private static ResultStatus ResultCodeToStatus(uint resultCode)
        {
            switch (resultCode)
            {
                case 0: return ResultStatus.Success;
                case 3: return ResultStatus.Bug;
                case 4: return ResultStatus.Error;
                case 5: return ResultStatus.Timeout;
                case 6: return ResultStatus.OutOfMemory;
                default: throw new ArgumentException("Unknown result code: " + resultCode);
            }
        }


        public static Stream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            if (!String.IsNullOrEmpty(s))
            {
                StreamWriter writer = new StreamWriter(stream);
                writer.Write(s);
                writer.Flush();
                stream.Position = 0;
            }
            return stream;
        }
    }
}
