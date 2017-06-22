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

        public ExperimentViewModel(ExperimentSummary summary, bool isFinished, DateTime submitted)
        {
            this.summary = summary;
            this.IsFinished = isFinished;
            this.SubmissionTime = submitted;
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

        public ExperimentSummary Summary { get { return summary; } }
    }
}