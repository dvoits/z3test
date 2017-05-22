using AzurePerformanceTest;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaunchExperiment
{
    class Program
    {
        static string storageName = "";
        static string storageKey = "";
        static string batchUri = "";
        static string batchName = "";
        static string batchKey = "";

        static void Main(string[] args)
        {
            var manager = AzureExperimentManager.Open(new AzureExperimentStorage(storageName, storageKey), batchUri, batchName, batchKey);

            var id = manager.StartExperiment(ExperimentDefinition.Create("z3.zip", "input", "smt2", "model_validate=true -smt2 -file:{0}", TimeSpan.FromSeconds(1200), "QF_BV/"), "Dmitry K", "test").Result;

            Console.WriteLine("Experiment id:" + id);
            Console.ReadLine();
        }
    }
}
