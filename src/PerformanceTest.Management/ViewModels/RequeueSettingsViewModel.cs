using AzurePerformanceTest;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PerformanceTest.Management
{
    public class RequeueSettingsViewModel : INotifyPropertyChanged
    {
        private readonly AzureExperimentManagerViewModel manager;
        private readonly IUIService service; 
        private string benchmarkContainerUri;
        private bool isDefaultBenchmarkContainerUri;
        private string selectedPool;

        public event PropertyChangedEventHandler PropertyChanged;

        public RequeueSettingsViewModel(string benchmarkContainerUri, AzureExperimentManagerViewModel manager, IUIService uiService)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (uiService == null) throw new ArgumentNullException(nameof(uiService));
            this.manager = manager;
            this.service = uiService;
            this.benchmarkContainerUri = benchmarkContainerUri;
            isDefaultBenchmarkContainerUri = benchmarkContainerUri == ExperimentDefinition.DefaultContainerUri;
            ChoosePoolCommand = new DelegateCommand(ListPools);
            //selectedPool = recentValues.BatchPool;
        }

        public bool IsDefaultBenchmarkContainerUri
        {
            get { return isDefaultBenchmarkContainerUri; }
            set
            {
                isDefaultBenchmarkContainerUri = value;
                if (isDefaultBenchmarkContainerUri) BenchmarkContainerUri = ExperimentDefinition.DefaultContainerUri;
                NotifyPropertyChanged();
            }
        }
        public string BenchmarkContainerUri
        {
            get { return benchmarkContainerUri; }
            set
            {
                benchmarkContainerUri = value;
                NotifyPropertyChanged();
            }
        }

        public string Pool
        {
            get { return selectedPool; }
            set
            {
                selectedPool = value;
                NotifyPropertyChanged();
            }
        }

        public ICommand ChoosePoolCommand
        {
            get; private set;
        }

        /// <summary>
        /// Returns true, if validation succeded and the new experiment may be submitted.
        /// Returns false, if validation failed and new experiment shouldn't be submitted.
        /// Validation can interact with the user and modify the values.
        /// </summary>
        /// <returns></returns>
        public bool Validate()
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(Pool))
            {
                isValid = false;
                service.ShowWarning("Azure Batch Pool is not specified", "Validation failed");
            }

            return isValid;
        }

        private void ListPools()
        {
            try
            {
                PoolDescription pool = service.ChooseOption("Choose an Azure Batch Pool",
                    new AsyncLazy<PoolDescription[]>(() => manager.GetAvailablePools()),
                    new Predicate<PoolDescription>(p => p.Id == selectedPool));
                if (pool != null)
                {
                    Pool = pool.Id;
                }
            }
            catch (Exception ex)
            {
                service.ShowError(ex, "Failed to get list of available Azure Batch pools");
            }
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
