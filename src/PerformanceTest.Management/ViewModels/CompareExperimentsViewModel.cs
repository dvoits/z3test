using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using Measurement;
using System.Diagnostics;

namespace PerformanceTest.Management
{
    public class CompareExperimentsViewModel : INotifyPropertyChanged
    {
        private BenchmarkResult[] allResults1, allResults2;
        private ExperimentComparingResultsViewModel[] experiments, allResults;
        private readonly int id1, id2;
        private readonly ExperimentManager manager;
        private readonly IUIService uiService;
        private bool checkIgnorePostfix, checkIgnoreCategory, checkIgnorePrefix;
        private string extension1, extension2, category1, category2, sharedDirectory1, sharedDirectory2;

        public event PropertyChangedEventHandler PropertyChanged;

        public CompareExperimentsViewModel(int id1, int id2, ExperimentDefinition def1, ExperimentDefinition def2, ExperimentManager manager, IUIService message)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.manager = manager;
            this.uiService = message;
            this.id1 = id1;
            this.id2 = id2;
            this.checkIgnoreCategory = false;
            this.checkIgnorePostfix = false;
            this.checkIgnorePrefix = false;
            this.extension1 = "." + def1.BenchmarkFileExtension;
            this.extension2 = "." + def2.BenchmarkFileExtension;
            this.category1 = def1.Category;
            this.category2 = def2.Category; ;
            this.sharedDirectory1 = def1.BenchmarkDirectory;
            this.sharedDirectory2 = def2.BenchmarkDirectory;

            RefreshItemsAsync();
        }
        private void UpdateCompared()
        {
            Stopwatch sw = Stopwatch.StartNew();
            var param = new CheckboxParameters(checkIgnoreCategory, checkIgnorePrefix, checkIgnorePostfix, category1, category2, extension1, extension2, sharedDirectory1, sharedDirectory2);
            var join = InnerJoinOrderedResults(allResults1, allResults2, manager, param, uiService);
            Array.Sort<ExperimentComparingResultsViewModel>(join, (a, b) =>
            {
                double diff = Math.Abs(a.Diff) - Math.Abs(b.Diff);
                if (diff < 0) return 1;
                else if (diff > 0) return -1;
                else return 0;
            });
            CompareItems = allResults = join;

            sw.Stop();
            Trace.WriteLine("inner join: " + sw.ElapsedMilliseconds);
        }

        private static ExperimentComparingResultsViewModel[] InnerJoinOrderedResults(BenchmarkResult[] r1, BenchmarkResult[] r2, ExperimentManager manager, CheckboxParameters param, IUIService uiService)
        {
            int n1 = r1.Length;
            int n2 = r2.Length;
            if (n2 < n1)
            {
                BenchmarkResult[] r = r1; r1 = r2; r2 = r;
                int n = n1; n1 = n2; n2 = n;
            }

            Debug.Assert(n1 <= n2);
            var join = new ExperimentComparingResultsViewModel[Math.Min(n1, n2)];
            if (!param.IsCategoryChecked && param.Category1 != param.Category2) return new ExperimentComparingResultsViewModel[0];
            if (!param.IsPrefixChecked && param.Dir1 != param.Dir2) return new ExperimentComparingResultsViewModel[0];
            int i = 0;
            for (int i1 = 0, i2 = 0; i1 < n1 && i2 < n2;)
            {
                string filename1 = param.Category1 + "\\" + r1[i1].BenchmarkFileName;
                string filename2 = param.Category2 + "\\" + r2[i2].BenchmarkFileName;
                if (param.IsCategoryChecked)
                {
                    filename1 = filename1.Substring(param.Category1.Length + 1, filename1.Length - param.Category1.Length - 1);
                    filename2 = filename2.Substring(param.Category2.Length + 1, filename2.Length - param.Category2.Length - 1);
                }
                if (param.IsPostfixChecked)
                {
                    filename1 = filename1.Substring(0, filename1.Length - param.Ext1.Length);
                    filename2 = filename2.Substring(0, filename2.Length - param.Ext2.Length);
                }
                int cmp = string.Compare(filename1, filename2);
                if (cmp == 0)
                {
                    join[i++] = new ExperimentComparingResultsViewModel(filename1, r1[i1], r2[i2], manager, uiService);
                    i1++; i2++;
                }
                else if (cmp < 0) // ~ r1 < r2
                {
                    i1++;
                }
                else // ~ r1 > r2
                {
                    i2++;
                }
            }
            var join2 = new ExperimentComparingResultsViewModel[i];
            for (; --i >= 0;)
            {
                join2[i] = join[i];
            }
            return join2;
        }

        private void RefreshItemsAsync()
        {
            var handle = uiService.StartIndicateLongOperation("Loading comparison table of 2 experiments...");
            try
            {
                allResults = CompareItems = null;

                var t1 = Task.Run(() => manager.GetResults(id1));
                var t2 = Task.Run(() => manager.GetResults(id2));
                allResults1 = t1.Result;
                allResults2 = t2.Result;

                UpdateCompared();
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
        }
        public ExperimentComparingResultsViewModel[] CompareItems
        {
            get { return experiments; }
            private set { experiments = value; NotifyPropertyChanged(); }
        }
        public string Category1
        {
            get { return category1; }
        }
        public string Category2
        {
            get { return category2; }
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
                UpdateCompared();
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
                UpdateCompared();
            }
        }
        public bool CheckIgnoreCategory
        {
            get { return checkIgnoreCategory; }
            set
            {
                checkIgnoreCategory = !checkIgnoreCategory;
                UpdateCompared();
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
            set
            {
                extension1 = value;
                UpdateCompared();
            }
        }
        public string Extension2
        {
            get { return extension2; }
            set
            {
                extension2 = value;
                UpdateCompared();
            }
        }
        public void FilterResultsByError(int code)
        {
            if (code == 0) CompareItems = allResults.Where(e => e.Sat1 > 0 && e.Sat2 > 0).ToArray(); //both sat
            else if (code == 1) CompareItems = allResults.Where(e => e.Unsat1 > 0 && e.Unsat2 > 0).ToArray(); //both unsat
            else if (code == 2) CompareItems = allResults.Where(e => e.Unknown1 > 0 && e.Unknown2 > 0).ToArray(); //both unknown
            else if (code == 3) CompareItems = allResults.Where(e => e.Sat1 > 0 || e.Sat2 > 0).ToArray(); //one sat
            else if (code == 4) CompareItems = allResults.Where(e => e.Unsat1 > 0 || e.Unsat2 > 0).ToArray(); //one unsat
            else if (code == 5) CompareItems = allResults.Where(e => e.Unknown1 > 0 || e.Unknown2 > 0).ToArray(); //one unknown
            else if (code == 6) CompareItems = allResults.Where(e => e.Status1 == ResultStatus.Bug || e.Status2 == ResultStatus.Bug).ToArray();
            else if (code == 7) CompareItems = allResults.Where(e => e.Status1 == ResultStatus.Error || e.Status2 == ResultStatus.Error).ToArray();
            else if (code == 8) CompareItems = allResults.Where(e => e.Status1 == ResultStatus.Timeout || e.Status2 == ResultStatus.Timeout).ToArray();
            else if (code == 9) CompareItems = allResults.Where(e => e.Status1 == ResultStatus.OutOfMemory || e.Status2 == ResultStatus.OutOfMemory).ToArray();
            else if (code == 10) CompareItems = allResults.Where(e => e.Sat1 > 0 && e.Sat2 == 0 || e.Sat1 == 0 && e.Sat2 > 0).ToArray(); //sat star
            else if (code == 11) CompareItems = allResults.Where(e => e.Unsat1 > 0 && e.Unsat2 == 0 || e.Unsat1 == 0 && e.Unsat2 > 0).ToArray(); //unsat star
            else if (code == 12) CompareItems = allResults.Where(e => e.Sat1 > 0 && e.Sat2 == 0 || e.Sat1 == 0 && e.Sat2 > 0 || e.Unsat1 > 0 && e.Unsat2 == 0 || e.Unsat1 == 0 && e.Unsat2 > 0).ToArray(); //ok star
            else if (code == 13) CompareItems = allResults.Where(e => e.Sat1 > 0 && e.Unsat2 > 0 || e.Unsat1 > 0 && e.Sat2 > 0).ToArray(); //sat/unsat
            else CompareItems = allResults;
        }
        public void FilterResultsByText(string filter)
        {
            if (filter != "")
            {
                var resVm = allResults;
                if (filter == "sat")
                {
                    resVm = resVm.Where(e => Regex.IsMatch(e.Filename, "/^(?:(?!unsat).)*$/")).ToArray();
                }
                CompareItems = resVm.Where(e => e.Filename.Contains(filter)).ToArray();
            }
            else CompareItems = allResults;
        }

        public string Runtime1Title { get { return "Runtime (" + id1.ToString() + ")"; } }
        public string Runtime2Title { get { return "Runtime (" + id2.ToString() + ")"; } }
        public string Status1Title { get { return "Status (" + id1.ToString() + ")"; } }
        public string Status2Title { get { return "Status (" + id2.ToString() + ")"; } }
        public string Exitcode1Title { get { return "ExitCode (" + id1.ToString() + ")"; } }
        public string Exitcode2Title { get { return "ExitCode (" + id2.ToString() + ")"; } }
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
    public class CheckboxParameters
    {
        private readonly bool isCategoryChecked, isPrefixChecked, isPostfixChecked;
        private readonly string category1, category2, ext1, ext2, dir1, dir2;
        public CheckboxParameters(bool isCategoryChecked, bool isPrefixChecked, bool isPostfixChecked, string category1, string category2, string ext1, string ext2, string dir1, string dir2)
        {
            this.isCategoryChecked = isCategoryChecked;
            this.isPrefixChecked = isPrefixChecked;
            this.isPostfixChecked = isPostfixChecked;
            this.category1 = category1;
            this.category2 = category2;
            this.ext1 = ext1;
            this.ext2 = ext2;
            this.dir1 = dir1;
            this.dir2 = dir2;
        }
        public bool IsCategoryChecked { get { return isCategoryChecked;} }
        public bool IsPrefixChecked { get { return isPrefixChecked; } }
        public bool IsPostfixChecked { get { return isPostfixChecked; } }
        public string Category1 { get { return category1; } }
        public string Category2 { get { return category2; } }
        public string Ext1 { get { return ext1; } }
        public string Ext2 { get { return ext2; } }
        public string Dir1 { get { return dir1; } }
        public string Dir2 { get { return dir2; } }
    }
    public class ExperimentComparingResultsViewModel:INotifyPropertyChanged
    {
        private readonly BenchmarkResult result1;
        private readonly BenchmarkResult result2;
        private readonly ExperimentManager manager;
        private readonly string filename;
        private readonly IUIService message;
        public event PropertyChangedEventHandler PropertyChanged;
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

        public string Filename {
            get { return filename; }
        }
        public int ID1 { get { return result1.ExperimentID; } }
        public int ID2 { get { return result2.ExperimentID; } }
        public double Runtime1 { get { return result1.NormalizedRuntime; } }
        public double Runtime2 { get { return result2.NormalizedRuntime; } }
        public ResultStatus Status1 { get { return result1.Status; } }
        public ResultStatus Status2 { get { return result2.Status; } }
        public int Exitcode1 { get { return result1.ExitCode; } }
        public int Exitcode2 { get { return result2.ExitCode; } }
        public double Diff { get { return result1.NormalizedRuntime - result2.NormalizedRuntime; } }
        private int GetProperty(string prop, int n)
        {
            int res = 0;
            if (n == 1) res = result1.Properties.ContainsKey(prop) ? Int32.Parse(result1.Properties[prop]) : 0;
            else res = result2.Properties.ContainsKey(prop) ? Int32.Parse(result2.Properties[prop]) : 0;
            return res;
        }
        public int Sat1
        {
            get { return GetProperty("SAT", 1); }
        }
        public int Unsat1
        {
            get { return GetProperty("UNSAT", 1); }
        }
        public int Unknown1
        {
            get { return GetProperty("UNKNOWN", 1); }
        }
        public int Sat2
        {
            get { return GetProperty("SAT", 2); }
        }
        public int Unsat2
        {
            get { return GetProperty("UNSAT", 2); }
        }
        public int Unknown2
        {
            get { return GetProperty("UNKNOWN", 2); }
        }
        public string StdOut1
        {
            get
            {
                StreamReader reader = new StreamReader(result1.StdOut);
                string text = reader.ReadToEnd();
                return text != "" ? text : "*** NO OUTPUT SAVED ***";
            }
        }
        public string StdErr1
        {
            get
            {
                StreamReader reader = new StreamReader(result1.StdErr);
                string text = reader.ReadToEnd();
                return text != "" ? text : "*** NO OUTPUT SAVED ***";
            }
        }
        public string StdOut2
        {
            get
            {
                StreamReader reader = new StreamReader(result2.StdOut);
                string text = reader.ReadToEnd();
                return text != "" ? text : "*** NO OUTPUT SAVED ***";
            }
        }
        public string StdErr2
        {
            get
            {
                StreamReader reader = new StreamReader(result2.StdErr);
                string text = reader.ReadToEnd();
                return text != "" ? text : "*** NO OUTPUT SAVED ***";
            }
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
