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


        public event PropertyChangedEventHandler PropertyChanged;


        public NewExperimentViewModel(ExperimentManagerViewModel manager, IUIService service)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (service == null) throw new ArgumentNullException("service");
            this.manager = manager;
            this.service = service;

            ChooseContainerCommand = new DelegateCommand(ChooseBenchmarkContainer);
            ChooseCategoriesCommand = new DelegateCommand(ChooseCategories);
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


        public ICommand ChooseContainerCommand
        {
            get; private set;
        }

        public ICommand ChooseCategoriesCommand
        {
            get; private set;
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

            selected = service.ChooseCategories(allCategories, selected);
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
