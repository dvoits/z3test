using Angara.Data;
using AzurePerformanceTest;
using Ionic.Zip;
using System;
using System.Collections.Generic;
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

            Console.Write("Uploading experiments table from {0}... ", pathToData);
            UploadExperiments(pathToData, storage);
        }

        static void UploadExperiments(string pathToData, AzureExperimentStorage storage)
        {
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

                    return exp;
                });
            storage.ImportExperiments(experiments).Wait();
            Console.WriteLine("Done.");
        }

        static void UploadResults(string pathToData)
        {
            var benchmarks =
                Directory.EnumerateFiles(pathToData, "*.zip")
                .AsParallel()
                .Select(file =>
                {
                    Table t = null;
                    using (ZipFile zip = ZipFile.Read(file))
                    {
                        var tableEntry = zip[Path.GetFileNameWithoutExtension(file) + ".csv"];
                        var stream = tableEntry.InputStream;
                        t = Table.Load(new StreamReader(stream));
                    }


                    var benchmark = new BenchmarkEntity();
                    return benchmark;
                });
            //storage.ImportExperiments(experiments).Wait();
            Console.WriteLine("Done.");
        }
    }
}
