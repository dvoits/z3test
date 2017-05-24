using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PerformanceTest.Management
{
    public class CompareExperimentsViewModel: INotifyPropertyChanged
    {
        private IEnumerable<ExperimentComparingResultsViewModel> allResults;
        private IEnumerable<ExperimentComparingResultsViewModel> experiments;
        private readonly int id1, id2;
        private readonly ExperimentManager manager;
        private readonly IUIService message;
        private bool checkIgnorePostfix, checkIgnoreCategory, checkIgnorePrefix;
        private string extension1, extension2;
        public event PropertyChangedEventHandler PropertyChanged;
        public CompareExperimentsViewModel(int id1, int id2, ExperimentManager manager, IUIService message)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.manager = manager;
            this.message = message;
            this.id1 = id1;
            this.id2 = id2;
            this.checkIgnoreCategory = false;
            this.checkIgnorePostfix = false;
            this.checkIgnorePrefix = false;
            this.extension1 = ".ext1";
            this.extension2 = ".ext1";
            //this.category1 = category1;
            //this.category2 = category2;

            RefreshItemsAsync();
        }
        private async void RefreshItemsAsync()
        {
            allResults = CompareItems = null;

            var res1 = await manager.GetResults(id1);
            var res2 = await manager.GetResults(id2);
            List<ExperimentComparingResultsViewModel> resItems = res1.Join(res2, elem => elem.BenchmarkFileName, elem2 => elem2.BenchmarkFileName,
                (f, s) => new ExperimentComparingResultsViewModel(f.BenchmarkFileName, f, s, manager, message)).ToList();
            
            allResults = CompareItems = resItems.OrderByDescending(q => Math.Abs(q.Diff));
        }
        public IEnumerable<ExperimentComparingResultsViewModel> CompareItems
        {
            get { return experiments; }
            private set { experiments = value; NotifyPropertyChanged(); }
        }
        public string Title
        {
            get { return "Comparison: " + id1.ToString() + " vs. " + id2.ToString(); }
        }
        public bool CheckIgnorePostfix
        {
            get { return checkIgnorePostfix; }
            set
            {
                checkIgnorePostfix = !checkIgnorePostfix;
                NotifyPropertyChanged("EnableFirstExtension");
                NotifyPropertyChanged("EnableSecondExtension");
            }
        }
        public bool CheckIgnorePrefix
        {
            get { return checkIgnorePrefix; }
            set
            {
                checkIgnorePrefix = !checkIgnorePrefix;
            }
        }
        public bool CheckIgnoreCategory
        {
            get { return checkIgnoreCategory; }
            set
            {
                checkIgnoreCategory = !checkIgnoreCategory;
                NotifyPropertyChanged();
            }
        }
        public bool EnableFirstExtension
        {
            get { return checkIgnorePostfix; }
        }
        public bool EnableSecondExtension
        {
            get { return checkIgnorePostfix; }
        }
        public string Extension1
        {
            get { return extension1; }
            set { extension1 = value; NotifyPropertyChanged(); }
        }
        public string Extension2
        {
            get { return extension2; }
            set { extension2 = value; NotifyPropertyChanged(); }
        }
        public async void FilterResultsByError(int code)
        {
            if (code == 0) CompareItems = allResults.Where(e => e.Sat1 > 0 && e.Sat2 > 0).ToArray(); //both sat
            else if (code == 1) CompareItems = allResults.Where(e => e.Unsat1 > 0 && e.Unsat2 > 0).ToArray(); //both unsat
            else if (code == 2) CompareItems = allResults.Where(e => e.Unknown1 > 0 && e.Unknown2 > 0).ToArray(); //both unknown
            else if (code == 3) CompareItems = allResults.Where(e => e.Sat1 > 0 || e.Sat2 > 0).ToArray(); //one sat
            else if (code == 4) CompareItems = allResults.Where(e => e.Unsat1 > 0 || e.Unsat2 > 0).ToArray(); //one unsat
            else if (code == 5) CompareItems = allResults.Where(e => e.Unknown1 > 0 || e.Unknown2 > 0).ToArray(); //one unknown
            else if (code == 6) CompareItems = allResults.Where(e => e.ResultCode1 == 3 || e.ResultCode2 == 3).ToArray(); //bugs
            else if (code == 7) CompareItems = allResults.Where(e => e.ResultCode1 == 4 || e.ResultCode2 == 4).ToArray(); //errors
            else if (code == 8) CompareItems = allResults.Where(e => e.ResultCode1 == 5 || e.ResultCode2 == 5).ToArray(); //timeout
            else if (code == 9) CompareItems = allResults.Where(e => e.ResultCode1 == 6 || e.ResultCode2 == 6).ToArray(); //memout
            else if (code == 10) CompareItems = allResults.Where(e => e.Sat1 > 0 && e.Sat2 == 0 || e.Sat1 == 0 && e.Sat2 > 0).ToArray(); //sat star
            else if (code == 11) CompareItems = allResults.Where(e => e.Unsat1 > 0 && e.Unsat2 == 0 || e.Unsat1 == 0 && e.Unsat2 > 0).ToArray(); //unsat star
            else if (code == 12) CompareItems = allResults.Where(e => e.Sat1 > 0 && e.Sat2 == 0 || e.Sat1 == 0 && e.Sat2 > 0 || e.Unsat1 > 0 && e.Unsat2 == 0 || e.Unsat1 == 0 && e.Unsat2 > 0).ToArray(); //ok star
            else if (code == 13) CompareItems = allResults.Where(e => e.Sat1 > 0 && e.Unsat2 > 0 || e.Unsat1 > 0 && e.Sat2 > 0).ToArray(); //sat/unsat
            else CompareItems = allResults;
        }
        public async void FilterResultsByText(string filter)
        {
            if (filter != "")
            {
                var resVm = allResults;
                if (filter == "sat")
                {
                    resVm = resVm.Where(e => Regex.IsMatch(e.Filename, "/^(?:(?!unsat).)*$/")).ToList();
                }
                CompareItems = resVm.Where(e => e.Filename.Contains(filter)).ToArray();
            }
            else CompareItems = allResults;
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
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
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
        public string StdOut1
        {
            get { return "*** NO OUTPUT SAVED ***"; }
        }
        public string StdErr1
        {
            get { return "*** NO OUTPUT SAVED ***"; }
        }
        public string StdOut2
        {
            get { return "*** NO OUTPUT SAVED ***"; }
        }
        public string StdErr2
        {
            get { return "*** NO OUTPUT SAVED ***"; }
        }
    }
}
