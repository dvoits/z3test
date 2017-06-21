using AzurePerformanceTest;
using Measurement;
using PerformanceTest;
using Summary.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Summary
{
    class Program
    {
        static int Main(string[] args)
        {
            if(args.Length != 2)
            {
                Console.WriteLine("Syntax: Summary.exe <ExperimentID> <SummaryName>\n");
                Console.WriteLine("Program computes summary for the given experiment and then either adds or replaces\n" +  
                    "the row corresponding to this experiment in the summary table determined by the <SummaryName>.");
                Console.WriteLine("\nSee the program configuration file for additional settings.");
                return 2;
            }

            try
            {
                int expId = int.Parse(args[0]);
                string summaryName = args[1];

                Run(expId, summaryName).Wait();

                Console.WriteLine("\nDone.");
                return 0;
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine("FAILED: " + ex.ToString());
                return 1;
            }
        }

        private static async Task Run(int id, string summaryName)
        {
            Console.WriteLine("Connecting to Azure experiment storage...");
            string connectionString = await GetConnectionString();
            AzureExperimentManager manager = AzureExperimentManager.Open(connectionString);

            var exp = await manager.Storage.GetExperiment(id);
            IDomainResolver resolveDomain = new MEFDomainResolver();
            Domain domain = resolveDomain.GetDomain(exp.DomainName);

            await UpdateSummary(summaryName, id, domain, manager.Storage);
        }

        private static async Task UpdateSummary(string summaryName, int experimentId, Domain domain, AzureExperimentStorage storage)
        {
            Console.WriteLine("Downloading experiment results...");
            var results = await storage.GetResults(experimentId);

            Console.WriteLine("Building summary for the experiment...");
            var catSummary = ExperimentSummary.Build(results, domain);
            var expSummary = new ExperimentSummaryEntity(experimentId, DateTimeOffset.Now, catSummary);

            Console.WriteLine("Uploading new summary...");
            await storage.AppendOrReplaceSummary(summaryName, experimentId, expSummary);
        }

        private static async Task<string> GetConnectionString()
        {
            if (!String.IsNullOrWhiteSpace(Settings.Default.ConnectionString))
            {
                return Settings.Default.ConnectionString;
            }

            var secretStorage = new SecretStorage(Settings.Default.AADApplicationId, Settings.Default.AADApplicationCertThumbprint, Settings.Default.KeyVaultUrl);
            return await secretStorage.GetSecret(Settings.Default.ConnectionStringSecretId);
        }
    }
}
