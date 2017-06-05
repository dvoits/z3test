using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public abstract class ExperimentManagerViewModel
    {
        protected readonly ExperimentManager manager;
        protected readonly IUIService uiService;
        protected readonly IDomainResolver domainResolver;

        public ExperimentManagerViewModel(ExperimentManager manager, IUIService uiService, IDomainResolver domainResolver)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            this.manager = manager;
            if (uiService == null) throw new ArgumentNullException("uiService");
            this.uiService = uiService;
            if (domainResolver == null) throw new ArgumentNullException("domainResolver");
            this.domainResolver = domainResolver;
        }

        public virtual string BenchmarkLibraryDescription
        {
            get { return null; }
        }
        public abstract Task<string[]> GetAvailableCategories(string benchmarkContainer);


        public virtual async Task SubmitExperiment(ExperimentDefinition def, string creator, string note)
        {
            var id = await manager.StartExperiment(def, creator, note);
        }

        public abstract string HandleMultileTargetFiles(string[] files, string mainFile);

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
        public void SaveMetaData (string filename, ExperimentStatusViewModel[] experiments)
        {
            SaveData.SaveMetaCSV(filename, experiments, manager, domainResolver, uiService);
        }
        public void SaveCSVData(string filename, ExperimentStatusViewModel[] experiments)
        {
            SaveData.SaveCSV(filename, experiments, manager, uiService);
        }
        public void SaveBinary(string filename, ExperimentStatusViewModel experiment)
        {
            SaveData.SaveBinary(filename, experiment, manager, uiService);
        }
        public void SaveMatrix(string filename, ExperimentStatusViewModel[] experiments)
        {
            SaveData.SaveMatrix(filename, experiments, manager, uiService);
        }
        public void SaveOutput(string selectedPath, ExperimentStatusViewModel experiment)
        {
            SaveData.SaveOutput(selectedPath, experiment, manager, uiService);
        }
        public abstract Task<string[]> GetDirectories(string baseDirectory);
    }

    public class LocalExperimentManagerViewModel : ExperimentManagerViewModel
    {
        public LocalExperimentManagerViewModel(LocalExperimentManager manager, IUIService uiService, IDomainResolver domainResolver) : base(manager, uiService, domainResolver)
        {
        }

        public override string BenchmarkLibraryDescription
        {
            get { return "A folder that contains benchmark files."; }
        }

        public override Task<string[]> GetAvailableCategories(string benchmarkContainer)
        {
            if (Directory.Exists(benchmarkContainer))
            {
                var sep = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
                return Task.FromResult(Directory.EnumerateDirectories(benchmarkContainer).Select(dir => {
                    int i = dir.LastIndexOfAny(sep);
                    return i >= 0 ? dir.Substring(i + 1) : dir;
                }).ToArray());
            }
            return Task.FromResult(new string[0]);
        }

        public override string HandleMultileTargetFiles(string[] files, string mainFile)
        {
            return mainFile;
        }

        public override Task<string[]> GetDirectories(string baseDirectory)
        {
            throw new NotImplementedException();
        }
    }
}
