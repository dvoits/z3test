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
    public class NewExperimentViewModel : INotifyPropertyChanged
    {
        private readonly ExperimentManagerViewModel manager;
        private readonly IUIService service;
        private readonly RecentValuesStorage recentValues;

        private string benchmarkContainerUri;
        private string benchmarkDirectory;
        private string categories;
        private string domain;
        private double memlimit;
        private double timelimit;
        private string parameters;
        private string extension;
        private string note;

        private bool useMostRecentExecutable;
        private string[] fileNames;

        public event PropertyChangedEventHandler PropertyChanged;


        public NewExperimentViewModel(ExperimentManagerViewModel manager, IUIService service, RecentValuesStorage recentValues)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (service == null) throw new ArgumentNullException("service");
            if (recentValues == null) throw new ArgumentNullException("recentValues");
            this.manager = manager;
            this.service = service;
            this.recentValues = recentValues;

            domain = "Z3";
            benchmarkContainerUri = ExperimentDefinition.DefaultContainerUri;

            ChooseDirectoryCommand = new DelegateCommand(ChooseDirectory);
            ChooseCategoriesCommand = new DelegateCommand(ChooseCategories);
            ChooseExecutableCommand = new DelegateCommand(ChooseExecutable);

            benchmarkDirectory = recentValues.BenchmarkDirectory;
            categories = recentValues.BenchmarkCategories;
            extension = recentValues.BenchmarkExtension;
            parameters = recentValues.ExperimentExecutableParameters;
            timelimit = recentValues.BenchmarkTimeLimit.TotalSeconds;
            memlimit = recentValues.BenchmarkMemoryLimit;
            note = recentValues.ExperimentNote;

            UseMostRecentExecutable = true;
        }

        public string BenchmarkLibaryDescription
        {
            get { return manager.BenchmarkLibraryDescription; }
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

        public string BenchmarkDirectory
        {
            get { return benchmarkDirectory; }
            set
            {
                if (benchmarkDirectory == value) return;
                benchmarkDirectory = value;
                NotifyPropertyChanged();
                Categories = "";
            }
        }

        public string Categories
        {
            get { return categories; }
            set
            {
                categories = value;
                NotifyPropertyChanged();
            }
        }

        public string Domain
        {
            get { return domain; }
            set
            {
                if (domain == value) return;
                domain = value;
                NotifyPropertyChanged();
            }
        }

        public string[] Domains
        {
            get { return new[] { "Z3", "default" }; }
        }

        public string MainExecutable
        {
            get { return fileNames != null && fileNames.Length > 0 ? fileNames[0] : string.Empty; }
        }

        public bool UseMostRecentExecutable
        {
            get { return useMostRecentExecutable; }
            set
            {
                if (useMostRecentExecutable == value) return;
                useMostRecentExecutable = value;
                NotifyPropertyChanged("UseMostRecentExecutable");
                NotifyPropertyChanged("UseNewExecutable");
            }
        }

        public bool UseNewExecutable
        {
            get { return !useMostRecentExecutable; }
            set
            {
                if (useMostRecentExecutable == !value) return;
                useMostRecentExecutable = !value;
                NotifyPropertyChanged("UseMostRecentExecutable");
                NotifyPropertyChanged("UseNewExecutable");
            }
        }

        public string[] ExecutableFileNames
        {
            get { return fileNames; }
        }

        public string Parameters
        {
            get { return parameters; }
            set
            {
                parameters = value;
                NotifyPropertyChanged();
            }
        }

        public double BenchmarkTimeoutSec
        {
            get { return timelimit; }
            set
            {
                timelimit = value;
                NotifyPropertyChanged();
            }
        }

        public double BenchmarkMemoryLimitMb
        {
            get { return memlimit; }
            set
            {
                memlimit = value;
                NotifyPropertyChanged();
            }
        }

        public string Extension
        {
            get { return extension; }
            set
            {
                extension = value;
                NotifyPropertyChanged();
            }
        }

        public string Note
        {
            get { return note; }
            set
            {
                note = value;
                NotifyPropertyChanged();
            }
        }


        public ICommand ChooseDirectoryCommand
        {
            get; private set;
        }

        public ICommand ChooseCategoriesCommand
        {
            get; private set;
        }

        public ICommand ChooseExecutableCommand
        {
            get; private set;
        }

        public void SaveRecentSettings()
        {
            recentValues.BenchmarkDirectory = benchmarkDirectory;
            recentValues.BenchmarkCategories = categories;
            recentValues.BenchmarkExtension = extension;
            recentValues.ExperimentExecutableParameters = parameters;
            recentValues.BenchmarkTimeLimit = TimeSpan.FromSeconds(timelimit);
            recentValues.BenchmarkMemoryLimit = memlimit;
            recentValues.ExperimentNote = note;
        }

        private void ChooseExecutable()
        {
            string[] files = service.ChooseFiles(null, "Executable files (*.exe;*.dll)|*.exe;*.dll|All Files (*.*)|*.*", "exe");
            if (files == null || files.Length == 0) return;

            if (files.Length > 1)
            {
                string[] exeFiles = files.Where(f => f.EndsWith(".exe")).ToArray();
                if (exeFiles.Length == 0)
                {
                    service.ShowError("No executable files have been chosen.", "New experiment");
                    return;
                }
                string mainFile = null;
                if (exeFiles.Length == 1)
                    mainFile = exeFiles[0];
                else
                {
                    mainFile = service.ChooseOption("Select main executable", exeFiles, exeFiles[0]);
                    if (mainFile == null) return;                    
                }

                // First element of the file names array must be main executable
                int i = Array.IndexOf(files, mainFile);
                if (i < 0) throw new InvalidOperationException("The chosen main executable is not found in the original file list");
                files[i] = files[0];
                files[0] = mainFile;
            }
            fileNames = files;
            NotifyPropertyChanged("MainExecutable");
            NotifyPropertyChanged("ExecutableFileNames");
            UseMostRecentExecutable = false;
        }

        private async void ChooseDirectory()
        {
            try
            {
                string[] initial = BenchmarkDirectory == null ? new string[0] : BenchmarkDirectory.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var selected = await service.BrowseTree("Browse for directory", initial, selection =>
                {
                    return manager.GetDirectories(string.Join("/", selection));
                });
                if (selected != null)
                {
                    BenchmarkDirectory = string.Join("/", selected);
                }
            }
            catch (Exception ex)
            {
                service.ShowError(ex);
            }
        }

        private async void ChooseCategories()
        {
            try
            {
                string[] allCategories;
                var handle = service.StartIndicateLongOperation("Loading categories...");
                try
                {
                    allCategories = await manager.GetAvailableCategories(BenchmarkDirectory);
                }
                finally
                {
                    service.StopIndicateLongOperation(handle);
                }

                string[] selected = Categories == null ? new string[0] : Categories.Split(',').Select(s => s.Trim()).ToArray();

                selected = service.ChooseOptions("Choose categories", allCategories, selected);
                if (selected != null)
                {
                    Categories = String.Join(",", selected);
                }
            }
            catch (Exception ex)
            {
                service.ShowError(ex);
            }
        }



        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
