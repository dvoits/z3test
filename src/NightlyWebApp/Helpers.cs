using Nightly;
using Nightly.Properties;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace AzurePerformanceTest
{
    public static class Helpers
    {
        public static async Task<string> GetConnectionString()
        {
            if (!String.IsNullOrWhiteSpace(Settings.Default.ConnectionString))
            {
                return Settings.Default.ConnectionString;
            }

            var secretStorage = new SecretStorage(Settings.Default.AADApplicationId, Settings.Default.AADApplicationCertThumbprint, Settings.Default.KeyVaultUrl);
            return await secretStorage.GetSecret(Settings.Default.ConnectionStringSecretId);
        }

        public static async Task<ExperimentsViewModel> GetExperimentsViewModel(string summaryName)
        {
            string connectionString = await Helpers.GetConnectionString();
            IDomainResolver domainResolver = new MEFDomainResolver(System.IO.Path.Combine(HttpRuntime.AppDomainAppPath, "bin"));
            var vm = await ExperimentsViewModel.Initialize(connectionString, summaryName, domainResolver);
            return vm;
        }
    }
}
