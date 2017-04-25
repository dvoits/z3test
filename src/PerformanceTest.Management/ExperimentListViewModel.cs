using System;
using System.Collections.Generic;
using System.ComponentModel;
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


        public event PropertyChangedEventHandler PropertyChanged;


        public ExperimentListViewModel(ExperimentManager manager)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            this.manager = manager;

            RefreshItemsAsync();
        }

        public IEnumerable<ExperimentStatusViewModel> Items
        {
            get { return experiments; }
            private set { experiments = value; NotifyPropertyChanged(); }
        }

        public void DeleteExperiment (int id)
        {
            var items = Items.Where(st => st.ID != id).ToArray();
            manager.DeleteExperiment(id);
            Items = items;
        }
        public void UpdateFlag (int id)
        {
            var items = Items.Select(st => {
                if (st.ID == id)
                    st.Flag = !st.Flag;
                return st;
            }).ToArray();
            //manager.UpdateExperiment(id);
            Items = items;
        }
        public double GetRuntime (int id)
        {
            var def = manager.GetResults(id).Select(res => res.Result);
            return def.Sum(r => r.NormalizedRuntime);
        }
        public async void FindExperiments (string filter)
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
                Items = status.Select(st => new ExperimentStatusViewModel(st)).ToArray();
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
            //var stat = status.OrderByDescending(s => s.ID);
            Items = status.Select(st => new ExperimentStatusViewModel(st)).ToArray();
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ExperimentStatusViewModel
    {
        private readonly ExperimentStatus status;

        public ExperimentStatusViewModel(ExperimentStatus status)
        {
            if (status == null) throw new ArgumentNullException("status");
            this.status = status;
        }

        public int ID { get { return status.ID; } }

        public string Category { get { return status.Category; } }

        public string Submitted { get { return status.SubmissionTime.ToString(); } }

        public bool Flag {
            get { return status.Flag; }
            set { status.Flag = value; }
        }
    }
}
