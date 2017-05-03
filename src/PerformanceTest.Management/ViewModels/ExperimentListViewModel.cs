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
        private IEnumerable<ExperimentStatusViewModel> experiments;
        private readonly ExperimentManager manager;
        private readonly IUIService message;


        public event PropertyChangedEventHandler PropertyChanged;


        public ExperimentListViewModel(ExperimentManager manager, IUIService message)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.manager = manager;
            this.message = message;

            RefreshItemsAsync();
        }

        public IEnumerable<ExperimentStatusViewModel> Items
        {
            get { return experiments; }
            private set { experiments = value; NotifyPropertyChanged(); }
        }

        public void DeleteExperiment(int id)
        {
            var items = Items.Where(st => st.ID != id).ToArray();
            manager.DeleteExperiment(id);
            Items = items;
        }

        public void UpdatePriority(int id, string priority)
        {
            manager.UpdatePriority(id, priority);
        }
        public double GetRuntime(int id)
        {
            return manager.GetResults(id).Sum(res => res.IsCompleted ? res.Result.NormalizedRuntime : 0);
        }

        public async void FindExperiments(string filter)
        {
            if (filter != "")
            {
                ExperimentManager.ExperimentFilter filt = new ExperimentManager.ExperimentFilter
                {
                    NotesEquals = filter,
                    CategoryEquals = filter,
                    CreatorEquals = filter
                };
                var ids = await manager.FindExperiments(filt);
                var status = await manager.GetStatus(ids);
                Items = status.Select(st => new ExperimentStatusViewModel(st, manager, message)).ToArray();
            }
            else
            {
                RefreshItemsAsync();
            }
        }
        private async void RefreshItemsAsync()
        {
            Items = null;

            var ids = await manager.FindExperiments();
            var status = await manager.GetStatus(ids);
            Items = status.Select(st => new ExperimentStatusViewModel(st, manager, message)).ToArray();
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ExperimentStatusViewModel : INotifyPropertyChanged
    {
        private readonly ExperimentStatus status;
        private readonly ExperimentManager manager;
        private readonly IUIService message;

        private bool flag;
        private string note;

        public event PropertyChangedEventHandler PropertyChanged;

        public ExperimentStatusViewModel(ExperimentStatus status, ExperimentManager manager, IUIService message)
        {
            if (status == null) throw new ArgumentNullException("status");
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.status = status;
            this.flag = status.Flag;
            this.note = status.Note;
            this.manager = manager;
            this.message = message;
        }

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

        public int BenchmarksDone { get { return status.BenchmarksDone; } }
        public int BenchmarksTotal { get { return status.BenchmarksTotal; } }
        public int BenchmarksQueued { get { return status.BenchmarksQueued; } }

        public bool Flag
        {
            get { return flag; }

            set {
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
                Trace.WriteLine("Note changed to '" + note + "' for " + status.ID);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to update experiment note: " + ex.Message);
                NotifyPropertyChanged("Note");
                message.ShowError("Failed to update experiment status flag: " + ex.Message);
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
                Trace.WriteLine("Failed to update experiment status flag: " + ex.Message);
                flag = status.Flag;
                NotifyPropertyChanged("Note");
                message.ShowError("Failed to update experiment note: " + ex.Message);
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
