﻿using AzurePerformanceTest;
using AzureWorker;
using Measurement;
using PerformanceTest;
using PerformanceTest.Records;
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
                Console.WriteLine("Program computes summary and records for the given experiment and then either adds or replaces\n" +  
                    "the row corresponding to this experiment in the summary table determined by the <SummaryName>.");
                Console.WriteLine("\nSee the program configuration file for additional settings.");
                return 2;
            }

            try
            {
                int expId = int.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture);
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
            Console.WriteLine("Connecting to Azure...");
            string connectionString = "DefaultEndpointsProtocol=https;AccountName=cz3test2;AccountKey=ncF/HfD7tMkmHo3xr9+mtlFg8HXPTH90B9RNH2I3Fk6p36AouBYFMbAUonDe8u6vArTq2Lxh88j0kQBKXd18EA==;BatchAccessKey=BwvyPY/PVCS78uADX/egSxOTMuUlFBCSyxGkgjK00A/it8k1+kPg39tNCyq4gJ5POxf71wiWmtlytbjEfKc4KQ==;BatchURL=https://cz3.westeurope.batch.azure.com;BatchAccount=cz3;";//await GetConnectionString(); 
            var manager = new AzureSummaryManager(connectionString, MEFDomainResolver.Instance);

            // debug: var stsum = await manager.GetStatusSummary(184, 158);
            var result = await manager.Update(summaryName, id);
            //string rec = Settings.Default.ReportRecipients;
            //string linkPage = Settings.Default.LinkPage;
            //SendMail sendMail = new SendMail();
            //await sendMail.SendReport(manager, result[0], result[1], rec, linkPage);
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
