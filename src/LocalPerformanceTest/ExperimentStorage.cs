using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Angara.Data;
using Angara.Data.DelimitedFile;
using System.Diagnostics;
using Microsoft.FSharp.Core;
using Measurement;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PerformanceTest
{
    public class FileStorage
    {
        private static FSharpOption<int> None = FSharpOption<int>.None;


        public static FileStorage Open(string storageName)
        {
            return new FileStorage(storageName);
        }



        private readonly DirectoryInfo dir;
        private readonly DirectoryInfo dirBenchmarks;

        private Table<ExperimentEntity> experimentsTable;


        private FileStorage(string storageName)
        {
            dir = Directory.CreateDirectory(storageName);
            dirBenchmarks = dir.CreateSubdirectory("data");

            string tableFile = Path.Combine(dir.FullName, "experiments.csv");
            if (File.Exists(tableFile))
            {
                experimentsTable =
                    Table.OfRows(
                        Table.Load(tableFile, new ReadSettings(Delimiter.Comma, true, true, None,
                            FSharpOption<FSharpFunc<Tuple<int, string>, FSharpOption<Type>>>.Some(FSharpFunc<Tuple<int, string>, FSharpOption<Type>>.FromConverter(tuple =>
                            {
                                var colName = tuple.Item2;
                                switch (colName)
                                {
                                    case "ID": return FSharpOption<Type>.Some(typeof(int));
                                    case "MemoryLimit": return FSharpOption<Type>.Some(typeof(int));
                                }
                                return FSharpOption<Type>.None;
                            }))))
                        .ToRows<ExperimentEntity>());
            }
            else
            {
                experimentsTable = Table.OfRows<ExperimentEntity>(new ExperimentEntity[0]);
            }
        }

        public string Location
        {
            get { return dir.FullName; }
        }

        public int MaxExperimentId
        {
            get
            {
                if (experimentsTable.RowsCount > 0)
                    return experimentsTable["ID"].Rows.AsInt.Max();
                else return 0;
            }
        }

        public void Clear()
        {
            experimentsTable = Table.OfRows<ExperimentEntity>(new ExperimentEntity[0]);
            dir.Delete(true);
            dir.Create();
            dirBenchmarks.Create();
        }

        public Dictionary<int, ExperimentEntity> GetExperiments()
        {
            var dict = new Dictionary<int, ExperimentEntity>();
            foreach (var row in experimentsTable.Rows)
            { 
                dict[row.ID] = row;
            }
            return dict;
        }

        public void SaveReferenceExperiment(ReferenceExperiment reference)
        {
            string json = JsonConvert.SerializeObject(reference, Formatting.Indented);
            File.WriteAllText(Path.Combine(dir.FullName, "reference.json"), json);
        }

        public ReferenceExperiment GetReferenceExperiment()
        {
            string content = File.ReadAllText(Path.Combine(dir.FullName, "reference.json"));
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ContractResolver = new PrivatePropertiesResolver();
            ReferenceExperiment reference = JsonConvert.DeserializeObject<ReferenceExperiment>(content, settings);
            return reference;
        }


        public BenchmarkResult[] GetResults(int experimentId)
        {
            var bt = Table.Load(IdToTableName(experimentId), new ReadSettings(Delimiter.Comma, true, true, None,
                FSharpOption<FSharpFunc<Tuple<int, string>, FSharpOption<Type>>>.Some(FSharpFunc<Tuple<int, string>, FSharpOption<Type>>.FromConverter(tuple =>
                {
                    var colName = tuple.Item2;
                    switch (colName)
                    {
                        case "PeakMemorySize":
                        case "ExitCode": return FSharpOption<Type>.Some(typeof(int));
                    }
                    return FSharpOption<Type>.None;
                }))))
                .ToRows<BenchmarkResultEntity>();
            return bt.Select(row =>
                new BenchmarkResult(experimentId, row.BenchmarkFileName, row.WorkerInformation, row.NormalizedRuntime, row.AcquireTime,
                    new ProcessRunMeasure(TimeSpan.FromSeconds(row.TotalProcessorTime), TimeSpan.FromSeconds(row.WallClockTime), row.PeakMemorySize << 20,
                        StatusFromString(row.Status), row.ExitCode, AsStream(row.StdOut), AsStream(row.StdErr)))
                ).ToArray();
        }

        public void AddExperiment(int id, ExperimentDefinition experiment, DateTime submitted, string creator, string note)
        {
            experimentsTable = experimentsTable.AddRow(new ExperimentEntity
            {
                ID = id,
                Submitted = submitted,
                Executable = experiment.Executable,
                Parameters = experiment.Parameters,
                BenchmarkContainer = experiment.BenchmarkContainer,
                BenchmarkFileExtension = experiment.BenchmarkFileExtension,
                Category = experiment.Category,
                BenchmarkTimeout = experiment.BenchmarkTimeout.TotalSeconds,
                ExperimentTimeout = experiment.ExperimentTimeout.TotalSeconds,
                MemoryLimit = (int)(experiment.MemoryLimit >> 20), // bytes to MB
                GroupName = experiment.GroupName,
                Note = note,
                Creator = creator
            });
            SaveTable(experimentsTable, Path.Combine(dir.FullName, "experiments.csv"), new WriteSettings(Delimiter.Comma, true, true));
        }

        public void AddResults(int id, BenchmarkResult[] benchmarks)
        {
            SaveBenchmarks(IdToTableName(id), benchmarks);
        }

        public void RemoveExperimentRow(ExperimentEntity deleteRow)
        {
            experimentsTable = Table.OfRows(experimentsTable.Rows.Where(r => r.ID != deleteRow.ID));
            SaveTable(experimentsTable, Path.Combine(dir.FullName, "experiments.csv"), new WriteSettings(Delimiter.Comma, true, true));
        }
        public void ReplaceExperimentRow(ExperimentEntity newRow)
        {
            experimentsTable = Table.OfRows(experimentsTable.Rows.Select(r =>
            {
                return r.ID == newRow.ID ? newRow : r;
            }));
            SaveTable(experimentsTable, Path.Combine(dir.FullName, "experiments.csv"), new WriteSettings(Delimiter.Comma, true, true));
        }

        private string IdToTableName(int id)
        {
            return Path.Combine(dirBenchmarks.FullName, id.ToString("000000") + ".csv");
        }

        private void SaveBenchmarks(string fileName, BenchmarkResult[] benchmarks)
        {
            var table = Table.OfColumns(new[]
            {
                Column.Create("BenchmarkFileName", benchmarks.Select(b => b.BenchmarkFileName), None),
                Column.Create("AcquireTime", benchmarks.Select(b => b.AcquireTime), None),
                Column.Create("NormalizedRuntime", benchmarks.Select(b => b.NormalizedRuntime), None),
                Column.Create("TotalProcessorTime", benchmarks.Select(b => b.Measurements.TotalProcessorTime.TotalSeconds), None),
                Column.Create("WallClockTime", benchmarks.Select(b => b.Measurements.WallClockTime.TotalSeconds), None),
                Column.Create("PeakMemorySize", benchmarks.Select(b => (int)(b.Measurements.PeakMemorySize >> 20)), None),
                Column.Create("Status", benchmarks.Select(b => StatusAsString(b.Measurements.Status)), None),
                Column.Create("ExitCode", benchmarks.Select(b => b.Measurements.ExitCode), None),
                Column.Create("StdOut", benchmarks.Select(b => b.Measurements.OutputToString()), None),
                Column.Create("StdErr", benchmarks.Select(b => b.Measurements.ErrorToString()), None),
                Column.Create("WorkerInformation", benchmarks.Select(b => b.WorkerInformation), None),
            });
            SaveTable(table, fileName, new WriteSettings(Delimiter.Comma, true, true));
        }

        private static void SaveTable(Table table, string fileName, WriteSettings settings)
        {
            using (TextWriter w = new StreamWriter(fileName, false, new UTF8Encoding(true)))
            {
                Table.Save(table, w, settings);
            }
        }

        private static string StatusAsString(Measure.CompletionStatus status)
        {
            return status.ToString();
        }

        private Measure.CompletionStatus StatusFromString(string status)
        {
            return (Measure.CompletionStatus)Enum.Parse(typeof(Measure.CompletionStatus), status);
        }

        private static Stream AsStream(string s)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(s);
            MemoryStream stream = new MemoryStream(byteArray);
            stream.Position = 0;
            return stream;
        }

        internal class PrivatePropertiesResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty prop = base.CreateProperty(member, memberSerialization);
                prop.Writable = true;
                return prop;
            }
        }
    }
}
