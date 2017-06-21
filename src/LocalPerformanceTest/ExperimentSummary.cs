using Angara.Data;
using Angara.Data.DelimitedFile;
using Measurement;
using Microsoft.FSharp.Core;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace PerformanceTest
{
    public static class ExperimentSummaryStorage
    {
        //public static void SaveBenchmarks(BenchmarkResult[] benchmarks, Stream stream)
        //{
        //    var length = FSharpOption<int>.Some(benchmarks.Length);
        //    List<Column> columns = new List<Column>()
        //    {
        //        Column.Create("BenchmarkFileName", benchmarks.Select(b => b.BenchmarkFileName), length),
        //        Column.Create("AcquireTime", benchmarks.Select(b => b.AcquireTime.ToString(System.Globalization.CultureInfo.InvariantCulture)), length),
        //        Column.Create("NormalizedRuntime", benchmarks.Select(b => b.NormalizedRuntime), length),
        //        Column.Create("TotalProcessorTime", benchmarks.Select(b => b.TotalProcessorTime.TotalSeconds), length),
        //        Column.Create("WallClockTime", benchmarks.Select(b => b.WallClockTime.TotalSeconds), length),
        //        Column.Create("PeakMemorySizeMB", benchmarks.Select(b => b.PeakMemorySizeMB), length),
        //        Column.Create("Status", benchmarks.Select(b => StatusToString(b.Status)), length),
        //        Column.Create("ExitCode", benchmarks.Select(b => b.ExitCode.HasValue ? b.ExitCode.ToString() : null), length),
        //        Column.Create("StdOut", benchmarks.Select(b => Utils.StreamToString(b.StdOut, true)), length),
        //        Column.Create("StdErr", benchmarks.Select(b => Utils.StreamToString(b.StdErr, true)), length)
        //    };

        //    HashSet<string> props = new HashSet<string>();
        //    foreach (var b in benchmarks)
        //        foreach (var p in b.Properties.Keys)
        //            props.Add(p);
        //    foreach (var p in props)
        //    {
        //        string key = p;
        //        Column c = Column.Create(key, benchmarks.Select(b =>
        //        {
        //            string val = null;
        //            b.Properties.TryGetValue(key, out val);
        //            return val;
        //        }), length);
        //        columns.Add(c);
        //    }
        //    var table = Table.OfColumns(columns);
        //    table.SaveUTF8Bom(stream, new WriteSettings(Delimiter.Comma, true, true));
        //}


        public static Table Append(Table table, ExperimentSummaryEntity newSummary)
        {
            Dictionary<string, string> newColumns = new Dictionary<string, string>
            {
                { "ID", newSummary.Id.ToString() },
                { "Date", newSummary.Date.ToUniversalTime().ToString("yyyy-MM-dd HH:hh:mm") }
            };
            foreach (var catSummary in newSummary.CategorySummary)
            {
                string cat = catSummary.Key;
                var expSum = catSummary.Value;
                newColumns.Add(string.Join("|", cat, "BUG"), expSum.Bugs.ToString());
                newColumns.Add(string.Join("|", cat, "ERROR"), expSum.Errors.ToString());
                newColumns.Add(string.Join("|", cat, "INFERR"), expSum.InfrastructureErrors.ToString());
                newColumns.Add(string.Join("|", cat, "MEMORY"), expSum.MemoryOuts.ToString());
                newColumns.Add(string.Join("|", cat, "TIMEOUT"), expSum.Timeouts.ToString());

                foreach (var prop in expSum.Properties)
                {
                    string propName = prop.Key;
                    string propVal = prop.Value;
                    newColumns.Add(string.Join("|", cat, propName), propVal);
                }
            }

            List<Column> finalColumns = new List<Column>();
            foreach (var existingColumn in table)
            {
                string newVal;
                ImmutableArray<string> newColArray;
                if (newColumns.TryGetValue(existingColumn.Name, out newVal))
                {
                    newColArray = existingColumn.Rows.AsString.Add(newVal);
                    newColumns.Remove(existingColumn.Name);
                }
                else
                    newColArray = existingColumn.Rows.AsString.Add(string.Empty);

                finalColumns.Add(Column.Create(existingColumn.Name, newColArray, FSharpOption<int>.None));
            }
            foreach (var newColumn in newColumns)
            {
                var array = ImmutableArray.CreateBuilder<string>(table.RowsCount + 1);
                for (int i = table.RowsCount; --i >= 0;)
                    array.Add(string.Empty);
                array.Add(newColumn.Value);

                finalColumns.Add(Column.Create(newColumn.Key, array, FSharpOption<int>.None));
            }

            var finalTable = Table.OfColumns(finalColumns);
            return finalTable;
        }

        public static void Append(Stream source, ExperimentSummaryEntity newSummary, Stream dest)
        {
            var table = Table.Load(new StreamReader(source), new ReadSettings(Delimiter.Comma, false, true, FSharpOption<int>.None,
                FSharpOption<FSharpFunc<Tuple<int, string>, FSharpOption<Type>>>.Some(FSharpFunc<Tuple<int, string>, FSharpOption<Type>>.FromConverter(tuple => FSharpOption<Type>.Some(typeof(string))))));
            var finalTable = Append(table, newSummary);
            Table.Save(finalTable, new StreamWriter(dest));
        }

        //public static ExperimentEntity[] Load(Stream stream)
        //{
        //    var table = Table.Load(new StreamReader(stream), new ReadSettings(Delimiter.Comma, false, true, FSharpOption<int>.None,
        //        FSharpOption<FSharpFunc<Tuple<int, string>, FSharpOption<Type>>>.Some(FSharpFunc<Tuple<int, string>, FSharpOption<Type>>.FromConverter(tuple => FSharpOption<Type>.Some(typeof(string))))));

        //    var date = table["Date"].Rows.AsString;
        //    var id = table["ID"].Rows.AsString;


        //    var header =
        //        table
        //            .Where(c => c.Name.Contains("|"))
        //            .Select(c =>
        //            {
        //                string[] parts = c.Name.Split(new[] { '|' }, 2);
        //                string category = parts[0];
        //                string property = parts[1];
        //                return Tuple.Create(category, property, c.Rows);
        //            })
        //            .GroupBy(t => t.Item1)
        //            .ToDictionary(g => g.Key, g => g.Select(t => Tuple.Create(t.Item2, t.Item3)).ToArray());

        //    var propColumns =
        //        (from c in table
        //         where
        //            c.Name != "Date" &&
        //            c.Name != "ID"
        //         select Tuple.Create(c.Name, c.Rows.AsString))
        //        .ToArray();

        //    var results = new ExperimentSummaryEntity[table.RowsCount];
        //    for (int i = 0; i < results.Length; i++)
        //    {
        //        Dictionary<string, string> props = new Dictionary<string, string>(propColumns.Length);
        //        foreach (var pc in propColumns)
        //        {
        //            if (pc.Item2 != null)
        //            {
        //                props[pc.Item1] = pc.Item2[i];
        //            }
        //        }

        //        results[i] = new ExperimentSummaryEntity(
        //            );
        //    }
        //    return results;
        //}

        public static string StatusToString(ResultStatus status)
        {
            return status.ToString();
        }

        public static ResultStatus StatusFromString(string status)
        {
            return (ResultStatus)Enum.Parse(typeof(ResultStatus), status);
        }

    }

    public class ExperimentSummaryEntity
    {
        public ExperimentSummaryEntity(int id, DateTimeOffset date, IReadOnlyDictionary<string, AggregatedAnalysis> categorySummary)
        {
            if (categorySummary == null) throw new ArgumentNullException(nameof(categorySummary));
            Id = id;
            Date = date;
            CategorySummary = categorySummary;
        }

        public int Id { get; private set; }
        public DateTimeOffset Date { get; private set; }

        public IReadOnlyDictionary<string, AggregatedAnalysis> CategorySummary { get; private set; }
    }

    public class ExperimentSummary
    {
        public static Dictionary<string, AggregatedAnalysis> Build(IEnumerable<BenchmarkResult> results, Domain domain)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (domain == null) throw new ArgumentNullException(nameof(domain));

            var categories = new Dictionary<string, List<BenchmarkResult>>();
            foreach (var result in results)
            {
                int i = result.BenchmarkFileName.IndexOf("/");
                string category = i >= 0 ? result.BenchmarkFileName.Substring(0, i) : string.Empty;

                List<BenchmarkResult> catResults;
                if (!categories.TryGetValue(category, out catResults))
                {
                    catResults = new List<BenchmarkResult>();
                    categories.Add(category, catResults);
                }
                catResults.Add(result);
            }

            var stats = new Dictionary<string, AggregatedAnalysis>(categories.Count);
            foreach (var cr in categories)
            {
                var catSummary = domain.Aggregate(cr.Value.Select(r => new ProcessRunResults(new ProcessRunAnalysis(r.Status, r.Properties), r.NormalizedRuntime)));
                stats.Add(cr.Key, catSummary);
            }
            return stats;
        }
    }
}
