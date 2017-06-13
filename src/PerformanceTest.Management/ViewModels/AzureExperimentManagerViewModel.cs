﻿using AzurePerformanceTest;
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

        public async Task<Tuple<string, int?, Exception>[]> SubmitExperiments(NewExperimentViewModel newExperiment, string creator)
        {
            // Uploading package with binaries
            string packageName;
            if (newExperiment.UseMostRecentExecutable)
            {
                packageName = await newExperiment.GetRecentExecutable();
                if (String.IsNullOrEmpty(packageName)) throw new InvalidOperationException("Executable package is not available");
            }
            else // upload new executable
            {
                using (MemoryStream stream = new MemoryStream(20 << 20))
                {
                    Measurement.ExecutablePackage.PackToStream(newExperiment.ExecutableFileNames, newExperiment.MainExecutable, stream);

                    string fileName = Path.GetFileNameWithoutExtension(newExperiment.MainExecutable);
                    string esc_creator = ToBinaryPackBlobName(creator);

                    do
                    {
                        stream.Position = 0;
                        packageName = string.Format("{0}.{1}.{2:yyyy-MM-ddTHH-mm-ss-ffff}.zip", esc_creator, fileName, DateTime.UtcNow);
                    } while (!await manager.Storage.TryUploadNewExecutable(stream, packageName, creator));
                }
            }

            // Submitting experiments
            string[] cats = newExperiment.Categories.Split(',');
            var res = new Tuple<string, int?, Exception>[cats.Length];

            for (int i = 0; i < cats.Length; i++)
            {
                string category = cats[i].Trim();
                ExperimentDefinition def =
                ExperimentDefinition.Create(
                    packageName, newExperiment.BenchmarkContainerUri, newExperiment.BenchmarkDirectory, newExperiment.Extension, newExperiment.Parameters,
                    TimeSpan.FromSeconds(newExperiment.BenchmarkTimeoutSec), newExperiment.Domain,
                    category, newExperiment.BenchmarkMemoryLimitMb);

                try
                {
                    int id = await manager.StartExperiment(def, creator, newExperiment.Note);
                    res[i] = Tuple.Create<string, int?, Exception>(category, id, null);
                }
                catch (Exception ex)
                {
                    res[i] = Tuple.Create<string, int?, Exception>(category, null, ex);
                }
            }
            return res;
        }


        private static string invalidChars = System.Text.RegularExpressions.Regex.Escape("." + new string(Path.GetInvalidFileNameChars()));
        private static string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

        private static string ToBinaryPackBlobName(string name)
        {
            string name2 = System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
            if (name2.Length > 1024) name2 = name2.Substring(0, 1024);
            else if (name2.Length == 0) name2 = "_";
            return name2;
        }


        public Task<Stream> SaveExecutable(string filename, string exBlobName)
        {
            if (filename == null) throw new ArgumentNullException("filename");
            if (exBlobName == null) throw new ArgumentNullException("exBlobName");
            return Task.Run(() => manager.Storage.DownloadExecutable(exBlobName));
        }

        public Task<Tuple<string, DateTimeOffset?>> GetRecentExecutable(string creator)
        {
            return Task.Run(() => manager.Storage.TryFindRecentExecutableBlob(ToBinaryPackBlobName(creator), creator));
        }

        public void SaveMetaData(string filename, ExperimentStatusViewModel[] experiments)
        {
            SaveData.SaveMetaCSV(filename, experiments, manager, domainResolver, uiService);
        }
        public void SaveCSVData(string filename, ExperimentStatusViewModel[] experiments)
        {
            SaveData.SaveCSV(filename, experiments, manager, uiService);
        }
        public void SaveMatrix(string filename, ExperimentStatusViewModel[] experiments)
        {
            SaveData.SaveMatrix(filename, experiments, manager, uiService);
        }
        public void SaveOutput(string selectedPath, ExperimentStatusViewModel experiment)
        {
            SaveData.SaveOutput(selectedPath, experiment, manager, uiService);
        }

        public Task<PoolDescription[]> GetAvailablePools()
        {
            return manager.GetAvailablePools();
        }
    }
}
