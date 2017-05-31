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

        public override string[] GetAvailableCategories(string benchmarkContainer)
        {
            throw new NotImplementedException();
        }

        public override string HandleMultileTargetFiles(string[] files, string mainFile)
        {
            throw new NotImplementedException();
        }
    }
}
