using Measurement;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace PerformanceTest.Management
{
    public class ExperimentPropertiesViewModel: INotifyPropertyChanged
    {
        public static async Task<ExperimentPropertiesViewModel> CreateAsync(ExperimentManager manager, int id, IDomainResolver domainResolver, IUIService ui)
        {
            if (manager == null) throw new ArgumentNullException("manager");

            Experiment exp = await manager.TryFindExperiment(id);
            if (exp == null) throw new KeyNotFoundException(string.Format("There is no experiment with id {0}.", id));
            ExperimentStatistics stat = await GetStatistics(manager, id, domainResolver.GetDomain(exp.Definition.DomainName ?? "Z3"));
            return new ExperimentPropertiesViewModel(exp.Definition, exp.Status, stat, manager, ui);
        }


        private static async Task<ExperimentStatistics> GetStatistics(ExperimentManager manager, int id, Measurement.Domain domain)
        {
            var results = await manager.GetResults(id);
            var aggr = domain.Aggregate(results.Select(r => new Measurement.ProcessRunAnalysis(r.Status, r.Properties)));
            return new ExperimentStatistics(aggr);
        }

        private readonly int id;
        private readonly ExperimentDefinition definition;
        private readonly ExperimentStatus status;
        private readonly ExperimentStatistics statistics;
        private readonly string[] MachineStatuses = { "OK", "Unable to retrieve status." };

        private readonly ExperimentManager manager;
        private readonly IUIService ui;

        private string currentNote;

        public event PropertyChangedEventHandler PropertyChanged;

        public ExperimentPropertiesViewModel(ExperimentDefinition def, ExperimentStatus status, ExperimentStatistics stat, ExperimentManager manager, IUIService ui)
        {
            if (def == null) throw new ArgumentNullException("def");
            if (status == null) throw new ArgumentNullException("status");
            if (stat == null) throw new ArgumentNullException("stat");
            if (manager == null) throw new ArgumentNullException("manager");
            if (ui == null) throw new ArgumentNullException("ui");

            this.id = status.ID;
            this.definition = def;
            this.status = status;
            this.statistics = stat;
            this.manager = manager;
            this.ui = ui;

            currentNote = status.Note;
            SubmitNote = new DelegateCommand(
                _ => UpdateNote(),
                _ => status.Note != currentNote);
        }

        public bool NoteChanged
        {
            get { return status.Note != currentNote; }
        }

        public string Note
        {
            get { return currentNote; }
            set
            {
                if (currentNote == value) return;
                currentNote = value;
                NotifyPropertyChanged("NoteChanged");
                NotifyPropertyChanged();
                SubmitNote.RaiseCanExecuteChanged();
            }
        }


        public DelegateCommand SubmitNote { get; private set; }

        public DateTime SubmissionTime
        {
            get { return status.SubmissionTime; }
        }
        public string Category
        {
            get { return definition.Category; }
        }
        public int BenchmarksTotal
        {
            get { return status.BenchmarksTotal; }
        }
        public int BenchmarksDone
        {
            get { return status.BenchmarksDone; }
        }
        public int BenchmarksQueued
        {
            get { return status.BenchmarksQueued; }
        }
        public Brush QueuedForeground
        {
            get
            {
                return (status.BenchmarksQueued == 0) ? Brushes.Green : Brushes.Red;
            }
        }

        private int GetProperty(string prop)
        {
            return int.Parse(statistics.AggregatedResults.Properties[prop]);
        }

        public int Sat
        {
            get
            {
                return GetProperty("SAT");
            }

        }
        public int Unsat
        {
            get
            {
                return GetProperty("UNSAT");
            }
        }
        public int Unknown
        {
            get
            {
                return GetProperty("UNKNOWN");
            }
        }
        public int Overperformed
        {
            get
            {
                return GetProperty("OVERPERFORMED");
            }
        }
        public int Underperformed
        {
            get
            {
                return GetProperty("UNDERPERFORMED");
            }
        }
        public int ProblemBug
        {
            get
            {
                return statistics.AggregatedResults.Bugs;
            }
        }
        public Brush BugForeground
        {
            get { return ProblemBug == 0 ? Brushes.Black : Brushes.Red; }
        }
        public int ProblemNonZero
        {
            get { return statistics.AggregatedResults.Errors; }
        }
        public Brush NonZeroForeground
        {
            get { return ProblemNonZero == 0 ? Brushes.Black : Brushes.Red; }
        }
        public int ProblemTimeout
        {
            get { return statistics.AggregatedResults.Timeouts; }
        }
        public Brush TimeoutForeground
        {
            get { return ProblemTimeout == 0 ? Brushes.Black : Brushes.Red; }
        }
        public int ProblemMemoryout
        {
            get { return statistics.AggregatedResults.MemoryOuts; }
        }
        public Brush MemoryoutForeground
        {
            get { return ProblemMemoryout == 0 ? Brushes.Black : Brushes.Red; }
        }
        public double TimeOut
        {
            get
            {
                return definition.BenchmarkTimeout.TotalSeconds;
            }
        }
        public double MemoryOut
        {
            get
            {
                return definition.MemoryLimitMB;
            }
        }
        public string WorkerInformation
        {
            get;
        }
        public string Parameters
        {
            get;
        }
        public string Creator
        {
            get { return status.Creator; }
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


        private async void UpdateNote()
        {
            try
            {
                await manager.UpdateNote(status.ID, currentNote);
                status.Note = currentNote;
                NotifyPropertyChanged("Note");
                NotifyPropertyChanged("NoteChanged");
                SubmitNote.RaiseCanExecuteChanged();

                Trace.WriteLine("Note changed to '" + currentNote + "' for " + status.ID);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to update experiment note: " + ex.Message);
                currentNote = status.Note;
                NotifyPropertyChanged("Note");
                NotifyPropertyChanged("NoteChanged");
                SubmitNote.RaiseCanExecuteChanged();

                ui.ShowError("Failed to update experiment note: " + ex.Message);
            }
        }
    }
}
