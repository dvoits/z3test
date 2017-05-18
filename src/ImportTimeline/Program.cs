using AzurePerformanceTest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
                    exp.PartitionKey = exp.Category = "smtlib";
                    exp.Executable = metadata.BinaryId.ToString();
                    exp.Parameters = metadata.Parameters;
                    exp.BenchmarkTimeout = metadata.Timeout;
                    exp.MemoryLimit = (int)(metadata.Memoryout >> 20);

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
            var upload =
                Directory.EnumerateFiles(pathToData, "*.zip")
                .AsParallel()
                .Select(file =>
                {
                    int expId = int.Parse(Path.GetFileNameWithoutExtension(file));
                    var submittedTime = submitted[expId];

                    Console.WriteLine("Uploading results for {0}... ", expId);

                    CSVData table = new CSVData(file, (uint)expId);
                    var entities =
                        table.Rows
                        .Select(r =>
                        {
                            var measure = new Measurement.ProcessRunMeasure(TimeSpan.FromSeconds(r.Runtime), TimeSpan.FromSeconds(0), 0, ResultCodeToStatus(r.ResultCode),
                                r.ReturnValue,
                                GenerateStreamFromString(r.StdOut),
                                GenerateStreamFromString(r.StdErr));
                            var b = new PerformanceTest.BenchmarkResult(expId, r.Filename, "HPC Cluster node", r.Runtime, submittedTime, measure);
                            return b;
                        });
                    return storage.PutExperimentResults(entities).ContinueWith(t =>
                    {
                        Console.WriteLine("Done uploading results for {0}.", expId);
                    });
                });

            Task.WhenAll(upload).Wait();
            Console.WriteLine("Done.");
        }

        private static Measurement.Measure.CompletionStatus ResultCodeToStatus(uint resultCode)
        {
            switch (resultCode)
            {
                case 0: return Measurement.Measure.CompletionStatus.Success;
                case 3: return Measurement.Measure.CompletionStatus.Bug;
                case 4: return Measurement.Measure.CompletionStatus.Error;
                case 5: return Measurement.Measure.CompletionStatus.Timeout;
                case 6: return Measurement.Measure.CompletionStatus.OutOfMemory;
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
