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

        public override async Task<int> SubmitExperiment(NewExperimentViewModel newExperiment, string creator)
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
