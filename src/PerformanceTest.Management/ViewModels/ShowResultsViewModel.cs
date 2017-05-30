using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using Measurement;

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
            allResults = Results = res.Select(e => new BenchmarkResultViewModel(e, manager, message)).ToArray();
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
            if (code == 0) Results = allResults.Where(e => e.Status == ResultStatus.Success && e.Sat > 0).ToArray();
            else if (code == 1) Results = allResults.Where(e => e.Status == ResultStatus.Success && e.Unsat > 0).ToArray();
            else if (code == 2) Results = allResults.Where(e => e.Status == ResultStatus.Success && e.Unknown > 0).ToArray();
            else if (code == 3) Results = allResults.Where(e => e.Status == ResultStatus.Bug).ToArray();
            else if (code == 4) Results = allResults.Where(e => e.Status == ResultStatus.Error).ToArray();
            else if (code == 5) Results = allResults.Where(e => e.Status == ResultStatus.Timeout).ToArray();
            else if (code == 6) Results = allResults.Where(e => e.Status == ResultStatus.OutOfMemory).ToArray();
            else if (code == 7) Results = allResults.Where(e => e.Status == ResultStatus.Success && e.Sat + e.Unsat > e.TargetSat + e.TargetUnsat && e.Unknown < e.TargetUnknown).ToArray();
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
            Results = allResults.Where(e => e.NormalizedRuntime >= limit).ToArray();
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class BenchmarkResultViewModel: INotifyPropertyChanged
    {
        private BenchmarkResult result;
        private readonly ExperimentManager manager;
        private readonly IUIService message;
        private ResultStatus status;
        private double runtime;
        public event PropertyChangedEventHandler PropertyChanged;
        public BenchmarkResultViewModel (BenchmarkResult res, ExperimentManager manager, IUIService message)
        {
            if (res == null) throw new ArgumentNullException("benchmark");
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.result = res;
            this.manager = manager;
            this.message = message;

            this.status = res.Status;
            this.runtime = res.NormalizedRuntime;
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
        public ResultStatus Status
        {
            get { return status; }
            set {
                status = value;
                NotifyPropertyChanged();
                UpdateResultStatus();
                NotifyPropertyChanged("Runtime");
            }
        }
        private async void UpdateResultStatus()
        {
            try
            {
                if (status == ResultStatus.Timeout) UpdateRuntime();
                await manager.UpdateResultStatus(result.ExperimentID, status);
                BenchmarkResult newResult = new BenchmarkResult(result.ExperimentID, result.BenchmarkFileName, result.WorkerInformation,
                    result.AcquireTime, runtime, result.TotalProcessorTime, result.WallClockTime, result.PeakMemorySizeMB,
                    status, result.ExitCode, result.StdOut, result.StdErr, result.Properties);
                result = newResult;
                Trace.WriteLine("Result status changed to '" + status.ToString() + "' for " + result.ExperimentID);
                
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to update experiment result status: " + ex.Message);
                status = result.Status;
                runtime = result.NormalizedRuntime;
                NotifyPropertyChanged("Status");
                message.ShowError("Failed to update benchmark status: " + ex.Message);
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
        public double NormalizedRuntime
        {
            get { return runtime; }
            set {
                runtime = value;
                NotifyPropertyChanged();
             //   UpdateRuntimeStatus();
            }
        }
        private async void UpdateRuntime()
        {
            try
            {
                await manager.UpdateRuntime(result.ExperimentID, runtime);
                Trace.WriteLine("Runtime changed to '" + runtime + "' for " + result.ExperimentID);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to update experiment runtime: " + ex.Message);
                runtime = result.NormalizedRuntime;
                NotifyPropertyChanged("Runtime");
                message.ShowError("Failed to update benchmark runtime: " + ex.Message);
                throw ex;
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
