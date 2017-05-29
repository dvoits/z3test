using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PerformanceTest.Management
{
    public class ShowResultsViewModel : INotifyPropertyChanged
    {
        private IEnumerable<BenchmarkResultViewModel> results, allResults;
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
            allResults = Results = null;
            var res = await manager.GetResults(id);
            allResults = Results = res.Select(e => new BenchmarkResultViewModel(e)).ToArray();
        }
        public IEnumerable<BenchmarkResultViewModel> Results
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

        public void FilterResultsByError(int code)
        {
            if (code == 0) Results = allResults.Where(e => e.Status == "Success" && e.Sat > 0).ToArray();
            else if (code == 1) Results = allResults.Where(e => e.Status == "Success" && e.Unsat > 0).ToArray();
            else if (code == 2) Results = allResults.Where(e => e.Status == "Success" && e.Unknown > 0).ToArray();
            else if (code == 3) Results = allResults.Where(e => e.Status == "Bug").ToArray();
            else if (code == 4) Results = allResults.Where(e => e.Status == "Error").ToArray();
            else if (code == 5) Results = allResults.Where(e => e.Status == "Timeout").ToArray();
            else if (code == 6) Results = allResults.Where(e => e.Status == "OutOfMemory").ToArray();
            else if (code == 7) Results = allResults.Where(e => e.Status == "Success" && e.Sat + e.Unsat > e.TargetSat + e.TargetUnsat && e.Unknown < e.TargetUnknown).ToArray();
            else if (code == 8) Results = allResults.Where(e => e.Sat + e.Unsat < e.Sat + e.Unsat || e.Unknown > e.TargetUnknown).ToArray();
            else Results = allResults;
        }
        public void FilterResultsByText(string filter, int code)
        {
            //code == 0 - only filename
            //code == 1 - output
            if (filter != "")
            {
                if (code == 0)
                {
                    var resVm = allResults;
                    if (filter == "sat")
                    {
                        resVm = allResults.Where(e => Regex.IsMatch(e.Filename, "/^(?:(?!unsat).)*$/")).ToArray();
                    }
                    Results = resVm.Where(e => e.Filename.Contains(filter)).ToArray();
                }
                if (code == 1)
                {
                    Results = allResults.Where(e => e.StdOut.Contains(filter) || e.StdErr.Contains(filter)).ToArray();
                }
            }
            else Results = allResults;
        }
        public void FilterResultsByRuntime(int limit)
        {
            Results = allResults.Where(e => e.Runtime >= limit).ToArray();
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class BenchmarkResultViewModel
    {
        private BenchmarkResult result;
        public event PropertyChangedEventHandler PropertyChanged;
        public BenchmarkResultViewModel (BenchmarkResult res)
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
        public int Exitcode
        {
            get { return result.ExitCode; }
        }
        public string Status
        {
            get { return result.Status.ToString(); }
            set {
                result.updateStatus(value);
                NotifyPropertyChanged("Results");
            }
        }
        private int GetProperty (string prop)
        {
            int res = result.Properties.ContainsKey(prop) ? Int32.Parse(result.Properties[prop]) : 0;
            return res;
        }
        public int Sat
        {
            get { return GetProperty("SAT"); }
        }
        public int Unsat
        {
            get { return GetProperty("UNSAT"); }
        }
        public int Unknown
        {
            get { return GetProperty("UNKNOWN"); }
        }
        public int TargetSat
        {
            get { return GetProperty("TargetSAT"); }
        }
        public int TargetUnsat
        {
            get { return GetProperty("TargetUNSAT"); }
        }
        public int TargetUnknown
        {
            get { return GetProperty("TargetUNKNOWN"); }
        }
        public double Runtime
        {
            get { return result.NormalizedRuntime; }
            set {
                result.updateRuntime(value);
                NotifyPropertyChanged();
            }
        }
        public double MemorySizeMB
        {
            get { return result.PeakMemorySizeMB; }
        }
        public string Worker
        {
            get { return result.WorkerInformation; }
        }
        public string StdOut
        {
            get
            {
                StreamReader reader = new StreamReader(result.StdOut);
                string text = reader.ReadToEnd();
                return text != "" ? text : "*** NO OUTPUT SAVED ***";
            }
        }
        public string StdErr
        {
            get
            {
                StreamReader reader = new StreamReader(result.StdErr);
                string text = reader.ReadToEnd();
                return text != "" ? text : "*** NO OUTPUT SAVED ***";
            }
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
