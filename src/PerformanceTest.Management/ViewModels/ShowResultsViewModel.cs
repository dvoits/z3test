using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public class ShowResultsViewModel : INotifyPropertyChanged
    {

        private IEnumerable<ExperimentResultViewModel> results;
        private readonly int id;
        private readonly ExperimentManager manager;
        private readonly IUIService message;
        private readonly string sharedDirectory;
        public event PropertyChangedEventHandler PropertyChanged;
        public ShowResultsViewModel(int id, string sharedDirectory, ExperimentManager manager, IUIService message)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.manager = manager;
            this.message = message;
            this.id = id;
            this.sharedDirectory = sharedDirectory;
            RefreshResultsAsync();
        }
        private async void RefreshResultsAsync()
        {
            Results = null;

            var res = await manager.GetResults(id);
            Results = res.Select(e => new ExperimentResultViewModel(e)).ToArray();
        }

        public IEnumerable<ExperimentResultViewModel> Results
        {
            get { return results; }
            set { results = value; NotifyPropertyChanged(); }
        }
        public string Title
        {
            get { return "Experiment " + id.ToString(); }
        }
        public string Directory
        {
            get { return sharedDirectory; }
        }

        public async void FilterResultsByError(int code)
        {
            var res = await manager.GetResults(id);
            var resVm = res.Select(e => new ExperimentResultViewModel(e)).ToArray();
            if (code == 0) Results = resVm.Where(e => e.ResultCode == 0 && e.SAT > 0).ToArray();
            else if (code == 1) Results = resVm.Where(e => e.ResultCode == 0 && e.UNSAT > 0).ToArray();
            else if (code == 2) Results = resVm.Where(e => e.ResultCode == 0 && e.UNKNOWN > 0).ToArray();
            else if (code == 3) Results = resVm.Where(e => e.ResultCode == 3).ToArray();
            else if (code == 4) Results = resVm.Where(e => e.ResultCode == 4).ToArray();
            else if (code == 5) Results = resVm.Where(e => e.ResultCode == 5).ToArray();
            else if (code == 6) Results = resVm.Where(e => e.ResultCode == 6).ToArray();
            else RefreshResultsAsync();
        }
        public async void FilterResultsByText(string filter, int code)
        {
            //code == 0 - only filename
            //code == 1 - output
            var res = await manager.GetResults(id);
            var resVm = res.Select(e => new ExperimentResultViewModel(e)).ToArray();
            if (filter != "") {
                if (code == 0)
                {
                    if (filter == "sat")
                    {
                        resVm = resVm.Where(e => Regex.IsMatch(e.Filename, "/^(?:(?!unsat).)*$/")).ToArray();
                    }
                    Results = resVm.Where(e => e.Filename.Contains(filter)).ToArray();
                }
                if (code == 1)
                {
                    Results = resVm.Where(e => e.StdOut.Contains(filter) || e.StdErr.Contains(filter)).ToArray();
                }
            } 
            else RefreshResultsAsync();
        }
        public async void FilterResultsByRuntime(int limit)
        {
            var res = await manager.GetResults(id);
            var resVm = res.Select(e => new ExperimentResultViewModel(e)).ToArray();
            Results = resVm.Where(e => e.Runtime >= limit).ToArray();
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class ExperimentResultViewModel
    {
        private BenchmarkResult result;
        public event PropertyChangedEventHandler PropertyChanged;
        public ExperimentResultViewModel (BenchmarkResult res)
        {
            this.result = res;
        }
        public int ID
        {
            get { return result.ExperimentID; }
        }
        public string Filename
        {
            get { return result.BenchmarkFileName; }
        }
        public int Returnvalue
        {
            get { return 0; }
        }
        public int ResultCode
        {
            get { return 0; }
        }
        public int SAT
        {
            get { return 0; }
        }
        public int UNSAT
        {
            get { return 0; }
        }
        public int UNKNOWN
        {
            get { return 0; }
        }
        public double Runtime
        {
            get { return result.NormalizedRuntime; }
        }
        public string Worker
        {
            get { return result.WorkerInformation; }
        }
        public string StdOut
        {
            get { return "standard output on double click"; }
        }
        public string StdErr
        {
            get { return "error output on double click"; }
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
