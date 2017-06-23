using AzurePerformanceTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using PerformanceTest;
using PerformanceTest.Records;

namespace Nightly
{
    public class MainPageViewModel
    {
        private readonly AzureExperimentManager expManager;
        private readonly AzureSummaryManager summaryManager;
        private readonly string summaryName;
        private readonly ExperimentViewModel[] experiments;

        public static async Task<MainPageViewModel> Initialize(string connectionString, string summaryName)
        {
            var expManager = AzureExperimentManager.Open(connectionString);
            var summaryManager = new AzureSummaryManager(connectionString);

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
            return new MainPageViewModel(expManager, summaryManager, summaryName, experiments, records);
        }

        private MainPageViewModel(AzureExperimentManager expManager, AzureSummaryManager summaryManager, string summaryName, ExperimentViewModel[] experiments, RecordsTable records)
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
    }
}