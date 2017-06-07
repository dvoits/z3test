using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public class ExperimentListViewModel : INotifyPropertyChanged
    {
        private readonly ExperimentManager manager;
        private readonly IUIService ui;
        private ExperimentStatusViewModel[] allExperiments;
        private ExperimentStatusViewModel[] filteredExperiments;

        private string keyword;
        private bool filterPending, isFiltering;

        public event PropertyChangedEventHandler PropertyChanged;

        public ExperimentListViewModel(ExperimentManager manager, IUIService ui)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (ui == null) throw new ArgumentNullException("message");
            this.manager = manager;
            this.ui = ui;

            RefreshItemsAsync();
        }

        public ExperimentStatusViewModel[] Items
        {
            get { return filteredExperiments; }
            private set
            {
                filteredExperiments = value;
                NotifyPropertyChanged();
            }
        }

        public string FilterKeyword
        {
            get { return keyword; }
            set
            {
                if (keyword == value) return;
                keyword = value;
                NotifyPropertyChanged();
                FilterExperiments(keyword);
            }
        }


        public void Refresh()
        {
            RefreshItemsAsync();
        }

        public async void DeleteExperiment(int id)
        {
            if (allExperiments == null) return;

            var handle = ui.StartIndicateLongOperation("Deleting the experiment...");
            try
            {
                Items = filteredExperiments.Where(st => st.ID != id).ToArray();
                await Task.Run(() => manager.DeleteExperiment(id));
            }
            catch (Exception ex)
            {
                Refresh();
                ui.ShowError(ex, "Error occured when tried to delete the experiment " + id.ToString());
            }
            finally
            {
                ui.StopIndicateLongOperation(handle);
            }
        }

        public Task<double> GetRuntimes(int[] ids)
        {
            return Task.Run(async () =>
            {
                var res = await manager.GetStatus(ids);
                return res.Sum(r => r.TotalRuntime.TotalSeconds);
            });
        }

        private async void RefreshItemsAsync()
        {
            var handle = ui.StartIndicateLongOperation("Loading table of experiments...");
            try
            {
                Items = null;
                var exp = await Task.Run(() => manager.FindExperiments());
                allExperiments = exp.Select(e => new ExperimentStatusViewModel(e, manager, ui)).ToArray();
                Items = FilterExperiments(allExperiments, keyword);
            }
            catch (Exception ex)
            {
                ui.ShowError(ex, "Failed to load experiments list");
            }
            finally
            {
                ui.StopIndicateLongOperation(handle);
            }
        }

        private async void FilterExperiments(string keyword)
        {
            if (allExperiments == null) return;
            if (isFiltering)
            {
                filterPending = true;
                return;
            }

            var handle = ui.StartIndicateLongOperation("Filtering experiments...");
            isFiltering = true;
            try
            {
                do
                {
                    filterPending = false;

                    var old = allExperiments;
                    var filtered = await Task.Run(() => FilterExperiments(old, keyword).ToArray());

                    if (allExperiments == old)
                        Items = filtered;
                } while (filterPending);
            }
            catch (Exception ex)
            {
                ui.ShowError(ex, "Failed to filter experiments list");
            }
            finally
            {
                ui.StopIndicateLongOperation(handle);
                isFiltering = false;
            }
        }

        private static ExperimentStatusViewModel[] FilterExperiments(ExperimentStatusViewModel[] source, string keyword)
        {
            if (String.IsNullOrEmpty(keyword)) return source;

            List<ExperimentStatusViewModel> dest = new List<ExperimentStatusViewModel>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                var e = source[i];
                if (Contains(e.Category, keyword) ||
                    Contains(e.Creator, keyword) ||
                    Contains(e.ID.ToString(), keyword) ||
                    Contains(e.Note, keyword))
                    dest.Add(e);
            }
            return dest.ToArray();
        }

        private static bool Contains(string str, string keyword)
        {
            if (str == null) return String.IsNullOrEmpty(keyword);
            return keyword == null || str.Contains(keyword);
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ExperimentStatusViewModel : INotifyPropertyChanged
    {
        private readonly ExperimentStatus status;
        private readonly ExperimentDefinition definition;
        private readonly ExperimentManager manager;
        private readonly IUIService uiService;

        private bool flag;
        private string note;

        public event PropertyChangedEventHandler PropertyChanged;

        public ExperimentStatusViewModel(Experiment exp, ExperimentManager manager, IUIService message)
        {
            if (exp == null) throw new ArgumentNullException("experiment");
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.status = exp.Status;
            this.definition = exp.Definition;
            this.flag = status.Flag;
            this.note = status.Note;
            this.manager = manager;
            this.uiService = message;
        }

        public ExperimentDefinition Definition { get { return definition; } }
        public int ID { get { return status.ID; } }

        public string Category { get { return status.Category; } }

        public string Submitted { get { return status.SubmissionTime.ToString(); } }

        public string Note
        {
            get { return note; }
            set
            {
                note = value;
                NotifyPropertyChanged();
                UpdateNote();
            }

        }

        public string Creator { get { return status.Creator; } }

        public string WorkerInformation { get { return status.WorkerInformation; } }

        public int? BenchmarksDone { get { return status.BenchmarksTotal == 0 ? null : (int?)status.BenchmarksDone; } }
        public int? BenchmarksTotal { get { return status.BenchmarksTotal == 0 ? null : (int?)status.BenchmarksTotal; } }
        public int? BenchmarksQueued { get { return status.BenchmarksTotal == 0 ? null : (int?)status.BenchmarksQueued; } }

        public bool Flag
        {
            get { return flag; }

            set
            {
                if (status.Flag != value && status.Flag == flag)
                {
                    flag = value;
                    NotifyPropertyChanged();

                    UpdateStatusFlag();
                }
            }
        }

        private async void UpdateNote()
        {
            try
            {
                await manager.UpdateNote(status.ID, note);
                status.Note = note;
                Trace.WriteLine("Note changed to '" + note + "' for " + status.ID);
            }
            catch (Exception ex)
            {
                note = status.Note;
                NotifyPropertyChanged("Note");
                uiService.ShowError(ex, "Failed to update experiment note");
            }
        }
        private async void UpdateStatusFlag()
        {
            try
            {
                await manager.UpdateStatusFlag(status.ID, flag);
                status.Flag = flag;
                Trace.WriteLine("Status flag changed to " + flag + " for " + status.ID);
            }
            catch (Exception ex)
            {
                flag = status.Flag;
                NotifyPropertyChanged("Note");
                uiService.ShowError(ex, "Failed to update experiment status flag");
            }
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
