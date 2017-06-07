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


        public abstract Task<int> SubmitExperiment(NewExperimentViewModel newExperiment, string creator);

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

        public abstract Task<string[]> GetDirectories(string baseDirectory);
    }
}
