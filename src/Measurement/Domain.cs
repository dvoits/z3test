using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Measurement
{
    public abstract class Domain
    {
        private static Domain defaultDomain = new DefaultDomain();
        public static Domain Default { get { return defaultDomain; } }

        private readonly string name;

        protected Domain(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentException("name is null or empty", "name");
            this.name = name;
        }

        public string Name { get { return name; } }

        public virtual string[] BenchmarkExtensions { get { return new string[0]; } }


        public abstract ProcessRunAnalysis Analyze(string inputFile, ProcessRunMeasure measure);

        public AggregatedAnalysis Aggregate(IEnumerable<ProcessRunAnalysis> benchmarkResults)
        {
            var results = benchmarkResults.ToArray();

            int bugs = 0, errors = 0, timeouts = 0, memouts = 0;
            for (int i = 0; i < results.Length; i++)
            {
                var result = results[i];
                switch (result.Status)
                {
                    case ResultStatus.OutOfMemory:
                        memouts++;
                        break;
                    case ResultStatus.Timeout:
                        timeouts++;
                        break;
                    case ResultStatus.Error:
                        errors++;
                        break;
                    case ResultStatus.Bug:
                        bugs++;
                        break;
                    default:
                        break;
                }
            }

            var props = AggregateProperties(results);
            return new AggregatedAnalysis(bugs, errors, timeouts, memouts, props);
        }

        protected abstract IReadOnlyDictionary<string, string> AggregateProperties(IEnumerable<ProcessRunAnalysis> benchmarkResults);
    }

    public sealed class DefaultDomain : Domain
    {
        public DefaultDomain() : base("default")
        {

        }

        public override ProcessRunAnalysis Analyze(string inputFile, ProcessRunMeasure measure)
        {
            ResultStatus status;
            switch (measure.Limits)
            {
                case Measure.LimitsStatus.WithinLimits:
                    status = measure.ExitCode == 0 ? ResultStatus.Success : ResultStatus.Error;
                    break;
                case Measure.LimitsStatus.MemoryOut:
                    status = ResultStatus.OutOfMemory;
                    break;
                case Measure.LimitsStatus.TimeOut:
                    status = ResultStatus.Timeout;
                    break;
                default:
                    throw new NotSupportedException("Unknown status");
            }

            return new ProcessRunAnalysis(status, new Dictionary<string, string>());
        }

        protected override IReadOnlyDictionary<string, string> AggregateProperties(IEnumerable<ProcessRunAnalysis> benchmarkResults)
        {
            return new Dictionary<string, string>();
        }
    }

    public class ProcessRunAnalysis
    {
        private readonly ResultStatus status;
        private readonly IReadOnlyDictionary<string, string> outputProperties;

        public ProcessRunAnalysis(ResultStatus status, IReadOnlyDictionary<string, string> outputProperties)
        {
            this.status = status;
            this.outputProperties = outputProperties;
        }


        public ResultStatus Status { get { return status; } }
        public IReadOnlyDictionary<string, string> OutputProperties { get { return outputProperties; } }
    }

    public class AggregatedAnalysis
    {
        public AggregatedAnalysis(int bugs, int errors, int timeouts, int memouts, IReadOnlyDictionary<string, string> props)
        {
            Bugs = bugs;
            Errors = errors;
            Timeouts = timeouts;
            MemoryOuts = memouts;
            Properties = props;
        }

        public int Bugs { get; private set; }

        public int Errors { get; private set; }

        public int Timeouts { get; private set; }

        public int MemoryOuts { get; private set; }

        public IReadOnlyDictionary<string, string> Properties { get; private set; }
    }


    public enum ResultStatus
    {
        Success,
        OutOfMemory,
        Timeout,
        Error,
        Bug
    }
}
