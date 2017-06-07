using AzurePerformanceTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public class AzureExperimentManagerViewModel : ExperimentManagerViewModel
    {
        public AzureExperimentManagerViewModel(AzureExperimentManager manager, IUIService uiService, IDomainResolver domainResolver) : base(manager, uiService, domainResolver)
        {
        }

        public override string BenchmarkLibraryDescription
        {
            get { return "Microsoft Azure blob container that contains benchmark files"; }
        }

        public override async Task<string[]> GetAvailableCategories(string directory)
        {
            if (directory == null) throw new ArgumentNullException("directory");
            string[] cats = await GetDirectories(directory);
            return cats;
        }

        public override Task<string[]> GetDirectories(string baseDirectory = "")
        {
            if (baseDirectory == null) throw new ArgumentNullException("baseDirectory");
            AzureExperimentManager manager = (AzureExperimentManager)base.manager;
            var expStorage = manager.Storage;
            var benchStorage = expStorage.DefaultBenchmarkStorage;

            return Task.Run(() =>
            {
                string[] dirs;
                try
                {
                    dirs = benchStorage.ListDirectories(baseDirectory).ToArray();
                    Array.Sort<string>(dirs);
                }
                catch (Exception ex)
                {
                    uiService.ShowError(ex.Message, "Failed to list directories");
                    dirs = new string[0];
                }
                return dirs;
            });
        }

        public override string HandleMultileTargetFiles(string[] files, string mainFile)
        {
            throw new NotImplementedException();
        }
        public override Task<Stream> SaveExecutable(string filename, string exBlobName)
        {
            if (filename == null) throw new ArgumentNullException("filename");
            if (exBlobName == null) throw new ArgumentNullException("exBlobName");
            AzureExperimentManager manager = (AzureExperimentManager)base.manager;
            var result = Task.Run(() => manager.Storage.DownloadExecutable(exBlobName));
            return result;
        }
    }
}
