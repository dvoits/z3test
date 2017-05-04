using Measurement;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using ExperimentID = System.Int32;

namespace PerformanceTest
{
    public class LocalExperimentManager : ExperimentManager
    {
        public static LocalExperimentManager NewExperiments(string experimentsFolder, ReferenceExperiment reference)
        {
            FileStorage storage = FileStorage.Open(experimentsFolder);
            storage.Clear();
            storage.SaveReferenceExperiment(reference);
            LocalExperimentManager manager = new LocalExperimentManager(storage);
            return manager;
        }

        public static LocalExperimentManager OpenExperiments(string experimentsFolder)
        {
            FileStorage storage = FileStorage.Open(experimentsFolder);
            LocalExperimentManager manager = new LocalExperimentManager(storage);
            return manager;
        }


        private readonly ConcurrentDictionary<ExperimentID, ExperimentInstance> runningExperiments;
        private readonly LocalExperimentRunner runner;
        private readonly FileStorage storage;
        private readonly AsyncLazy<double> asyncNormal;
        private int lastId = 0;

        private LocalExperimentManager(FileStorage storage) : base(storage.GetReferenceExperiment())
        {
            if (storage == null) throw new ArgumentNullException("storage");
            this.storage = storage;
            runningExperiments = new ConcurrentDictionary<ExperimentID, ExperimentInstance>();
            runner = new LocalExperimentRunner(storage.Location);
            lastId = storage.MaxExperimentId;

            asyncNormal = new AsyncLazy<double>(this.ComputeNormal);
        }

        public string Directory
        {
            get { return storage.Location; }
        }

        public override async Task<ExperimentID> StartExperiment(ExperimentDefinition definition, string creator = null, string note = null)
        {
            ExperimentID id = Interlocked.Increment(ref lastId);
            DateTime submitted = DateTime.Now;

            double normal = await asyncNormal;

            var results = runner.Enqueue(id, definition, normal);

            int benchmarksLeft = results.Length;
            BenchmarkResult[] benchmarks = new BenchmarkResult[results.Length];

            var resultsWithSave =
                results.Select((task, index) =>
                    task.ContinueWith(benchmark =>
                    {
                        int left = Interlocked.Decrement(ref benchmarksLeft);
                        Trace.WriteLine(String.Format("Benchmark {0} completed, {1} left", index, left));
                        if (benchmark.IsCompleted)
                        {
                            benchmarks[index] = benchmark.Result;
                            if (left == 0)
                            {
                                storage.AddResults(id, benchmarks);
                            }
                            ExperimentInstance val;
                            runningExperiments.TryRemove(id, out val);
                            return benchmark.Result;
                        }
                        else throw benchmark.Exception;
                    }))
                .ToArray();

            ExperimentInstance experiment = new ExperimentInstance(id, definition, resultsWithSave);
            runningExperiments[id] = experiment;

            storage.AddExperiment(id, definition, submitted, creator, note);

            return id;
        }


        public override Task<ExperimentDefinition> GetDefinition(int id)
        {
            ExperimentInstance experiment;
            if (runningExperiments.TryGetValue(id, out experiment))
            {
                return Task.FromResult(experiment.Definition);
            }
            ExperimentsTableRow row;
            if (storage.GetExperiments().TryGetValue(id, out row))
            {
                return Task.FromResult(RowToDefinition(row));
            }
            else throw new ArgumentException(string.Format("Experiment {0} not found", id));
        }

        public override async Task<IEnumerable<ExperimentStatus>> GetStatus(IEnumerable<int> ids)
        {
            List<ExperimentStatus> status = new List<ExperimentStatus>();
            var experiments = storage.GetExperiments();
            foreach (var id in ids)
            {
                ExperimentsTableRow expRow = experiments[id];
                var st = new ExperimentStatus(id, expRow.Category, expRow.Submitted, expRow.Creator, expRow.Note, expRow.Flag);
                status.Add(st);
            }
            return status;
        }
        
        public override void DeleteExperiment (int id)
        {
            //not implemented
        }
        public override Task UpdateStatusFlag (int id, bool flag)
        {
            var newRow = storage.GetExperiments()[id];
            newRow.Flag = flag;
            storage.ReplaceExperimentRow(newRow);
            return Task.FromResult(0);
        }

        public override Task<BenchmarkResult>[] GetResults(int id)
        {
            ExperimentInstance experiment;
            if (runningExperiments.TryGetValue(id, out experiment))
            {
                return experiment.Results;
            }
            return storage.GetResults(id).Select(r => Task.FromResult(r)).ToArray();
        }


        public override Task<IEnumerable<int>> FindExperiments(ExperimentFilter? filter = default(ExperimentFilter?))
        {
            IEnumerable<KeyValuePair<int, ExperimentsTableRow>> experiments = storage.GetExperiments().ToArray();

            if (filter.HasValue)
            {
                experiments =
                    experiments
                    .Where(q =>
                    {
                        var id = q.Key;
                        var e = q.Value;
                        return (filter.Value.BenchmarkContainerEquals == null || e.BenchmarkContainer == filter.Value.BenchmarkContainerEquals) &&
                                    (filter.Value.CategoryEquals == null || e.Category == null || e.Category.Contains(filter.Value.CategoryEquals)) &&
                                    (filter.Value.ExecutableEquals == null || e.Executable == null || e.Executable == filter.Value.ExecutableEquals) &&
                                    (filter.Value.ParametersEquals == null || e.Parameters == null || e.Parameters == filter.Value.ParametersEquals) &&
                                    (filter.Value.NotesEquals == null || e.Note  == null || e.Note.Contains(filter.Value.NotesEquals)); //&&
                                    //(filter.Value.CreatorEquals == null || e.Creator.Contains(filter.Value.CreatorEquals));
                    });
            }

            return Task.FromResult(experiments.Select(e => e.Key));
        }

        private async Task<double> ComputeNormal()
        {
            var benchmarks = await Task.WhenAll(runner.Enqueue(-1, reference.Definition, 1.0, reference.Repetitions));
            var m = benchmarks.Sum(b => b.Measurements.TotalProcessorTime.TotalSeconds);
            double n = reference.ReferenceValue / m;
            Trace.WriteLine(String.Format("Median reference duration: {0}, normal: {1}", m, n));
            return n;
        }

        private static ExperimentDefinition RowToDefinition(ExperimentsTableRow row)
        {
            return ExperimentDefinition.Create(
                row.Executable, row.BenchmarkContainer,
                row.BenchmarkFileExtension, row.Parameters,
                TimeSpan.FromSeconds(row.BenchmarkTimeout), row.Category);
        }
    }
    

    public class ExperimentInstance
    {
        private readonly ExperimentID id;
        private readonly ExperimentDefinition def;

        private Task<BenchmarkResult>[] results;

        public ExperimentInstance(ExperimentID id, ExperimentDefinition def, Task<BenchmarkResult>[] results)
        {
            if (def == null) throw new ArgumentNullException("def");
            this.id = id;
            this.def = def;

            this.results = results;
        }

        public ExperimentDefinition Definition { get { return def; } }

        public Task<BenchmarkResult>[] Results { get { return results; } }
    }

}
