using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Measurement
{
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

        public static ProcessRunAnalysis Analyze(ProcessRunMeasure measure, OutputDomain outputDomain)
        {
            ResultStatus status;
            switch (measure.Limits)
            {
                case Measure.LimitsStatus.WithinLimits:
                    status = outputDomain.GetStatus(measure);
                    break;
                case Measure.LimitsStatus.MemoryOut:
                    status = ResultStatus.OutOfMemory;
                    break;
                case Measure.LimitsStatus.TimeOut:
                    status = ResultStatus.Timeout;
                    break;
                default:
                    throw new NotSupportedException("Unexpected measure status");
            }

            var props = outputDomain.BuildProperties(measure);
            return new ProcessRunAnalysis(status, props);
        }
    }

}
