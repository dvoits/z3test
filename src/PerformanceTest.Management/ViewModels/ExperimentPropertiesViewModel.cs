using Measurement;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PerformanceTest.Management
{
    public class ExperimentPropertiesViewModel: INotifyPropertyChanged
    {
        private ExperimentStatusViewModel statusVm;
        private IEnumerable<BenchmarkResultViewModel> result;
        public event PropertyChangedEventHandler PropertyChanged;
        private ExperimentManager manager;
        private readonly IUIService message;
        private int id;
        private readonly string[] MachineStatuses = { "OK", "Unable to retrieve status." };
        public ExperimentPropertiesViewModel(ExperimentStatusViewModel statusVm, ExperimentManager manager, int id, IUIService message)
        {
            if (statusVm == null) throw new ArgumentNullException("statusVm");
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.id = id;
            this.statusVm = statusVm;
            this.manager = manager;
            this.message = message;
            var res = manager.GetResults(id).Result;
            this.result = res.Select(elem => new BenchmarkResultViewModel(elem, manager, message));
            
        }
        public string SubmissionTime
        {
            get { return statusVm.Submitted; }
        }
        public string Category
        {
            get { return statusVm.Category; }
        }
        public int BenchmarksTotal
        {
            get { return statusVm.BenchmarksTotal; }
        }
        public int BenchmarksDone
        {
            get { return statusVm.BenchmarksDone; }
        }
        public int BenchmarksQueued
        {
            get { return statusVm.BenchmarksQueued; }
        }
        public Brush QueuedForeground
        {
            get
            {
                return (statusVm.BenchmarksQueued == 0) ? Brushes.Green : Brushes.Red;
            }
        }
        
        public int Sat
        {
            get
            {
                return result.Sum(elem => elem.Sat);
            }

        }
        public int Unsat
        {
            get
            {
                return result.Sum(elem => elem.Unsat);
            }
        }
        public int Unknown
        {
            get
            {
                return result.Sum(elem => elem.Unknown);
            }
        }
        public int Overperformed
        {
            get
            {
                return result.Sum(e => (e.Status == ResultStatus.Success && e.Sat + e.Unsat > e.TargetSat + e.TargetUnsat && e.Unknown < e.TargetUnknown) ? 1 : 0);
            }
        }
        public int Underperformed
        {
            get
            {
                return result.Sum(e => (e.Sat + e.Unsat < e.Sat + e.Unsat || e.Unknown > e.TargetUnknown) ? 1 : 0);
            }
        }
        public int ProblemBug
        {
            get
            {
                return result.Sum(e => (e.Status == ResultStatus.Bug) ? 1 : 0);
            }
        }
        public Brush BugForeground
        {
            get { return ProblemBug == 0 ? Brushes.Black : Brushes.Red; }
        }
        public int ProblemNonZero
        {
            get { return result.Sum(e => (e.Status == ResultStatus.Error) ? 1 : 0); }
        }
        public Brush NonZeroForeground
        {
            get { return ProblemNonZero == 0 ? Brushes.Black : Brushes.Red; } 
        }
        public int ProblemTimeout
        {
            get { return result.Sum(e => (e.Status == ResultStatus.Timeout) ? 1 : 0); }
        }
        public Brush TimeoutForeground
        {
            get { return ProblemTimeout == 0 ? Brushes.Black : Brushes.Red; } 
        }
        public int ProblemMemoryout
        {
            get { return result.Sum(e => (e.Status == ResultStatus.OutOfMemory) ? 1 : 0); }
        }
        public Brush MemoryoutForeground
        {
            get { return ProblemMemoryout == 0 ? Brushes.Black : Brushes.Red; } 
        }
        public double TimeOut
        {
            get
            {
                return result.Max(e => e.Runtime);
            } 
        }
        public double MemoryOut
        {
            get
            {
                return result.Max(e => Math.Abs(e.MemorySizeMB));
            }
        }
        public string Machine
        {
            get;
        }
        public string Parameters
        {
            get;
        }
        public string Group
        {
            get;
        }
        public string Creator
        {
            get { return statusVm.Creator; }
        }
        public string Note
        {
            get { return statusVm.Note; }
            set
            {
                statusVm.Note = value;
            }
        }
        public string MachineStatus
        {
            get { return MachineStatuses[1]; }
        }
        public Brush MachineStatusForeground
        {
            get { return Brushes.Orange; }
        }
        public string Title
        {
            get { return "Experiment #" + id.ToString(); }
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
