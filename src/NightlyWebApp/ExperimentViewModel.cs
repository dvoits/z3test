using Measurement;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Nightly
{
    public class ExperimentViewModel
    {
        private ExperimentSummary summary;

        public ExperimentViewModel(ExperimentSummary summary, bool isFinished, DateTime submitted, TimeSpan timeout)
        {
            this.summary = summary;
            this.IsFinished = isFinished;
            this.SubmissionTime = submitted;
            this.Timeout = timeout;
        }

        public AggregatedAnalysis this[string category]
        {
            get
            {
                if (string.IsNullOrEmpty(category))
                {
                    return summary.Overall;
                }
                else
                {
                    return summary.CategorySummary[category];
                }
            }
        }

        public int Id { get { return summary.Id; } }

        public bool IsFinished { get; internal set; }

        public DateTime SubmissionTime { get; internal set; }

        public TimeSpan Timeout { get; internal set; }

        public ExperimentSummary Summary { get { return summary; } }
    }

    public class Z3SummaryProperties
    {
        private readonly IReadOnlyDictionary<string, string> props;

        public Z3SummaryProperties(AggregatedAnalysis summary)
        {
            props = summary.Properties;
        }

        public int Sat { get { return int.Parse(props[Z3Domain.KeySat]); } }
        public int Unsat { get { return int.Parse(props[Z3Domain.KeyUnsat]); } }
        public double TimeUnsat { get { return double.Parse(props[Z3Domain.KeyTimeUnsat]); } }
        public double TimeSat { get { return double.Parse(props[Z3Domain.KeyTimeSat]); } }
    }


}