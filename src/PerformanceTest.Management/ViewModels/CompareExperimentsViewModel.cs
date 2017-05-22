using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    class CompareExperimentsViewModel
    {
        private IEnumerable<ExperimentsResultsViewModel> experiments;
        private readonly int id1, id2;
        private readonly ExperimentManagerViewModel manager;
        private readonly IUIService message;

        public CompareExperimentsViewModel(int id1, int id2, ExperimentManagerViewModel manager, IUIService message)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.manager = manager;
            this.message = message;
            this.id1 = id1;
            this.id2 = id2;
        }
        public IEnumerable<ExperimentsResultsViewModel> CompareItems
        {
            get { return experiments; }
            private set { experiments = value; }
        }
        public string Title
        {
            get { return "Comparison: " + id1.ToString() + " vs. " + id2.ToString(); }
        }
        public string Runtime1Title { get { return "Runtime (" + id1.ToString() + ")"; } }
        public string Runtime2Title { get { return "Runtime (" + id2.ToString() + ")"; } }
        public string ResultCode1Title { get { return "ResultCode (" + id1.ToString() + ")"; } }
        public string ResultCode2Title { get { return "ResultCode (" + id2.ToString() + ")"; } }
        public string ReturnValue1Title { get { return "Returnvalue (" + id1.ToString() + ")"; } }
        public string ReturnValue2Title { get { return "Returnvalue (" + id2.ToString() + ")"; } }
        public string Sat1Title { get { return "SAT (" + id1.ToString() + ")"; } }
        public string Sat2Title { get { return "SAT (" + id2.ToString() + ")"; } }
        public string Unsat1Title { get { return "UNSAT (" + id1.ToString() + ")"; } }
        public string Unsat2Title { get { return "UNSAT (" + id2.ToString() + ")"; } }
        public string Unknown1Title { get { return "UNKNOWN (" + id1.ToString() + ")"; } }
        public string Unknown2Title { get { return "UNKNOWN (" + id2.ToString() + ")"; } }
    }
    public class ExperimentsResultsViewModel
    {
        private readonly ExperimentStatus status1;
        private readonly ExperimentStatus status2;
        private readonly ExperimentManager manager;
        private readonly IUIService message;

        public ExperimentsResultsViewModel(ExperimentStatus status1, ExperimentStatus status2, ExperimentManager manager, IUIService message)
        {
            if (status1 == null || status2 == null) throw new ArgumentNullException("status");
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.status1 = status1;
            this.status2 = status2;
            this.manager = manager;
            this.message = message;
        }

        public int ID1 { get { return status1.ID; } }
        public int ID2 { get { return status2.ID; } }
        public double Runtime1 { get { return 0.0; } }
        public double Runtime2 { get { return 0.0; } }
        public int ResultCode1 { get { return 0; } }
        public int ResultCode2 { get { return 0; } }
        public int ReturnValue1 { get { return 0; } }
        public int ReturnValue2 { get { return 0; } }
        public double Diff { get { return 0.0; } }
        public int Sat1
        {
            get { return 0; }
        }
        public int Unsat1
        {
            get { return 0; }
        }
        public int Unknown1
        {
            get { return 0; }
        }
        public int Sat2
        {
            get { return 0; }
        }
        public int Unsat2
        {
            get { return 0; }
        }
        public int Unknown2
        {
            get { return 0; }
        }
    }
}
