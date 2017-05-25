﻿using Angara.Data;
using Angara.Data.DelimitedFile;
using Measurement;
using Microsoft.FSharp.Core;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PerformanceTest
{
    public static class BenchmarkResultsStorage
    {
        public static void SaveBenchmarks(BenchmarkResult[] benchmarks, Stream stream)
        {
            var length = FSharpOption<int>.Some(benchmarks.Length);
            List<Column> columns = new List<Column>()
            {
                Column.Create("BenchmarkFileName", benchmarks.Select(b => b.BenchmarkFileName), length),
                Column.Create("AcquireTime", benchmarks.Select(b => b.AcquireTime.ToString(System.Globalization.CultureInfo.InvariantCulture)), length),
                Column.Create("NormalizedRuntime", benchmarks.Select(b => b.NormalizedRuntime), length),
                Column.Create("TotalProcessorTime", benchmarks.Select(b => b.TotalProcessorTime.TotalSeconds), length),
                Column.Create("WallClockTime", benchmarks.Select(b => b.WallClockTime.TotalSeconds), length),
                Column.Create("PeakMemorySizeMB", benchmarks.Select(b => b.PeakMemorySizeMB), length),
                Column.Create("Status", benchmarks.Select(b => StatusToString(b.Status)), length),
                Column.Create("ExitCode", benchmarks.Select(b => b.ExitCode), length),
                Column.Create("StdOut", benchmarks.Select(b => Utils.StreamToString(b.StdOut, true)), length),
                Column.Create("StdErr", benchmarks.Select(b => Utils.StreamToString(b.StdErr, true)), length),
                Column.Create("WorkerInformation", benchmarks.Select(b => b.WorkerInformation), length),
            };

            HashSet<string> props = new HashSet<string>();
            foreach (var b in benchmarks)
                foreach (var p in b.Properties.Keys)
                    props.Add(p);
            foreach (var p in props)
            {
                string key = p;
                Column c = Column.Create(key, benchmarks.Select(b =>
                {
                    string val = null;
                    b.Properties.TryGetValue(key, out val);
                    return val;
                }), length);
                columns.Add(c);
            }
            var table = Table.OfColumns(columns);
            table.SaveUTF8Bom(stream, new WriteSettings(Delimiter.Comma, true, true));
        }

        public static BenchmarkResult[] LoadBenchmarks(int expId, Stream stream)
        {
            var table = Table.Load(new StreamReader(stream), new ReadSettings(Delimiter.Comma, true, true, FSharpOption<int>.None,
                FSharpOption<FSharpFunc<Tuple<int, string>, FSharpOption<Type>>>.Some(FSharpFunc<Tuple<int, string>, FSharpOption<Type>>.FromConverter(tuple => FSharpOption<Type>.Some(typeof(string))))));

            var fileName = table["BenchmarkFileName"].Rows.AsString;
            var acq = table["AcquireTime"].Rows.AsString;
            var norm = table["NormalizedRuntime"].Rows.AsString;
            var runtime = table["TotalProcessorTime"].Rows.AsString;
            var wctime = table["WallClockTime"].Rows.AsString;
            var mem = table["PeakMemorySizeMB"].Rows.AsString;
            var stat = table["Status"].Rows.AsString;
            var exitcode = table["ExitCode"].Rows.AsString;
            var stdout = table["StdOut"].Rows.AsString;
            var stderr = table["StdErr"].Rows.AsString;
            var worker = table["WorkerInformation"].Rows.AsString;

            var propColumns =
                (from c in table
                 where
                    c.Name != "BenchmarkFileName" &&
                    c.Name != "AcquireTime" &&
                    c.Name != "NormalizedRuntime" &&
                    c.Name != "TotalProcessorTime" &&
                    c.Name != "WallClockTime" &&
                    c.Name != "PeakMemorySizeMB" &&
                    c.Name != "Status" &&
                    c.Name != "ExitCode" &&
                    c.Name != "StdOut" &&
                    c.Name != "StdErr" &&
                    c.Name != "WorkerInformation"
                 select Tuple.Create(c.Name, c.Rows.AsString))
                .ToArray();

            BenchmarkResult[] results = new BenchmarkResult[table.RowsCount];
            for (int i = 0; i < results.Length; i++)
            {
                Dictionary<string, string> props = new Dictionary<string, string>(propColumns.Length);
                foreach (var pc in propColumns)
                {
                    if(pc.Item2 != null) {
                        props[pc.Item1] = pc.Item2[i];
                    }
                }

                results[i] = new BenchmarkResult(
                    expId, fileName[i], worker[i], DateTime.Parse(acq[i], System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(norm[i]), TimeSpan.FromSeconds(double.Parse(runtime[i])), TimeSpan.FromSeconds(double.Parse(wctime[i])), double.Parse(mem[i]),
                    StatusFromString(stat[i]), int.Parse(exitcode[i]), Utils.StringToStream(stdout[i]), Utils.StringToStream(stderr[i]),
                    props);
            }
            return results;
        }

        public static string StatusToString(ResultStatus status)
        {
            return status.ToString();
        }

        public static ResultStatus StatusFromString(string status)
        {
            return (ResultStatus)Enum.Parse(typeof(ResultStatus), status);
        }

    }
}
