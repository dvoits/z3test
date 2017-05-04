using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Measure
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 5 || args.Length > 7)
                return PrintSyntax();

            int k = 0;
            bool init = false;
            if (args[k] == "--init")
            {
                if (args.Length != 7) return PrintSyntax();
                init = true;
                k++;
            }
            else
            {
                if (args.Length != 5) return PrintSyntax();
            }

            string executable = args[k++];
            string arguments = args[k++];
            string benchmarkContainer = args[k++];
            string category = args[k++];
            string extension = args[k++];

            int repetitions = 1;
            double referenceValue = 1.0;
            if (init) {
                repetitions = int.Parse(args[k++]);
            }

            TimeSpan timeout = TimeSpan.FromHours(1);
            ExperimentDefinition definition = ExperimentDefinition.Create(executable, benchmarkContainer, extension, arguments, timeout, category: category);
            string version = GetVersion(executable);

            if(init)
                Print(String.Format("Initializing environment..."));
            else
                Print(String.Format("Measuring performance of {0} {1}...\n", executable, version));

            if (init)
            {
                var reference = new ReferenceExperiment(definition, repetitions, referenceValue);
                ExperimentManager manager = LocalExperimentManager.NewExperiments("measure", reference);
            }
            else
            {
                ExperimentManager manager = LocalExperimentManager.OpenExperiments("measure");
                Run(manager, definition).Wait();
            }

            return 0;
        }

        private static int PrintSyntax()
        {
            Console.WriteLine("Setup tests:\n\tMeasure.exe --init <executable> <arguments> <benchmarkContainer> <category> <extension> <repetitions>");
            Console.WriteLine("Run tests:\n\tMeasure.exe <executable> <arguments> <benchmarkContainer> <category> <extension>");
            return 2;
        }

        static async Task Run(ExperimentManager manager, ExperimentDefinition definition)
        {
            int id = await manager.StartExperiment(definition);
            var results = manager.GetResults(id);


            var filter = new ExperimentManager.ExperimentFilter
            {
                BenchmarkContainerEquals = definition.BenchmarkContainer,
                CategoryEquals = definition.Category,
                ExecutableEquals = definition.Executable,
                ParametersEquals = definition.Parameters
            };
            var history = (await manager.FindExperiments(filter)).Where(q => q != id).ToArray();

            Dictionary<string, BenchmarkResult> lastBenchmarks = new Dictionary<string, BenchmarkResult>();
            if (history.Length != 0)
            {
                var lastResults = await Task.WhenAll(manager.GetResults(history.Max()));
                foreach (var b in lastResults)
                {
                    lastBenchmarks[b.BenchmarkFileName] = b;
                }
            }

            var print = results.Select(task => task.ContinueWith(benchmark =>
                {
                    if (benchmark.IsFaulted)
                    {
                        PrintError(String.Format("Failed to complete the benchmark {0}", benchmark.Exception.Message));
                        return;
                    }
                    BenchmarkResult lastBenchmark = null;
                    lastBenchmarks.TryGetValue(benchmark.Result.BenchmarkFileName, out lastBenchmark);
                    if (lastBenchmark != null && lastBenchmark.Measurements.Status != Measurement.Measure.CompletionStatus.Success)
                        lastBenchmark = null;
                    PrintBenchmark(benchmark.Result, lastBenchmark);
                })).ToArray();
            await Task.WhenAll(print);
        }


        static void PrintBenchmark(BenchmarkResult result, BenchmarkResult lastResult = null)
        {
            string info;
            double speedup = 1.0;
            double extraMem = 1.0;
            double threshold = 0.15;

            if (lastResult == null)
                info = String.Format("{1:0.0000}\t{2:0.00} MB\t{0}",
                    result.BenchmarkFileName, result.NormalizedRuntime, result.Measurements.PeakMemorySize >> 20);
            else
            {
                speedup = (lastResult.NormalizedRuntime / result.NormalizedRuntime);
                extraMem = (result.Measurements.PeakMemorySize - lastResult.Measurements.PeakMemorySize) >> 20;
                info = String.Format("{0:0.0000} ({1:0.00}{2})\t{3:0.00} MB ({4}{5:0.00})\t{6}",
                        result.NormalizedRuntime,
                        speedup,
                        speedup == 1 ? " same" : speedup > 1 ? " faster" : " slower",
                        result.Measurements.PeakMemorySize >> 20,
                        extraMem >= 0 ? "+" : "",
                        extraMem,
                        result.BenchmarkFileName);
                extraMem = ((double)result.Measurements.PeakMemorySize) / lastResult.Measurements.PeakMemorySize;
            }


            if (result.Measurements.Status == Measurement.Measure.CompletionStatus.Success)
            {
                if (speedup < 1 - threshold)
                    PrintWarning("Slower   " + info);
                else if (extraMem > 1 + threshold)
                    PrintWarning("More memory   " + info);
                else if (speedup > 1 + threshold)
                    PrintNotice("Faster   " + info);
                else if (extraMem < 1 - threshold)
                    PrintNotice("Less memory   " + info);
                else
                    Print("Passed   " + info);
            }
            else if (result.Measurements.Status == Measurement.Measure.CompletionStatus.OutOfMemory)
            {
                PrintError("Out of memory    " + info);
            }
            else if (result.Measurements.Status == Measurement.Measure.CompletionStatus.Error)
            {
                PrintError("Error    " + info);
            }
            else if (result.Measurements.Status == Measurement.Measure.CompletionStatus.Timeout)
            {
                PrintError("Timeout    " + info);
            }
        }


        static void Print(string s)
        {
            Console.WriteLine(s);
        }

        static void PrintError(string s)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(s);
            Console.ForegroundColor = color;
        }


        static void PrintWarning(string s)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(s);
            Console.ForegroundColor = color;
        }

        static void PrintNotice(string s)
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(s);
            Console.ForegroundColor = color;
        }

        private static string GetVersion(string pathToExe)
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(pathToExe);
            return versionInfo.ProductVersion;
        }
    }
}
