using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.IO;

using ExperimentID = System.Int32;
using System.Threading;
using System.Threading.Tasks.Schedulers;
using System.Diagnostics;
using Measurement;

namespace PerformanceTest
{
    public class LocalExperimentManager : ExperimentManager
    {
        public static ExperimentManager NewExperiments(string experimentsFolder, ReferenceExperiment reference)
        {
            FileStorage storage = FileStorage.Open(experimentsFolder);
            storage.Clear();
            storage.SaveReferenceExperiment(reference);
            ExperimentManager manager = new LocalExperimentManager(storage);
            return manager;
        }

        public static ExperimentManager OpenExperiments(string experimentsFolder)
        {
            FileStorage storage = FileStorage.Open(experimentsFolder);
            storage.GetExperiments();
            ExperimentManager manager = new LocalExperimentManager(storage);
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
            runner = new LocalExperimentRunner();
            lastId = storage.MaxExperimentId;

            asyncNormal = new AsyncLazy<double>(this.ComputeNormal);
        }

        public string Directory
        {
            get { return storage.Location; }
        }

        public override async Task<ExperimentID> StartExperiment(ExperimentDefinition definition)
        {
            ExperimentID id = Interlocked.Increment(ref lastId);

            double normal = await asyncNormal;
            var results = runner.Enqueue(id, definition, normal);
            ExperimentInstance experiment = new ExperimentInstance(id, definition, results);
            runningExperiments[id] = experiment;

            storage.AddExperiment(id, definition);
            var _ = 
                Task.WhenAll(experiment.Results)
                .ContinueWith(t =>
                {
                    storage.AddResults(id, t.Result);
                    ExperimentInstance val;
                    runningExperiments.TryRemove(id, out val);
                });

            return id;
        }


        public override Task<ExperimentDefinition> GetDefinition(int id)
        {
            return Task.FromResult(GetInstance(id).Definition);
        }

        public override Task<ExperimentStatus> GetStatus(int id)
        {
            throw new NotImplementedException();
        }

        public override Task<BenchmarkResult>[] GetResults(int id)
        {
            return GetInstance(id).Results;
        }

        public override Task<IEnumerable<int>> FindExperiments(ExperimentFilter? filter = default(ExperimentFilter?))
        {
            IEnumerable<ExperimentsTableRow> experiments = storage.GetExperiments();

            if (filter.HasValue)
            {
                experiments =
                    experiments
                    .Where(e => (filter.Value.BenchmarkContainerEquals == null || e.BenchmarkContainer == filter.Value.BenchmarkContainerEquals) &&
                                (filter.Value.CategoryEquals == null || e.Category == filter.Value.CategoryEquals) &&
                                (filter.Value.ExecutableEquals == null || e.Executable == filter.Value.ExecutableEquals) &&
                                (filter.Value.ParametersEquals == null || e.Parameters == filter.Value.ParametersEquals));
            }

            return Task.FromResult(experiments.Select(e => e.ID));
        }

        private ExperimentInstance GetInstance(int id)
        {
            ExperimentInstance experiment;
            if (runningExperiments.TryGetValue(id, out experiment))
            {
                return experiment;
            }
            else throw new ArgumentException("Experiment not found");
        }

        private async Task<double> ComputeNormal()
        {
            double[] results = new double[reference.Repetitions];
            for (var i = 0; i < reference.Repetitions; i++)
            {
                var benchmarks = await Task.WhenAll(runner.Enqueue(-1, reference.Definition, 1.0));
                results[i] = benchmarks.Sum(b => b.Measurements.TotalProcessorTime.TotalSeconds);
            }

            // results has 1 or more elements            
            Array.Sort(results);
            int im = reference.Repetitions >> 1;
            double m;
            if (reference.Repetitions % 2 == 1)
                m = results[im];
            else
                m = 0.5 * (results[im] + results[im - 1]);

            double n = reference.ReferenceValue / m;
            Debug.WriteLine(String.Format("Median reference duration: {0}, normal: {1}", m, n));
            return n;
        }
    }

    public class LocalExperimentRunner
    {
        private readonly LimitedConcurrencyLevelTaskScheduler scheduler;
        private readonly TaskFactory factory;

        public LocalExperimentRunner()
        {
            scheduler = new LimitedConcurrencyLevelTaskScheduler(1);
            factory = new TaskFactory(scheduler);
        }

        public TaskFactory TaskFactory { get { return factory; } }

        public Task<BenchmarkResult>[] Enqueue(ExperimentID id, ExperimentDefinition experiment, double normal)
        {
            if (experiment == null) throw new ArgumentNullException("experiment");
            return RunExperiment(id, experiment, factory, normal);
        }

        private static Task<BenchmarkResult>[] RunExperiment(ExperimentID id, ExperimentDefinition experiment, TaskFactory factory, double normal)
        {
            if (!File.Exists(experiment.Executable)) throw new ArgumentException("Executable not found");

            var workerInfo = GetWorkerInfo();
            string benchmarkFolder = string.IsNullOrEmpty(experiment.Category) ? experiment.BenchmarkContainer : Path.Combine(experiment.BenchmarkContainer, experiment.Category);
            var benchmarks = Directory.EnumerateFiles(benchmarkFolder, "*." + experiment.BenchmarkFileExtension, SearchOption.AllDirectories).ToArray();

            var results = new Task<BenchmarkResult>[benchmarks.Length];
            for (int i = 0; i < benchmarks.Length; i++)
            {
                results[i] =
                    factory.StartNew(_benchmark =>
                    {
                        string benchmark = (string)_benchmark;
                        Trace.WriteLine("Running benchmark " + Path.GetFileName(benchmark));

                        string args = experiment.Parameters;
                        if (args != null)
                        {
                            args = args.Replace("{0}", benchmark);
                        }

                        DateTime acq = DateTime.Now;
                        var m = ProcessMeasurer.Measure(experiment.Executable, args, experiment.BenchmarkTimeout, experiment.MemoryLimit == 0 ? null : new Nullable<long>(experiment.MemoryLimit));
                        Trace.WriteLine(String.Format("Done in {0}", m.WallClockTime));

                        var performanceIndex = normal * m.TotalProcessorTime.TotalSeconds;
                        return new BenchmarkResult(id, benchmark, workerInfo, performanceIndex, acq, m);
                    }, benchmarks[i], TaskCreationOptions.LongRunning);
            }

            return results;
        }

        private static string GetWorkerInfo()
        {
            return "";
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
