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
        public ShowResultsViewModel BuildResultsView(int id, double timeout, string directory)
        {
            return new ShowResultsViewModel(id, timeout, directory, manager, uiService);
        }
        public CompareExperimentsViewModel BuildComparingResults(int id1, int id2, ExperimentDefinition def1, ExperimentDefinition def2)
        {
            return new CompareExperimentsViewModel(id1, id2, def1, def2, manager, uiService);
        }
        public Task<ExperimentPropertiesViewModel> BuildProperties(int id)
        {
            return ExperimentPropertiesViewModel.CreateAsync(manager, id, domainResolver, uiService);
        }
        public async void BuildDuplicatesResolverView(int[] ids, bool resolveTimeouts, bool resolveSameTime, bool resolveSlowest, bool resolveInErrors)
        {
            bool zero_duplicates = true;
            var handle = uiService.StartIndicateLongOperation("Resolving duplicates...");
            try
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    int eid = ids[i];
                    var vm = new DuplicatesViewModel(eid, resolveTimeouts, resolveSameTime, resolveSlowest, resolveInErrors, manager, uiService);
                    bool hadDuplicates = await vm.DownloadResultsAsync();
                    if (hadDuplicates) zero_duplicates = false;
                }

                if (zero_duplicates)
                {
                    uiService.ShowInfo("There are no duplicates to resolve.", "No duplicates");
                }
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex, "Failed to resolve duplicates in experiment");
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }

        }
        public async void RequeueIErrors(ExperimentStatusViewModel[] ids)
        {
            var handle = uiService.StartIndicateLongOperation("Requeue infrastructure errors...");
            int requeueCount = 0;
            try
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    int eid = ids[i].ID;
                    var results = await manager.GetResults(eid);
                    var ieResults = results.Where(r => r.Status == Measurement.ResultStatus.InfrastructureError).Select(r => r.BenchmarkFileName).Distinct();
                    if (ieResults.Count() > 0)
                    {
                        string benchmarkCont = ids[i].Definition.BenchmarkContainerUri;
                        await manager.RestartBenchmarks(eid, ieResults, benchmarkCont);
                        requeueCount += ieResults.Count();
                    }
                }
                uiService.ShowInfo("Requeued " + requeueCount + " infrastructure errors.", "Infrastructure errors");
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex, "Failed to requeue infrastructure errors");
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
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

        public async Task<Tuple<string, int?, Exception>[]> SubmitExperiments(NewExperimentViewModel newExperiment)
        {
            // Uploading package with binaries
            string creator = newExperiment.Creator;
            string packageName;            
            if (newExperiment.UseMostRecentExecutable)
            {
                packageName = await newExperiment.GetRecentExecutable();
                if (String.IsNullOrEmpty(packageName)) throw new InvalidOperationException("Executable package is not available");
            }
            else // upload new executable
            {
                if (newExperiment.ExecutableFileNames == null || newExperiment.ExecutableFileNames.Length == 0)
                    throw new InvalidOperationException("New executable should be uploaded but no files selected");

                if (newExperiment.ExecutableFileNames.Length == 1) // single file will be uploaded as is
                {
                    string fileName = Path.GetFileName(newExperiment.ExecutableFileNames[0]);                    
                    using (Stream stream = File.Open(newExperiment.ExecutableFileNames[0], FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        packageName = await manager.Storage.UploadNewExecutable(stream, fileName, creator);
                    }
                }
                else // multiple files are packed into zip
                {
                    if (String.IsNullOrEmpty(newExperiment.MainExecutable))
                        throw new InvalidOperationException("There is no main executable selected");

                    string fileName = Path.GetFileNameWithoutExtension(newExperiment.MainExecutable) + ".zip";
                    using (MemoryStream stream = new MemoryStream(20 << 20))
                    {
                        Measurement.ExecutablePackage.PackToStream(newExperiment.ExecutableFileNames, newExperiment.MainExecutable, stream);
                        packageName = await manager.Storage.UploadNewExecutable(stream, fileName, creator);
                    }
                }
            }

            int maxRepetitions = 1;
            double maxTime = 0;
            if (newExperiment.AllowAdaptiveRuns)
            {
                maxRepetitions = newExperiment.MaxRepetitions;
                maxTime = newExperiment.MaxTimeForAdaptiveRuns;
            }

            // Submitting experiments
            string[] cats = newExperiment.Categories.Split('|');
            var res = new Tuple<string, int?, Exception>[cats.Length];

            for (int i = 0; i < cats.Length; i++)
            {
                string category = cats[i].Trim();
                ExperimentDefinition def =
                ExperimentDefinition.Create(
                    packageName, newExperiment.BenchmarkContainerUri, newExperiment.BenchmarkDirectory, newExperiment.Extension, newExperiment.Parameters,
                    TimeSpan.FromSeconds(newExperiment.BenchmarkTimeoutSec), newExperiment.Domain,
                    category, newExperiment.BenchmarkMemoryLimitMb, maxRepetitions, maxTime);

                try
                {
                    // Starts the experiment job
                    manager.BatchPoolID = newExperiment.Pool;
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


        public Task<Stream> SaveExecutable(string filename, string exBlobName)
        {
            if (filename == null) throw new ArgumentNullException("filename");
            if (exBlobName == null) throw new ArgumentNullException("exBlobName");
            return Task.Run(() => manager.Storage.DownloadExecutable(exBlobName));
        }

        public Task<Tuple<string, DateTimeOffset?>> GetRecentExecutable(string creator)
        {
            return Task.Run(() => manager.Storage.TryFindRecentExecutableBlob(creator));
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
