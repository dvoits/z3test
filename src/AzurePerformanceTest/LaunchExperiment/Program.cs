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
        static string storageName = "cz3test2";
        static string storageKey = "3gbt4wTBGpC0uAZpxUJTmsj6aL/fqhsd86edeVvCmSUuuOaS//wQ/SiSWkIejnbvrkLpCQDVRmHZsiAyJfyjJA==";
        static string batchUri = "https://msrcambatchwe.westeurope.batch.azure.com";
        static string batchName = "msrcambatchwe";
        static string batchKey = "ugabK5qw226T0dZFby9621mCwWZj/chWBT5I+Abg808mU6D0WImiwu29Awja3MjFHdfh6NIQU1q4E+lmmHoErQ==";

        static void Main(string[] args)
        {
            var manager = AzureExperimentManager.Open(new AzureExperimentStorage(storageName, storageKey), batchUri, batchName, batchKey);

            var id = manager.StartExperiment(ExperimentDefinition.Create("z3.zip", "input", "smt2", "model_validate=true -smt2 -file:{0}", TimeSpan.FromSeconds(1200), "QF_BV/"), "Dmitry K", "test").Result;

            Console.WriteLine("Experiment id:" + id);
            Console.ReadLine();
        }
    }
}
