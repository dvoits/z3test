﻿using System;
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
        protected readonly UIService uiService;
        protected readonly IDomainResolver domainResolver;

        public ExperimentManagerViewModel(ExperimentManager manager, UIService uiService, IDomainResolver domainResolver)
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
        public abstract string[] GetAvailableCategories(string benchmarkContainer);


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
            return new ShowResultsViewModel(id, this.BenchmarkLibraryDescription, manager, uiService);
        }
        public CompareExperimentsViewModel BuildComparingResults(int id1, int id2)
        {
            return new CompareExperimentsViewModel(id1, id2, manager, uiService);
        }
        public Task<ExperimentPropertiesViewModel> BuildProperties(int id)
        {
            return ExperimentPropertiesViewModel.CreateAsync(manager, id, domainResolver, uiService);
        }
    }

    public class LocalExperimentManagerViewModel : ExperimentManagerViewModel
    {
        public LocalExperimentManagerViewModel(LocalExperimentManager manager, UIService uiService, IDomainResolver domainResolver) : base(manager, uiService, domainResolver)
        {
        }

        public override string BenchmarkLibraryDescription
        {
            get { return "A folder that contains benchmark files."; }
        }

        public override string[] GetAvailableCategories(string benchmarkContainer)
        {
            if (Directory.Exists(benchmarkContainer))
            {
                var sep = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
                return Directory.EnumerateDirectories(benchmarkContainer).Select(dir => {
                    int i = dir.LastIndexOfAny(sep);
                    return i >= 0 ? dir.Substring(i + 1) : dir;
                }).ToArray();
            }
            return new string[0];
        }

        public override string HandleMultileTargetFiles(string[] files, string mainFile)
        {
            return mainFile;
        }
    }
}
