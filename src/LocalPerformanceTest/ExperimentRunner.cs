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
        private readonly string rootFolder;

        public LocalExperimentRunner(string rootFolder)
        {
            if (rootFolder == null) new ArgumentNullException("rootFolder");

            scheduler = new LimitedConcurrencyLevelTaskScheduler(1);
            factory = new TaskFactory(scheduler);
            this.rootFolder = rootFolder;
        }

        public TaskFactory TaskFactory { get { return factory; } }

        public Task<BenchmarkResult>[] Enqueue(ExperimentID id, ExperimentDefinition experiment, double normal, int repetitions = 0)
        {
            if (experiment == null) throw new ArgumentNullException("experiment");
            return RunExperiment(id, experiment, factory, rootFolder, normal, repetitions);
        }

        private static Task<BenchmarkResult>[] RunExperiment(ExperimentID id, ExperimentDefinition experiment, TaskFactory factory, string rootFolder, double normal, int repetitions = 0)
        {
            string executable;
            if (Path.IsPathRooted(experiment.Executable)) executable = experiment.Executable;
            else executable = Path.Combine(rootFolder, experiment.Executable);
            if (!File.Exists(executable)) throw new ArgumentException("Executable not found");

            var workerInfo = GetWorkerInfo();
            string benchmarkFolder = string.IsNullOrEmpty(experiment.Category) ? experiment.BenchmarkContainer : Path.Combine(experiment.BenchmarkContainer, experiment.Category);
            if (!Path.IsPathRooted(benchmarkFolder))
            {
                benchmarkFolder = Path.Combine(rootFolder, benchmarkFolder);
            }
            var benchmarks = Directory.GetFiles(benchmarkFolder, "*." + experiment.BenchmarkFileExtension, SearchOption.AllDirectories);

            var results = new List<Task<BenchmarkResult>>(256);
            foreach (string benchmarkFile in Directory.GetFiles(benchmarkFolder, "*." + experiment.BenchmarkFileExtension, SearchOption.AllDirectories))
            {
                var task =
                    factory.StartNew(_benchmark =>
                    {
                        string benchmark = (string)_benchmark;
                        string fileName = Utils.MakeRelativePath(benchmarkFolder, benchmark);
                        Trace.WriteLine("Running benchmark " + Path.GetFileName(fileName));

                        string args = experiment.Parameters;
                        if (args != null)
                        {
                            args = args.Replace("{0}", benchmark);
                        }

                        DateTime acq = DateTime.Now;

                        int maxCount = repetitions == 0 ? 25 : repetitions;
                        TimeSpan maxTime = TimeSpan.FromSeconds(30);

                        int count = 0;
                        List<ProcessRunMeasure> measures = new List<ProcessRunMeasure>();
                        TimeSpan total = TimeSpan.FromSeconds(0);

                        do
                        {
                            var m = ProcessMeasurer.Measure(executable, args, experiment.BenchmarkTimeout, experiment.MemoryLimit == 0 ? null : new Nullable<long>(experiment.MemoryLimit));
                            measures.Add(m);
                            count++;
                            total += m.WallClockTime;
                        } while ((repetitions != 0 || total < maxTime) && count < maxCount);

                        ProcessRunMeasure finalMeasure = Utils.AggregateMeasures(measures.ToArray());
                        Trace.WriteLine(String.Format("Done in {0} (aggregated by {1} runs)", finalMeasure.WallClockTime, count));

                        var performanceIndex = normal * finalMeasure.TotalProcessorTime.TotalSeconds;
                        return new BenchmarkResult(id, fileName, workerInfo, performanceIndex, acq, finalMeasure);
                    }, benchmarkFile, TaskCreationOptions.LongRunning);
                results.Add(task);
            }

            return results.ToArray();
        }

        private static string GetWorkerInfo()
        {
            return "";
        }
    }
}