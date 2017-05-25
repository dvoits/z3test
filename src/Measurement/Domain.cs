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



        public abstract ProcessRunAnalysis Analyze(string inputFile, ProcessRunMeasure measure);
    }

    public sealed class DefaultDomain : Domain
    {
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


    public enum ResultStatus
    {
        Success,
        OutOfMemory,
        Timeout,
        Error,
        Bug
    }
}
