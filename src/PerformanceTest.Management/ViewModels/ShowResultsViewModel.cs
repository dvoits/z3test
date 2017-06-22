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
                else if (code == 4) Results = allResults.Where(e => e.Status == ResultStatus.Error || e.Status == ResultStatus.InfrastructureError).ToArray();
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

        public void ReclassifyResults(BenchmarkResultViewModel[] old_Results, ResultStatus rc)
        {
            List<BenchmarkResult> new_Results = new List<BenchmarkResult>();
            foreach (var res in old_Results)
            {
                BenchmarkResult old_result = res.GetBenchmarkResult();
                BenchmarkResult new_result = new BenchmarkResult(old_result.ExperimentID, old_result.BenchmarkFileName, 
                        old_result.AcquireTime, old_result.NormalizedRuntime, old_result.TotalProcessorTime, old_result.WallClockTime, 
                        old_result.PeakMemorySizeMB, rc, old_result.ExitCode, old_result.StdOut, old_result.StdErr, old_result.Properties);
                new_Results.Add(new_result);
            }
            manager.UpdateExperiment(id, null, new_Results.ToArray(), null);
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
