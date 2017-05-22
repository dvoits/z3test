﻿using Measurement;
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
    public sealed class LocalExperimentManager : ExperimentManager
    {
        public static LocalExperimentManager NewExperiments(string experimentsFolder, ReferenceExperiment reference)
        {
            ExperimentDefinition def = MakeRelativeDefinition(experimentsFolder, reference.Definition);
            ReferenceExperiment relRef = new ReferenceExperiment(def, reference.Repetitions, reference.ReferenceValue);

            FileStorage storage = FileStorage.Open(experimentsFolder);
            storage.Clear();
            storage.SaveReferenceExperiment(relRef);
            LocalExperimentManager manager = new LocalExperimentManager(storage);
            return manager;
        }

        public static LocalExperimentManager OpenExperiments(string experimentsFolder)
        {
            FileStorage storage = FileStorage.Open(experimentsFolder);
            LocalExperimentManager manager = new LocalExperimentManager(storage);
            return manager;
        }

        private static ExperimentDefinition MakeRelativeDefinition(string experimentsFolder, ExperimentDefinition def)
        {
            string relExec = Utils.MakeRelativePath(experimentsFolder, def.Executable);
            string relContainer = Utils.MakeRelativePath(experimentsFolder, def.BenchmarkContainer);
            return ExperimentDefinition.Create(relExec, relContainer, def.BenchmarkFileExtension,
                def.Parameters, def.BenchmarkTimeout,
                def.Category, def.MemoryLimit);
        }

        private readonly ReferenceExperiment reference;
        private readonly ConcurrentDictionary<ExperimentID, ExperimentInstance> runningExperiments;
        private readonly LocalExperimentRunner runner;
        private readonly FileStorage storage;
        private readonly AsyncLazy<double> asyncNormal;
        private int lastId = 0;

        private LocalExperimentManager(FileStorage storage) 
        {
            if (storage == null) throw new ArgumentNullException("storage");
            this.storage = storage;
            this.reference = storage.GetReferenceExperiment();

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
            definition = MakeRelativeDefinition(storage.Location, definition);

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
                        if (benchmark.IsCompleted && !benchmark.IsFaulted)
                        {
                            benchmarks[index] = benchmark.Result;
                            if (left == 0)
                            {
                                storage.AddResults(id, benchmarks);
                                ExperimentInstance val;
                                runningExperiments.TryRemove(id, out val);
                            }                            
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
            ExperimentEntity row;
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
                ExperimentEntity expRow = experiments[id];
                int done, total;
                ExperimentInstance experiment;
                if (runningExperiments.TryGetValue(id, out experiment))
                {
                    total = experiment.Results.Length;
                    done = experiment.Results.Count(t => t.IsCompleted);
                }else
                {
                    var results = storage.GetResults(id);
                    done = total = results.Length;                    
                }

                var st = new ExperimentStatus(id, expRow.Category, expRow.Submitted, expRow.Creator, expRow.Note, expRow.Flag, done, total);
                status.Add(st);
            }
            return status;
        }
        
        public override Task DeleteExperiment (int id)
        {
            var deleteRow = storage.GetExperiments()[id];
            storage.RemoveExperimentRow(deleteRow);
            return Task.FromResult(0);
        }
        public override Task UpdatePriority(int id, string priority)
        {
            //var newRow = storage.GetExperiments()[id];
            //newRow.Priority = priority;
            //storage.ReplaceExperimentRow(newRow);
            //return Task.FromResult(0);
            throw new NotImplementedException();
        }
        public override Task UpdateStatusFlag (int id, bool flag)
        {
            var newRow = storage.GetExperiments()[id];
            newRow.Flag = flag;
            storage.ReplaceExperimentRow(newRow);
            return Task.FromResult(0);
        }

        public override Task UpdateNote (int id, string note)
        {
            var newRow = storage.GetExperiments()[id];
            newRow.Note = note;
            storage.ReplaceExperimentRow(newRow);
            return Task.FromResult(0);
        }
        public override async Task<BenchmarkResult[]> GetResults(int id)
        {
            ExperimentInstance experiment;
            if (runningExperiments.TryGetValue(id, out experiment))
            {
                //return experiment.Results;
                return await Task.WhenAll(experiment.Results);
            }
            return storage.GetResults(id).ToArray();
        }


        public override Task<IEnumerable<int>> FindExperiments(ExperimentFilter? filter = default(ExperimentFilter?))
        {
            IEnumerable<KeyValuePair<int, ExperimentEntity>> experiments = storage.GetExperiments().ToArray();

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
                                    (filter.Value.NotesEquals == null || e.Note == null || e.Note.Contains(filter.Value.NotesEquals)) &&
                                    (filter.Value.CreatorEquals == null || e.Creator == null || e.Creator.Contains(filter.Value.CreatorEquals));
                    })
                    .OrderByDescending(q => q.Value.Submitted);
            }

            return Task.FromResult(experiments.OrderByDescending(q => q.Value.Submitted).Select(e => e.Key));
        }

        private async Task<double> ComputeNormal()
        {
            var benchmarks = await Task.WhenAll(runner.Enqueue(-1, reference.Definition, 1.0, reference.Repetitions));
            var m = benchmarks.Sum(b => b.Measurements.TotalProcessorTime.TotalSeconds);
            double n = reference.ReferenceValue / m;
            Trace.WriteLine(String.Format("Median reference duration: {0}, normal: {1}", m, n));
            return n;
        }

        private static ExperimentDefinition RowToDefinition(ExperimentEntity row)
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
