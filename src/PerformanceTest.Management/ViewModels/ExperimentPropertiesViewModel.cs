using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PerformanceTest.Management
{
    class ExperimentPropertiesViewModel
    {
        //private ExperimentListViewModel experiments;
        private ExperimentStatusViewModel statusVm;
        private int id;
        private readonly string[] MachineStatuses = { "OK", "Unable to retrieve status." };
        public ExperimentPropertiesViewModel(ExperimentListViewModel experimentsVm, int id)
        {
            //this.experiments = experimentsVm;
            this.id = id;
            this.statusVm = experimentsVm.Items.Where(item => item.ID == id).ToArray()[0];
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
            get { return 0; }
        }
        public int Unsat
        {
            get { return 0; }
        }
        public int Unknown
        {
            get { return 0; }
        }
        public int Overperformed
        {
            get { return 0; }
        }
        public int Underperformed
        {
            get { return 0; }
        }
        public int ProblemBug
        {
            get { return 0; }
        }
        public Brush BugForeground
        {
            get { return Brushes.Black; } //if bugs == 0 return black else red
        }
        public int ProblemNonZero
        {
            get { return 0; }
        }
        public Brush NonZeroForeground
        {
            get { return Brushes.Black; } //if bugs == 0 return black else red
        }
        public int ProblemTimeout
        {
            get { return 0; }
        }
        public Brush TimeoutForeground
        {
            get { return Brushes.Black; } //if bugs == 0 return black else red
        }
        public int ProblemMemoryout
        {
            get { return 0; }
        }
        public Brush MemoryoutForeground
        {
            get { return Brushes.Black; } //if bugs == 0 return black else red
        }
        public int TimeOut
        {
            get ; 
        }
        public int MemoryOut
        {
            get;
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
        public string Locality
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
            
       
    }
}
