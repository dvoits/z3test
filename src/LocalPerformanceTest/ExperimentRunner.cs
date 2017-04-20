using Measurement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using ExperimentID = System.Int32;

namespace PerformanceTest
{
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

        public Task<BenchmarkResult>[] Enqueue(ExperimentID id, ExperimentDefinition experiment, double normal, int repetitions = 0)
        {
            if (experiment == null) throw new ArgumentNullException("experiment");
            return RunExperiment(id, experiment, factory, normal, repetitions);
        }

        private static Task<BenchmarkResult>[] RunExperiment(ExperimentID id, ExperimentDefinition experiment, TaskFactory factory, double normal, int repetitions = 0)
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

                        int maxCount = repetitions == 0 ? 10 : repetitions;
                        TimeSpan maxTime = TimeSpan.FromSeconds(10);

                        int count = 0;
                        List<ProcessRunMeasure> measures = new List<ProcessRunMeasure>();
                        TimeSpan total = TimeSpan.FromSeconds(0);

                        do
                        {
                            var m = ProcessMeasurer.Measure(experiment.Executable, args, experiment.BenchmarkTimeout, experiment.MemoryLimit == 0 ? null : new Nullable<long>(experiment.MemoryLimit));
                            measures.Add(m);
                            count++;
                            total += m.WallClockTime;
                        } while ((repetitions != 0 || total < maxTime) && count < maxCount);

                        ProcessRunMeasure finalMeasure = Utils.AggregateMeasures(measures.ToArray());
                        Trace.WriteLine(String.Format("Done in {0} (aggregated by {1} runs)", finalMeasure.WallClockTime, count));

                        var performanceIndex = normal * finalMeasure.TotalProcessorTime.TotalSeconds;
                        return new BenchmarkResult(id, benchmark, workerInfo, performanceIndex, acq, finalMeasure);
                    }, benchmarks[i], TaskCreationOptions.LongRunning);
            }

            return results;
        }

        private static string GetWorkerInfo()
        {
            return "";
        }
    }
}