﻿using AzurePerformanceTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using PerformanceTest;
using PerformanceTest.Records;

namespace Nightly
{
    public class ExperimentsViewModel
    {
        private readonly AzureExperimentManager expManager;
        private readonly AzureSummaryManager summaryManager;
        private readonly string summaryName;
        /// <summary>Ordered by submission time, most recent is last.</summary>
        private readonly ExperimentViewModel[] experiments;

        public static async Task<ExperimentsViewModel> Initialize(string connectionString, string summaryName, IDomainResolver domainResolver)
        {
            var expManager = AzureExperimentManager.Open(connectionString);
            var summaryManager = new AzureSummaryManager(connectionString, domainResolver);

            var summRec = await summaryManager.GetSummariesAndRecords(summaryName);
            var summ = summRec.Item1;
            var records = summRec.Item2;
            var now = DateTime.Now;

            var expTasks =
                summ
                .Select(async expSum =>
                {
                    var exp = await expManager.TryFindExperiment(expSum.Id);
                    if (exp == null) return null;

                    bool isFinished;
                    if (exp.Status.SubmissionTime.Subtract(now).TotalDays >= 3)
                    {
                        isFinished = true;
                    }
                    else
                    {
                        try
                        {
                            var jobState = await expManager.GetExperimentJobState(new[] { exp.ID });
                            isFinished = jobState[0] != ExperimentExecutionState.Active;
                        }
                        catch
                        {
                            isFinished = true;
                        }
                    }

                    return new ExperimentViewModel(expSum, isFinished, exp.Status.SubmissionTime, exp.Definition.BenchmarkTimeout);
                });

            var experiments = await Task.WhenAll(expTasks);
            return new ExperimentsViewModel(expManager, summaryManager, summaryName, experiments, records);
        }

        private ExperimentsViewModel(AzureExperimentManager expManager, AzureSummaryManager summaryManager, string summaryName, ExperimentViewModel[] experiments, RecordsTable records)
        {
            this.expManager = expManager;
            this.summaryManager = summaryManager;
            this.summaryName = summaryName;
            this.experiments = experiments.OrderBy(exp => exp.SubmissionTime).ToArray();

            Records = records;
        }

        public string SummaryName { get { return summaryName; } }

        public string[] Categories
        {
            get
            {
                return experiments.SelectMany(exp => exp.Summary.CategorySummary.Keys).Distinct().ToArray();
            }
        }

        public RecordsTable Records { get; internal set; }

        public ExperimentViewModel[] Experiments { get { return experiments; } }

        public ExperimentViewModel GetExperiment(int id)
        {
            return experiments.First(exp => exp.Id == id);
        }

        public ExperimentViewModel GetLastExperiment()
        {
            return experiments.Last();
        }

        public async Task<ExperimentStatusSummary> GetStatusSummary(int id)
        {
            for (int i = experiments.Length; --i >= 0;)
            {
                if(experiments[i].Id == id)
                {
                    int? refId = null;
                    if (i > 0) refId = experiments[i - 1].Id;
                    var summary = await summaryManager.GetStatusSummary(id, refId);
                    return summary;
                }
            }
            throw new KeyNotFoundException("Experiment " + id + " not found");
        }
    }
}