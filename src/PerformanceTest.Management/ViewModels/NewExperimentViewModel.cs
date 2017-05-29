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

        private string benchmarkLibrary;
        private string categories;
        private bool useMostRecentExecutable;
        private string executable;
        private string domain;

        public event PropertyChangedEventHandler PropertyChanged;


        public NewExperimentViewModel(ExperimentManagerViewModel manager, IUIService service)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (service == null) throw new ArgumentNullException("service");
            this.manager = manager;
            this.service = service;

            domain = "Z3";
            UseMostRecentExecutable = true;

            ChooseContainerCommand = new DelegateCommand(ChooseBenchmarkContainer);
            ChooseCategoriesCommand = new DelegateCommand(ChooseCategories);
            ChooseExecutableCommand = new DelegateCommand(ChooseExecutable);
        }

        public string BenchmarkLibaryDescription
        {
            get { return manager.BenchmarkLibraryDescription; }
        }

        public string BenchmarkLibrary
        {
            get { return benchmarkLibrary; }
            set {
                benchmarkLibrary = value;
                NotifyPropertyChanged();
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

        public string Executable
        {
            get { return executable; }
            set
            {
                if (executable == value) return;
                executable = value;
                NotifyPropertyChanged();
            }
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

        public string Parameters
        {
            get; set;
        }

        public double BenchmarkTimeoutSec
        {
            get; set;
        }

        public int BenchmarkMemoryLimitMb
        {
            get; set;
        }

        public string Extension { get; set; }

        public string Note
        {
            get; set;
        }


        public ICommand ChooseContainerCommand
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


        private void ChooseExecutable()
        {
            string[] files = service.ChooseFiles(Executable, "Executable files (*.exe;*.dll)|*.exe;*.dll|All Files (*.*)|*.*", "exe");
            if (files == null || files.Length == 0) return;

            if(files.Length == 1)
            {
                Executable = files[0];
            }else
            {
                string[] exeFiles = files.Where(f => f.EndsWith(".exe")).ToArray();
                if(exeFiles.Length == 0)
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

                    mainFile = manager.HandleMultileTargetFiles(files, mainFile);
                }
                Executable = mainFile;
            }
            UseMostRecentExecutable = false;
        }

        private void ChooseBenchmarkContainer()
        {
            string folder = service.ChooseFolder(BenchmarkLibrary, manager.BenchmarkLibraryDescription);
            if (folder != null)
            {
                BenchmarkLibrary = folder;
            }
        }

        private void ChooseCategories()
        {
            string[] allCategories = manager.GetAvailableCategories(BenchmarkLibrary);
            string[] selected = Categories == null ? new string[0] : Categories.Split(',').Select(s => s.Trim()).ToArray();

            selected = service.ChooseOptions("Choose Categories", allCategories, selected);
            if (selected != null)
            {
                Categories = String.Join(",", selected);
            }
        }



        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
