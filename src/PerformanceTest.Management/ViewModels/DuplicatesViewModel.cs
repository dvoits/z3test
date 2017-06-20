using Measurement;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PerformanceTest.Management
{
    public class DuplicatesViewModel : INotifyPropertyChanged
    {
        private readonly int id;
        private readonly ExperimentManager manager;
        private readonly IUIService uiService;
        private List<string> filenames;
        private Dictionary<string, List<BenchmarkResultViewModel>> duplicates;
        private List<BenchmarkResultViewModel> currentDuplicates;
        private BenchmarkResult[] results; 

        private bool resolveTimeouts, resolveSameTime, resolveSlowest;
        public event PropertyChangedEventHandler PropertyChanged;
        public DuplicatesViewModel(int id, bool resolveTimeouts, bool resolveSameTime, bool resolveSlowest, ExperimentManager manager, IUIService uiService)
        {
            this.id = id;
            this.resolveTimeouts = resolveTimeouts;
            this.resolveSameTime = resolveSameTime;
            this.resolveSlowest = resolveSlowest;
            this.manager = manager;
            this.uiService = uiService;
            this.filenames = new List<string>();
            this.currentDuplicates = new List<BenchmarkResultViewModel>();
        }
        public string Title
        {
            get { return "Duplicates in experiment #" + id + "..."; }
        }
        public List<BenchmarkResultViewModel> Duplicates {
            get { return currentDuplicates;}
            private set
            {
                currentDuplicates = value;
                NotifyPropertyChanged();
            }
        }
        public async Task<bool> DownloadResultsAsync()
        {
            var t1 = Task.Run(() => manager.GetResults(id));
            var res = await t1;
            results = res; 
            var dataToResolve = res.Select(e => new BenchmarkResultViewModel(e, manager, uiService)).ToArray();

            return ResolveData(dataToResolve);
        }
        private bool ResolveData(BenchmarkResultViewModel[] results)
        {
            Dictionary<string, List<BenchmarkResultViewModel>> dict = new Dictionary<string, List<BenchmarkResultViewModel>>();
            if (results != null && results.Length > 1)
            {
                List<BenchmarkResultViewModel> dupList = new List<BenchmarkResultViewModel>();
                string prevFilename = results[0].Filename;
                bool isNew = true;
                for (int i = 1; i < results.Length; i++)
                {
                    string nextFilename = results[i].Filename;
                    if (nextFilename == prevFilename)
                    {
                        if (isNew)
                        {
                            filenames.Add(prevFilename);
                            dupList.Add(results[i - 1]);
                            isNew = false;
                        }
                        dupList.Add(results[i]);
                    }
                    else
                    {
                        if (dupList.Count > 0)
                        {
                            dict.Add(prevFilename, dupList);
                            dupList = new List<BenchmarkResultViewModel>();
                        }
                        prevFilename = nextFilename;
                        isNew = true;
                    }
                }
            }
            duplicates = dict;

            showNextDupe();
            return filenames.Count > 0;
        }
        private bool IsEqualBenchmarks (BenchmarkResult b1, BenchmarkResultViewModel b2)
        {
            var b1vm = new BenchmarkResultViewModel(b1, manager, uiService);
            bool result = b1vm.ExitCode == b2.ExitCode && b1vm.Filename == b2.Filename && b1vm.MemorySizeMB == b2.MemorySizeMB &&
                          b1vm.NormalizedRuntime == b2.NormalizedRuntime && b1vm.Sat == b2.Sat && b1vm.Status == b2.Status &&
                          b1vm.TargetSat == b2.TargetSat && b1vm.TargetUnknown == b2.TargetUnknown && b1vm.TargetUnsat == b2.TargetUnsat &&
                          b1vm.TotalProcessorTime == b2.TotalProcessorTime && b1vm.Unknown == b2.Unknown && b1vm.Unsat == b2.Unsat &&
                          b1vm.WallClockTime == b2.WallClockTime;
            return result;
                            
        }
        public void Pick(BenchmarkResultViewModel item)
        {
            var new_Results = results.Where(i => !IsEqualBenchmarks(i, item)).ToArray();



            throw new NotImplementedException();
            
            //remove duplicates
        }
        public void showNextDupe()
        {
            if (filenames.Count == 0)
                return;
            else
            {
                bool not_done = true;
                do
                {
                    string next_fn = filenames.First();
                    filenames.RemoveAt(0);
                    var duplicates_fn = new List<BenchmarkResultViewModel>();
                    duplicates.TryGetValue(next_fn, out duplicates_fn);
                    Duplicates = duplicates_fn;
                    if (resolveTimeouts || resolveSameTime || resolveSlowest)
                    {
                        bool first = true;
                        bool all_timeouts = true;
                        bool all_ok = true;
                        bool all_times_same = true;
                        bool all_memouts = true;
                        double runtime = 0.0;
                        double min_time = double.MaxValue;
                        BenchmarkResultViewModel min_item = null;
                        double max_time = double.MinValue;
                        BenchmarkResultViewModel max_item = null;

                        foreach (BenchmarkResultViewModel r in duplicates_fn)
                        {
                            ResultStatus status = r.Status;
                            double time = r.NormalizedRuntime;
                            if (status != ResultStatus.Timeout) { all_timeouts = false; }
                            if (status != ResultStatus.Success) { all_ok = false; }
                            if (status != ResultStatus.OutOfMemory) { all_memouts = false; }

                            if (time < min_time)
                            {
                                min_time = time;
                                min_item = r;
                            }

                            if (time > max_time)
                            {
                                max_time = time;
                                max_item = r;
                            }

                            if (first)
                            {
                                first = false; runtime = time;
                            }
                            else
                            {
                                if (time != runtime) all_times_same = false;
                            }
                        }

                        if (resolveTimeouts && all_timeouts)
                            Pick(duplicates_fn.First());
                        else if (resolveSameTime && all_ok && all_times_same)
                            Pick(duplicates_fn.First());
                        else if (resolveSlowest && (all_ok || all_memouts))
                            Pick(max_item);
                        else
                        {
                            not_done = true;
                            uiService.ShowDuplicatesWindow(this);
                        }
                    }
                    else
                        not_done = false;
                }
                while (not_done && filenames.Count() > 0);

                if (not_done == false && filenames.Count() == 0)
                    return;
                uiService.ShowDuplicatesWindow(this);
            }
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
