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
        private Dictionary<string, List<BenchmarkResult>> duplicates;
        private List<BenchmarkResultViewModel> currentDuplicates;
        private BenchmarkResult[] results;
        private List<BenchmarkResult> duplicatesToRemove;

        private bool resolveTimeouts, resolveSameTime, resolveSlowest, resolveInErrors;
        public event PropertyChangedEventHandler PropertyChanged;
        public DuplicatesViewModel(int id, bool resolveTimeouts, bool resolveSameTime, bool resolveSlowest, bool resolveInErrors, ExperimentManager manager, IUIService uiService)
        {
            this.id = id;
            this.resolveTimeouts = resolveTimeouts;
            this.resolveSameTime = resolveSameTime;
            this.resolveSlowest = resolveSlowest;
            this.resolveInErrors = resolveInErrors;
            this.manager = manager;
            this.uiService = uiService;
            this.filenames = new List<string>();
            this.currentDuplicates = new List<BenchmarkResultViewModel>();
            this.duplicatesToRemove = new List<BenchmarkResult>();
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
            return ResolveData(results);
        }
        private bool ResolveData(BenchmarkResult[] results)
        {
            Dictionary<string, List<BenchmarkResult>> dict = new Dictionary<string, List<BenchmarkResult>>();
            if (results != null && results.Length > 1)
            {
                List<BenchmarkResult> dupList = new List<BenchmarkResult>();
                string prevFilename = results[0].BenchmarkFileName;
                bool isNew = true;
                for (int i = 1; i < results.Length; i++)
                {
                    string nextFilename = results[i].BenchmarkFileName;
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
                            dupList = new List<BenchmarkResult>();
                        }
                        prevFilename = nextFilename;
                        isNew = true;
                    }
                }
            }
            duplicates = dict;

            showNextDupe();
            if (duplicatesToRemove.Count > 0)
                DeleteResults(duplicatesToRemove.ToArray());
            return filenames.Count > 0;
        }
        public void Pick(List<BenchmarkResultViewModel> items)
        {
            BenchmarkResult[] itemsToRemove = items.Select(elem => elem.GetBenchmarkResult()).ToArray();
            duplicatesToRemove.AddRange(itemsToRemove);
        }
        public void showNextDupe()
        {
            if (filenames.Count == 0)
                return;
            else
            {
                do
                {
                    string next_fn = filenames.First();
                    filenames.RemoveAt(0);
                    var duplicates_fn = new List<BenchmarkResult>();
                    duplicates.TryGetValue(next_fn, out duplicates_fn);
                    Duplicates = duplicates_fn.Select(item => new BenchmarkResultViewModel(item, manager, uiService)).ToList();
                    if (resolveTimeouts || resolveSameTime || resolveSlowest || resolveInErrors)
                    {
                        bool first = true;
                        bool all_timeouts = true;
                        bool all_ok = true;
                        bool all_times_same = true;
                        bool all_memouts = true;
                        double runtime = 0.0;
                        double min_time = double.MaxValue;
                        BenchmarkResult min_item = null;
                        double max_time = double.MinValue;
                        BenchmarkResult max_item = null;

                        foreach (BenchmarkResult r in duplicates_fn)
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
                        {
                            BenchmarkResult pickItem = duplicates_fn.First();
                            duplicates_fn.Remove(pickItem);
                            duplicatesToRemove.AddRange(duplicates_fn);
                        }
                        else if (resolveSameTime && all_ok && all_times_same)
                        {
                            BenchmarkResult pickItem = duplicates_fn.First();
                            duplicates_fn.Remove(pickItem);
                            duplicatesToRemove.AddRange(duplicates_fn);
                        }
                        else if (resolveSlowest && (all_ok || all_memouts))
                        {
                            duplicates_fn.Remove(max_item);
                            duplicatesToRemove.AddRange(duplicates_fn);
                        }
                        //TO DO: add resolving in. errors
                        else
                        {
                            uiService.ShowDuplicatesWindow(this);
                        }
                    }
                    else
                    {
                        uiService.ShowDuplicatesWindow(this);   
                    }
                }
                while (filenames.Count() > 0);                 
            }
        }
        public void DeleteResults(BenchmarkResult[] removing_results)
        {
            throw new NotImplementedException();
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
