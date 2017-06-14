using Measurement;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{

    public class BenchmarkResultViewModel : INotifyPropertyChanged
    {
        private readonly ExperimentManager manager;
        private readonly IUIService uiService;

        private BenchmarkResult result;
        private ResultStatus status;

        private double runtime;

        public event PropertyChangedEventHandler PropertyChanged;

        public BenchmarkResultViewModel(BenchmarkResult res, ExperimentManager manager, IUIService message)
        {
            if (res == null) throw new ArgumentNullException("benchmark");
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.result = res;
            this.manager = manager;
            this.uiService = message;

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
        public int ExitCode
        {
            get { return result.ExitCode; }
        }
        public double WallClockTime
        {
            get { return result.WallClockTime.TotalSeconds; }
        }
        public double TotalProcessorTime
        {
            get { return result.TotalProcessorTime.TotalSeconds; }
        }
        public ResultStatus Status
        {
            get { return status; }
            set
            {
                status = value;
                NotifyPropertyChanged();
                UpdateResultStatus();
                NotifyPropertyChanged("Runtime");
            }
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
            set
            {
                runtime = value;
                NotifyPropertyChanged();
            }
        }

        public double MemorySizeMB
        {
            get { return result.PeakMemorySizeMB; }
        }

        public Task<string> GetStdOutAsync(bool useDefaultIfMissing)
        {
            return ReadOutputAsync(result.StdOut, useDefaultIfMissing);
        }

        public Task<string> GetStdErrAsync(bool useDefaultIfMissing)
        {
            return ReadOutputAsync(result.StdErr, useDefaultIfMissing);
        }


        public async Task<ShowOutputViewModel> GetOutputViewModel()
        {
            var handle = uiService.StartIndicateLongOperation("Loading benchmark output...");
            try
            {
                return await Task.Run(async () =>
                {
                    string stdOut = await GetStdOutAsync(true);
                    string stdErr = await GetStdErrAsync(true);
                    ShowOutputViewModel vm = new ShowOutputViewModel(ID, Filename, stdOut, stdErr);
                    return vm;
                });
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
        }

        private async void UpdateResultStatus()
        {
            try
            {
                if (status == ResultStatus.Timeout) UpdateRuntime();
                await manager.UpdateResultStatus(result.ExperimentID, status);
                BenchmarkResult newResult = new BenchmarkResult(result.ExperimentID, result.BenchmarkFileName,
                    result.AcquireTime, runtime, result.TotalProcessorTime, result.WallClockTime, result.PeakMemorySizeMB,
                    status, result.ExitCode, result.StdOut, result.StdErr, result.Properties);
                result = newResult;
                Trace.WriteLine("Result status changed to '" + status.ToString() + "' for " + result.ExperimentID);

            }
            catch (Exception ex)
            {
                status = result.Status;
                runtime = result.NormalizedRuntime;
                NotifyPropertyChanged("Status");
                uiService.ShowError(ex, "Failed to update benchmark status");
            }
        }

        private int GetProperty(string prop)
        {
            int res = result.Properties.ContainsKey(prop) ? Int32.Parse(result.Properties[prop]) : 0;
            return res;
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
                runtime = result.NormalizedRuntime;
                NotifyPropertyChanged("Runtime");
                uiService.ShowError(ex, "Failed to update benchmark runtime");
                throw ex;
            }
        }

        private async Task<string> ReadOutputAsync(Stream stream, bool useDefaultIfMissing)
        {
            string text = null;
            if (stream != null)
            {
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                text = await reader.ReadToEndAsync();
                text = System.Text.RegularExpressions.Regex.Unescape(text);
            }

            if (useDefaultIfMissing && String.IsNullOrEmpty(text))
                return "*** NO OUTPUT SAVED ***";
            return text;
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
