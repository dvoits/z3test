﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using Measurement;

namespace PerformanceTest.Management
{
    public class CompareExperimentsViewModel: INotifyPropertyChanged
    {
        private IEnumerable<BenchmarkResult> allResults1, allResults2;
        private IEnumerable<ExperimentComparingResultsViewModel> experiments, allResults;
        private readonly int id1, id2;
        private readonly ExperimentManager manager;
        private readonly IUIService message;
        private bool checkIgnorePostfix, checkIgnoreCategory, checkIgnorePrefix;
        private string extension1, extension2, category1, category2;//, sharedDirectory1, sharedDirectory2;
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
            this.extension1 = ".smt2";
            this.extension2 = ".smt2";
            
            //this.category1 = "smtlib-latest";
            //this.category2 = "smtlib-latest";
            //this.sharedDirectory1 = "";
            //this.sharedDirectory2 = "";

            RefreshItemsAsync();
        }
        private string modifyFilename (string filename, int n)
        {
            //EnableExtensions PostFix and prefix
            string result = filename;
            if (n == 1)
            {
                if (checkIgnorePostfix && result.EndsWith(extension1))
                {
                    result = result.Substring(0, result.Length - extension1.Length);
                }
                if (checkIgnoreCategory)
                {

                }
                if (checkIgnorePrefix)
                {

                }
            }
            else
            {
                if (checkIgnorePostfix && result.EndsWith(extension2))
                {
                    result = result.Substring(0, result.Length - extension2.Length);
                }
                if (checkIgnoreCategory)
                {

                }
                if (checkIgnorePrefix)
                {

                }
            }
            return result;
        }
        private void UpdateCompared () //на случай другого сравнения
        {
            List<ExperimentComparingResultsViewModel> resItems = allResults1.Join(allResults2, elem => modifyFilename(elem.BenchmarkFileName, 1), elem2 => modifyFilename(elem2.BenchmarkFileName, 2),
                (f, s) => new ExperimentComparingResultsViewModel(f.BenchmarkFileName, f, s, manager, message)).ToList();
            CompareItems = allResults = resItems.OrderByDescending(q => Math.Abs(q.Diff));
        }
        private async void getResults1()
        {
            allResults1 = await manager.GetResults(id1);
        }
        private async void getResults2()
        {
            allResults2 = await manager.GetResults(id2);
        }
        private void RefreshItemsAsync()
        {
            allResults = CompareItems = null;

            Task t1 = Task.Factory.StartNew(getResults1);
            Task t2 = Task.Factory.StartNew(getResults2);
            Task.WaitAll(t1, t2);
            UpdateCompared();
        }
        public IEnumerable<ExperimentComparingResultsViewModel> CompareItems
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
            set { extension1 = value; NotifyPropertyChanged(); }
        }
        public string Extension2
        {
            get { return extension2; }
            set { extension2 = value; NotifyPropertyChanged(); }
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
                    resVm = resVm.Where(e => Regex.IsMatch(e.Filename, "/^(?:(?!unsat).)*$/")).ToList();
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
    }
}
