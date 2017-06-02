using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using Measurement;
using System.Diagnostics;
using System.IO;

namespace PerformanceTest.Management
{
    public class ShowResultsViewModel : INotifyPropertyChanged
    {
        private readonly int id;
        private readonly ExperimentManager manager;
        private readonly IUIService uiService;
        private readonly string sharedDirectory;

        private IEnumerable<BenchmarkResultViewModel> results, allResults;
        private bool isFiltering;


        public event PropertyChangedEventHandler PropertyChanged;

        public ShowResultsViewModel(int id, string sharedDirectory, ExperimentManager manager, IUIService uiService)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (uiService == null) throw new ArgumentNullException("uiService");
            this.manager = manager;
            this.uiService = uiService;
            this.id = id;
            this.sharedDirectory = sharedDirectory;
            RefreshResultsAsync();
        }

        public bool IsFiltering
        {
            get { return isFiltering; }
            private set
            {
                isFiltering = value;
                NotifyPropertyChanged();
            }
        }

        private async void RefreshResultsAsync()
        {
            var handle = uiService.StartIndicateLongOperation("Loading experiment results...");
            try
            {
                allResults = Results = null;
                var res = await Task.Run(() => manager.GetResults(id));
                allResults = Results = res.Select(e => new BenchmarkResultViewModel(e, manager, uiService)).ToArray();
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex.Message, "Failed to load experiment results");
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
        }
        public IEnumerable<BenchmarkResultViewModel> Results
        {
            get { return results; }
            private set
            {
                results = value;
                NotifyPropertyChanged();
            }
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
            if (IsFiltering) return;
            IsFiltering = true;
            try
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
            finally
            {
                IsFiltering = false;
            }
        }
        public async void FilterResultsByText(string filter, int code)
        {
            if (String.IsNullOrEmpty(filter) || code < 0 || code > 1) return;
            if (IsFiltering) return;

            var handle = uiService.StartIndicateLongOperation("Filtering results...");
            IsFiltering = true;
            try
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
                    if (code == 1) // search in outputs
                    {
                        Results = await Task.Run(async () =>
                        {
                            var selectionTask = allResults.Select(async r =>
                            {
                                string output = await r.GetStdOutAsync(false);
                                if (output.Contains(filter)) return true;

                                string error = await r.GetStdErrAsync(false);
                                if (error.Contains(filter)) return true;

                                return false;
                            });
                            var selection = await Task.WhenAll(selectionTask);
                            return allResults.Where((r, i) => selection[i]).ToArray();
                        });
                    }
                }
                else Results = allResults;
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex, "Failed to filter results");
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
                IsFiltering = false;
            }
        }

        public void FilterResultsByRuntime(int limit)
        {
            if (IsFiltering) return;
            IsFiltering = true;
            try
            {
                Results = allResults.Where(e => e.NormalizedRuntime >= limit).ToArray();
            }
            finally
            {
                IsFiltering = false;
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class BenchmarkResultViewModel : INotifyPropertyChanged
    {
        private BenchmarkResult result;
        private readonly ExperimentManager manager;
        private readonly IUIService uiService;
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
                uiService.ShowError(ex, "Failed to update benchmark status");
            }
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

        private int GetProperty(string prop)
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
            set
            {
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
                uiService.ShowError("Failed to update benchmark runtime: " + ex.Message);
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
        public Task<string> GetStdOutAsync(bool useDefaultIfMissing)
        {
            return ReadOutputAsync(result.StdOut, useDefaultIfMissing);
        }

        public Task<string> GetStdErrAsync(bool useDefaultIfMissing)
        {
            return ReadOutputAsync(result.StdErr, useDefaultIfMissing);
        }

        private async Task<string> ReadOutputAsync(Stream stream, bool useDefaultIfMissing)
        {
            string text = null;
            if (stream != null)
            {
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                text = await reader.ReadToEndAsync();
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
