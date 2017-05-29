using AzurePerformanceTest;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace LaunchExperiment
{
    class Program
    {
        static void Main(string[] args)
        {
            Keys keys = JsonConvert.DeserializeObject<Keys>(File.ReadAllText("..\\..\\keys.json"));
            var manager = AzureExperimentManager.Open(new AzureExperimentStorage(keys.storageName, keys.storageKey), keys.batchUri, keys.batchName, keys.batchKey);

            var id = manager.StartExperiment(ExperimentDefinition.Create("z3.zip", "input", "smt2", "model_validate=true -smt2 -file:{0}", TimeSpan.FromSeconds(1200), "", null, 2048), "Dmitry K", "test").Result;

            Console.WriteLine("Experiment id:" + id);

            Console.ReadLine();
        }
    }

    struct Keys
    {
        public string storageName { get; set; }
        public string storageKey { get; set; }
        public string batchUri { get; set; }
        public string batchName { get; set; }
        public string batchKey { get; set; }
    }
}
