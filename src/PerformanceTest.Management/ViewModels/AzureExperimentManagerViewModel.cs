using AzurePerformanceTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public class AzureExperimentManagerViewModel 
    {
        protected readonly AzureExperimentManager manager;
        protected readonly IUIService uiService;
        protected readonly IDomainResolver domainResolver;

        public AzureExperimentManagerViewModel(AzureExperimentManager manager, IUIService uiService, IDomainResolver domainResolver)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            this.manager = manager;
            if (uiService == null) throw new ArgumentNullException("uiService");
            this.uiService = uiService;
            if (domainResolver == null) throw new ArgumentNullException("domainResolver");
            this.domainResolver = domainResolver;
        }


        public string BenchmarkLibraryDescription
        {
            get { return "Microsoft Azure blob container that contains benchmark files"; }
        }

        public ExperimentListViewModel BuildListView()
        {
            return new ExperimentListViewModel(manager, uiService);
        }
        public ShowResultsViewModel BuildResultsView(int id, string directory)
        {
            return new ShowResultsViewModel(id, directory, manager, uiService);
        }
        public CompareExperimentsViewModel BuildComparingResults(int id1, int id2, ExperimentDefinition def1, ExperimentDefinition def2)
        {
            return new CompareExperimentsViewModel(id1, id2, def1, def2, manager, uiService);
        }
        public Task<ExperimentPropertiesViewModel> BuildProperties(int id)
        {
            return ExperimentPropertiesViewModel.CreateAsync(manager, id, domainResolver, uiService);
        }

        public async Task<string[]> GetAvailableCategories(string directory)
        {
            if (directory == null) throw new ArgumentNullException("directory");
            string[] cats = await GetDirectories(directory);
            return cats;
        }

        public Task<string[]> GetDirectories(string baseDirectory = "")
        {
            if (baseDirectory == null) throw new ArgumentNullException("baseDirectory");
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

        public async Task<int> SubmitExperiment(NewExperimentViewModel newExperiment, string creator)
        {
            AzureExperimentManager azureManager = (AzureExperimentManager)manager;
            string packageName;

            if (newExperiment.UseMostRecentExecutable)
                throw new NotImplementedException();
            else // upload new executable
            {
                using (MemoryStream stream = new MemoryStream(20 << 20))
                {
                    Measurement.ExecutablePackage.PackToStream(newExperiment.ExecutableFileNames, newExperiment.MainExecutable, stream);

                    string extension = Path.GetExtension(newExperiment.MainExecutable);
                    string fileName = Path.GetFileNameWithoutExtension(newExperiment.MainExecutable);

                    do
                    {
                        stream.Position = 0;
                        packageName = string.Format("{0}.{1:yyyy-MM-ddTHH-mm-ss-ffff}.zip", fileName, DateTime.UtcNow);
                    } while (!await azureManager.Storage.TryUploadNewExecutable(stream, packageName));
                }
            }

            ExperimentDefinition def =
                ExperimentDefinition.Create(
                    packageName, newExperiment.BenchmarkContainerUri, newExperiment.BenchmarkDirectory, newExperiment.Extension, newExperiment.Parameters,
                    TimeSpan.FromSeconds(newExperiment.BenchmarkTimeoutSec), newExperiment.Domain,
                    newExperiment.Categories, newExperiment.BenchmarkMemoryLimitMb);


            return await manager.StartExperiment(def, creator, newExperiment.Note);
        }
    }
}
