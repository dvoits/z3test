using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public class CompareExperimentsViewModel
    {
        private IEnumerable<ExperimentComparingResultsViewModel> experiments;
        private readonly int id1, id2;
        private readonly ExperimentManager manager;
        private readonly IUIService message;

        public CompareExperimentsViewModel(int id1, int id2, ExperimentManager manager, IUIService message)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.manager = manager;
            this.message = message;
            this.id1 = id1;
            this.id2 = id2;

            RefreshItemsAsync();
        }
        private async void RefreshItemsAsync()
        {
            CompareItems = null;

            var res1 = await manager.GetResults(id1);
            var res2 = await manager.GetResults(id2);
            List<ExperimentComparingResultsViewModel> resItems = new List<ExperimentComparingResultsViewModel>();
            for (var i = 0; i < res1.Length; i++)
            {
                for (var j = 0; j < res2.Length; j++)
                {
                    if (res1[i].BenchmarkFileName == res2[j].BenchmarkFileName)
                        resItems.Add(new ExperimentComparingResultsViewModel(res1[i].BenchmarkFileName, res1[i], res2[j], manager, message));
                }
            }
            CompareItems = resItems;
        }
        public IEnumerable<ExperimentComparingResultsViewModel> CompareItems
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
    public class ExperimentComparingResultsViewModel
    {
        private readonly BenchmarkResult result1;
        private readonly BenchmarkResult result2;
        private readonly ExperimentManager manager;
        private readonly string filename;
        private readonly IUIService message;

        public ExperimentComparingResultsViewModel(string filename, BenchmarkResult res1, BenchmarkResult res2, ExperimentManager manager, IUIService message)
        {
            if (res1 == null || res2 == null) throw new ArgumentNullException("results");
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.result1 = res1;
            this.result2 = res2;
            this.filename = filename;
            this.manager = manager;
            this.message = message;
        }

        public string Filename { get { return filename; } }
        public int ID1 { get { return result1.ExperimentID; } }
        public int ID2 { get { return result2.ExperimentID; } }
        public double Runtime1 { get { return result1.NormalizedRuntime; } }
        public double Runtime2 { get { return result2.NormalizedRuntime; } }
        public int ResultCode1 { get { return 0; } }
        public int ResultCode2 { get { return 0; } }
        public int ReturnValue1 { get { return 0; } }
        public int ReturnValue2 { get { return 0; } }
        public double Diff { get { return result1.NormalizedRuntime - result2.NormalizedRuntime; } }
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
