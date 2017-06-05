using Measurement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{

    class CSVDatum
    {
        public int? rv = null;
        public double runtime = 0.0;
        public int sat = 0, unsat = 0, unknown = 0; 
    }
    public class SaveData
    {
        SaveData ()
        {
        }
        private static async Task<ExperimentStatistics> GetStatistics(ExperimentManager manager, int id, Measurement.Domain domain)
        {
            var results = await manager.GetResults(id);
            var aggr = domain.Aggregate(results.Select(r => new Measurement.ProcessRunAnalysis(r.Status, r.Properties)));
            return new ExperimentStatistics(aggr);
        }

        private static List<int> computeUnique(ExperimentStatusViewModel[] experiments, BenchmarkResult[][] b)
        {
            List<int> res = new List<int>();


            for (int i = 0; i < experiments.Length; i++)
            {
                List<string> filenames = new List<string>();
                for (int j = 0; j < experiments.Length; j++)
                {

                }



                res.Add(filenames.Count);
            }
            return res;
        }
        public static async void SaveMetaCSV (string filename, ExperimentStatusViewModel[] experiments, ExperimentManager manager, IDomainResolver domainResolver, IUIService uiService)
        {
            if (filename == null) throw new ArgumentNullException("filename");
            if (experiments == null) throw new ArgumentNullException("experiments");
            if (manager == null) throw new ArgumentNullException("manager");
            if (domainResolver == null) throw new ArgumentNullException("domain");
            if (uiService == null) throw new ArgumentNullException("uiService");

            StreamWriter f = new StreamWriter(filename, false);
            f.WriteLine("\"ID\",\"# Total\",\"# SAT\",\"# UNSAT\",\"# UNKNOWN\",\"# Timeout\",\"# Memout\",\"# Bug\",\"# Error\",\"# Unique\",\"Parameters\",\"Note\"");
            var count = experiments.Length;
            BenchmarkResult[][] b = new BenchmarkResult[count][];
            b = await DownloadResultsAsync(experiments, manager);
            var unique = computeUnique(experiments, b);
            var handle = uiService.StartIndicateLongOperation("Save meta csv...");
            try
            {
                for (var i = 0; i < count; i++)
                {
                    var domain = domainResolver.GetDomain(experiments[i].Definition.DomainName ?? "Z3");
                    var t1 = Task.Run(() => GetStatistics(manager, experiments[i].ID, domain));
                    var statistics = await t1;
                    var def = experiments[i].Definition;
                    string ps = def.Parameters.Trim(' ');
                    string note = experiments[i].Note.Trim(' ');
                    int? sat = statistics == null ? null : (int?)int.Parse(statistics.AggregatedResults.Properties["SAT"]);
                    int? unsat = statistics == null ? null : (int?)int.Parse(statistics.AggregatedResults.Properties["UNSAT"]);
                    int? unknown = statistics == null ? null : (int?)int.Parse(statistics.AggregatedResults.Properties["UNKNOWN"]);
                    int? bugs = statistics == null ? null : (int?)statistics.AggregatedResults.Bugs;
                    int? errors = statistics == null ? null : (int?)statistics.AggregatedResults.Errors;
                    int? timeouts = statistics == null ? null : (int?)statistics.AggregatedResults.Timeouts;
                    int? memouts = statistics == null ? null : (int?)statistics.AggregatedResults.MemoryOuts;

                    f.WriteLine(experiments[i].ID + "," +
                                experiments[i].BenchmarksTotal + "," +
                                sat + "," +
                                unsat + "," +
                                unknown + "," +
                                timeouts + "," +
                                memouts + "," +
                                bugs + "," +
                                errors + "," +
                                unique[i] + "," +
                                "\"" + ps + "\"," +
                                "\"" + note + "\"");
                }
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
            f.WriteLine();
            f.Close();
        }
        public static async void SaveCSV (string filename, ExperimentStatusViewModel[] experiments, ExperimentManager manager, IUIService uiService)
        {
            if (filename == null) throw new ArgumentNullException("filename");
            if (experiments == null) throw new ArgumentNullException("experiments");
            if (manager == null) throw new ArgumentNullException("manager");
            if (uiService == null) throw new ArgumentNullException("uiService");

            StreamWriter f = new StreamWriter(filename, false);
            var count = experiments.Length;
            Dictionary<string, Dictionary<int, CSVDatum>> data =
                    new Dictionary<string, Dictionary<int, CSVDatum>>();
            var handle = uiService.StartIndicateLongOperation("Save csv...");
            try
            {
                f.Write(",");
                for (var i = 0; i < count; i++)
                {
                    int id = experiments[i].ID;
                    var def = experiments[i].Definition;
                    string ps = def.Parameters.Trim(' ');
                    string note = experiments[i].Note.Trim(' ');
                    var ex_timeout = def.BenchmarkTimeout.TotalSeconds;

                    f.Write(experiments[i].Note.Trim(' ') + ",");
                    if (ps != "") f.Write("'" + ps + "'");
                    f.Write(",,,,");

                    double error_line = 10.0 * ex_timeout;
                    var t1 = Task.Run(() => manager.GetResults(id));
                    var benchmarks = await t1;
                    bool HasDuplicates = false;
                    for (var j = 0; j < benchmarks.Length; j++)
                    {
                        BenchmarkResult b = benchmarks[j];
                        CSVDatum cur = new CSVDatum();
                        cur.rv = b.ExitCode.Equals(DBNull.Value) ? null : (int?)b.ExitCode;
                        cur.runtime = b.NormalizedRuntime.Equals(DBNull.Value) ? ex_timeout : b.NormalizedRuntime;
                        cur.sat = Int32.Parse(b.Properties["SAT"]);
                        cur.unsat = Int32.Parse(b.Properties["UNSAT"]);
                        cur.unknown = Int32.Parse(b.Properties["UNKNOWN"]);

                        bool rv_ok = b.Status != ResultStatus.Error &&
                                     (b.Status == ResultStatus.Timeout && cur.rv == null ||
                                     (b.Status == ResultStatus.Success && (cur.rv == 0 || cur.rv == 10 || cur.rv == 20)));

                        if (cur.sat == 0 && cur.unsat == 0 && !rv_ok) cur.runtime = error_line;
                        if (cur.runtime < 0.01) cur.runtime = 0.01;

                        if (!data.ContainsKey(b.BenchmarkFileName)) data.Add(b.BenchmarkFileName, new Dictionary<int, CSVDatum>());
                        if (data[b.BenchmarkFileName].ContainsKey(id))
                            HasDuplicates = true;
                        else
                            data[b.BenchmarkFileName].Add(id, cur);
                    }
                    if (HasDuplicates)
                        uiService.ShowWarning(String.Format("Duplicates in experiment #{0} ignored", id), "Duplicate warning");
                }
                f.WriteLine();

                // Write headers
                f.Write(",");
                for (int i = 0; i < count; i++)
                {
                    int id = experiments[i].ID;
                    f.Write("R" + id + ",T" + id + ",SAT" + id + ",UNSAT" + id + ",UNKNOWN" + id + ",");
                }
                f.WriteLine();

                // Write data.
                foreach (KeyValuePair<string, Dictionary<int, CSVDatum>> d in data.OrderBy(x => x.Key))
                {
                    bool skip = false;
                    for (int i = 0; i < count; i++)
                    {
                        int id = experiments[i].ID;
                        if (!d.Value.ContainsKey(id) || d.Value[id] == null)
                            skip = true;
                    }
                    if (skip)
                        continue;

                    f.Write(d.Key + ",");

                    for (int i = 0; i < count; i++)
                    {
                        int id = experiments[i].ID;

                        if (!d.Value.ContainsKey(id) || d.Value[id] == null)
                            f.Write("MISSING,,,,");
                        else
                        {
                            CSVDatum c = d.Value[id];
                            f.Write(c.rv + ",");
                            f.Write(c.runtime + ",");
                            f.Write(c.sat + ",");
                            f.Write(c.unsat + ",");
                            f.Write(c.unknown + ",");
                        }
                    }
                    f.WriteLine();
                }
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
            f.Close();
        }
        public static async void SaveOutput (string selectedPath, ExperimentStatusViewModel experiment, ExperimentManager manager, IUIService uiService)
        {
            if (experiment == null) throw new ArgumentNullException("experiment");
            if (manager == null) throw new ArgumentNullException("manager");
            if (uiService == null) throw new ArgumentNullException("uiService");

            var handle = uiService.StartIndicateLongOperation("Save output...");
            try
            {
                string drctry = string.Format(@"{0}\{1}", selectedPath, experiment.ID.ToString());
                double total = 0.0;
                Directory.CreateDirectory(drctry);
                var benchs = await Task.Run(() => manager.GetResults(experiment.ID));
                var benchsVm = benchs.Select(e => new BenchmarkResultViewModel(e, manager, uiService)).ToArray();
                total = benchsVm.Length;

                for (int i = 0; i < total; i++)
                {
                    UTF8Encoding enc = new UTF8Encoding();
                    string stdout = await benchsVm[i].GetStdOutAsync(false);
                    string stderr = await benchsVm[i].GetStdErrAsync(false);
                    string path = drctry + @"\" + benchsVm[i].Filename;
                    Directory.CreateDirectory(path.Substring(0, path.LastIndexOf(@"\")));
                    if (stdout != null && stdout.Length > 0)
                    {
                        FileStream stdoutf = File.Open(path + ".out.txt", FileMode.OpenOrCreate);
                        stdoutf.Write(enc.GetBytes(stdout), 0, enc.GetByteCount(stdout));
                        stdoutf.Close();
                    }

                    if (stderr != null && stderr.Length > 0)
                    {
                        FileStream stderrf = File.Open(path + ".err.txt", FileMode.OpenOrCreate);
                        stderrf.Write(enc.GetBytes(stderr), 0, enc.GetByteCount(stderr));
                        stderrf.Close();
                    }
                }

            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }


        }
        private static void MakeMatrix(ExperimentStatusViewModel[] experiments, BenchmarkResult[][] b, StreamWriter f, int condition, string name)
        {
            int numItems = experiments.Length;
            f.WriteLine(@"\begin{table}");
            f.WriteLine(@"  \centering");
            f.Write(@"  \begin{tabular}[h]{|p{8cm}|");
            for (int i = 0; i < numItems; i++)
                f.Write(@"c|");
            f.WriteLine(@"}\cline{2-" + (numItems + 1) + "}");

            // Header line
            f.Write(@"    \multicolumn{1}{c|}{}");
            for (int i = 0; i < numItems; i++)
            {
                string label = experiments[i].Note.Replace(@"\", @"\textbackslash ").Replace(@"_", @"\_");
                f.Write(@" & \multicolumn{1}{l|}{\rotatebox[origin=c]{90}{\parbox{8cm}{" + label + @"}}}");
            }
            f.WriteLine(@"\\\hline\hline");
            

            int example_value = 0;

            for (int i = 0; i < numItems; i++)
            {
                string label = experiments[i].Note.Replace(@"\", @"\textbackslash ").Replace(@"_", @"\_");
                f.Write(@"    " + label); 
                for (int j = 0; j < numItems; j++)
                {
                    if (i == j)
                        f.Write(@" & $\pm 0$");
                    else
                    {
                        int q = FindSimilarBenchmarks(b[i], b[j], condition);
                        f.Write(@" & $" + (q > 0 ? @"+" : (q == 0) ? @"\pm" : @"") + q.ToString() + "$");
                        if (i == 1 && j == 0) example_value = q;
                    }
                }

                f.WriteLine(@"\\\hline");
            }

            f.WriteLine(@"  \end{tabular}");
            f.Write(@"  \caption{\label{tbl:mtrx} " + name + " Matrix. ");
            string label1 = experiments[1].Note.Replace(@"\", @"\textbackslash ").Replace(@"_", @"\_");
            string label0 = experiments[0].Note.Replace(@"\", @"\textbackslash ").Replace(@"_", @"\_");
            f.Write(@"For instance, '" + label1 + "' outperforms '" + label0 + "' on " + example_value + " benchmarks. ");
            f.WriteLine(@"}");
            f.WriteLine(@"\end{table}");
        }
        public static async void SaveMatrix(string filename, ExperimentStatusViewModel[] experiments, ExperimentManager manager, IUIService uiService)
        {
            if (filename == null) throw new ArgumentNullException("filename");
            if (experiments == null) throw new ArgumentNullException("experiments");
            if (manager == null) throw new ArgumentNullException("manager");
            if (uiService == null) throw new ArgumentNullException("uiService");
            var handle = uiService.StartIndicateLongOperation("Save tex...");
            try
            {
                using (StreamWriter f = new StreamWriter(filename, false))
                {
                    f.WriteLine("% -*- mode: latex; TeX-master: \"main.tex\"; -*-");
                    f.WriteLine();
                    f.WriteLine(@"\documentclass{article}");
                    f.WriteLine(@"\usepackage{multirow}");
                    f.WriteLine(@"\usepackage{rotating}");
                    f.WriteLine(@"\begin{document}");
                    int count = experiments.Length;
                    BenchmarkResult[][] b = new BenchmarkResult[count][];
                    b = await DownloadResultsAsync(experiments, manager);

                    MakeMatrix(experiments, b, f, 0, "SAT+UNSAT");
                    MakeMatrix(experiments, b, f, 1, "SAT");
                    MakeMatrix(experiments, b, f, 2, "UNSAT");

                    f.WriteLine(@"\end{document}");
                }
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
        }
        public static void SaveBinary(string filename, ExperimentStatusViewModel experiment, ExperimentManager manager, IUIService uiService)
        {
            if (filename == null) throw new ArgumentNullException("filename");
            if (experiment == null) throw new ArgumentNullException("experiment");
            if (manager == null) throw new ArgumentNullException("manager");
            if (uiService == null) throw new ArgumentNullException("uiService");


            throw new NotImplementedException();
            //var handle = uiService.StartIndicateLongOperation("Save tex...");
            //try
            //{

            //    string executable_path = experiment.Definition.Executable;
            //    FileStream file = File.Open(filename, FileMode.OpenOrCreate, FileAccess.Write);

            //    //if ()
            //    //{
            //    //    byte[] data = ;
            //    //    file.Write(data, 0, data.Length);
            //    //}
            //    //else
            //    //    uiService.ShowError("Could not get binary data");
            //    file.Close();
            //}
            //finally
            //{
            //    uiService.StopIndicateLongOperation(handle);
            //}
        }
        private static async Task<BenchmarkResult[][]> DownloadResultsAsync(ExperimentStatusViewModel[] experiments, ExperimentManager manager)
        {
            var count = experiments.Length;
            var t = new Task<BenchmarkResult[]>[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                t[index] = Task.Run(() => manager.GetResults(experiments[index].ID));
            }
            var b = new BenchmarkResult[count][];
            for (int j = 0; j < count; j++)
            {
                int index = j;
                b[index] = await t[index];
            }
            return b;
        }
        private static bool ConditionTrue(int condition, BenchmarkResult elem1, BenchmarkResult elem2)
        {
            bool condition1 = elem1.BenchmarkFileName == elem2.BenchmarkFileName && elem1.ExitCode == 0 && elem2.ExitCode != 0 ||
                              elem1.Status == ResultStatus.Success && elem2.Status == ResultStatus.Success && elem1.NormalizedRuntime < elem2.NormalizedRuntime;

            if (condition == 0) condition1 = condition1 && (Int32.Parse(elem1.Properties["UNSAT"]) + Int32.Parse(elem2.Properties["UNSAT"]) > 0 ||
                                                            Int32.Parse(elem1.Properties["SAT"]) + Int32.Parse(elem2.Properties["SAT"]) > 0);
            if (condition == 1) condition1 = condition1 && (Int32.Parse(elem1.Properties["SAT"]) + Int32.Parse(elem2.Properties["SAT"]) > 0);
            if (condition == 2) condition1 = condition1 && (Int32.Parse(elem1.Properties["UNSAT"]) + Int32.Parse(elem2.Properties["UNSAT"]) > 0);
            return condition1;
        }
        private static int FindSimilarBenchmarks (BenchmarkResult[] br1, BenchmarkResult[] br2, int condition)
        {
            int result = 0; 

            for (int i1 = 0, i2 = 0; i1 < br1.Length && i2 < br2.Length;)
            {
                string filename1 = br1[i1].BenchmarkFileName;
                string filename2 = br2[i2].BenchmarkFileName;
                
                int cmp = string.Compare(filename1, filename2);
                if (cmp == 0)
                {
                    if (ConditionTrue(condition, br1[i1], br2[i2])) result++;
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
            return result;
        }
    }
}
