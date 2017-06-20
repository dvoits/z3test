using Measurement;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public class DuplicatesViewModel : INotifyPropertyChanged
    {
        private readonly int id;
        private readonly ExperimentManager manager;
        private readonly IUIService uiService;
        private BenchmarkResultViewModel[] results = null;
        private List<BenchmarkResultViewModel> duplicates;
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
            ResolveData();
        }
        public List<BenchmarkResultViewModel> Duplicates {
            get { return duplicates;}
            private set
            {
                duplicates = value;
                NotifyPropertyChanged();
            }
        }
        private async void downloadResultsAsync()
        {
            var t1 = Task.Run(() => manager.GetResults(id));
            var res = await t1;
            results = res.Select(e => new BenchmarkResultViewModel(e, manager, uiService)).ToArray();
        }
        private void ResolveData()
        {
            downloadResultsAsync();
            List<BenchmarkResultViewModel> dupList = new List<BenchmarkResultViewModel>();
            if (results != null && results.Length > 1)
            {
                string prevFilename = results[0].Filename;
                bool isNew = true;
                for (int i = 1; i < results.Length; i++)
                {
                    string nextFilename = results[i].Filename;
                    if (nextFilename == prevFilename)
                    {
                        if (isNew)
                        {
                            dupList.Add(results[i - 1]);
                            isNew = false;
                        }
                        dupList.Add(results[i]);
                    }
                    else
                    {
                        prevFilename = nextFilename;
                        isNew = true;
                    }
                }
            }
            Duplicates = dupList;
        }
        public void Pick(BenchmarkResultViewModel item)
        {
            throw new NotImplementedException();
            //оставить только item остальные с тем же именем удалить
        }
        public bool showNextDupe()
        {
            if (Duplicates.Count == 0)
                return true;
            else
            {
                bool not_done = true;
                do
                {
                    if ((resolveTimeouts || resolveSameTime || resolveSlowest) && Duplicates.Count > 0)
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

                        foreach (BenchmarkResultViewModel r in Duplicates)
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
                            Pick(Duplicates.First());
                        else if (resolveSameTime && all_ok && all_times_same)
                            Pick(Duplicates.First());
                        else if (resolveSlowest && (all_ok || all_memouts))
                            Pick(max_item);
                        else
                        {
                            not_done = true;
                            return false;
                        }
                    }
                    else
                        not_done = false;
                }
                while (not_done && Duplicates.Count() > 0);

                if (not_done == false && Duplicates.Count() == 0)
                    return true;
                return false;
            }
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
