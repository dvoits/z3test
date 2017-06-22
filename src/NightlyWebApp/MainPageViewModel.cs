using AzurePerformanceTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using PerformanceTest;

namespace Nightly
{
    public class MainPageViewModel
    {
        private readonly AzureExperimentManager manager;
        private readonly string summaryName;
        private readonly ExperimentSummary[] summaries;

        public MainPageViewModel(AzureExperimentManager manager, string summaryName, ExperimentSummary[] summaries)
        {
            this.manager = manager;
            this.summaryName = summaryName;
            this.summaries = summaries;
        }

        public static async Task<MainPageViewModel> Initialize(AzureExperimentManager manager, string summaryName)
        {
            var summaries = await manager.Storage.GetSummary(summaryName);
            return new MainPageViewModel(manager, summaryName, summaries);
        }

        public string[] Categories
        {
            get
            {
                return summaries.SelectMany(s => s.CategorySummary.Keys).Distinct().ToArray();
            }
        }

       
        public async Task<ExperimentViewModel> GetExperiment(int id)
        {
            var exp = await manager.TryFindExperiment(id);
            if (exp == null) throw new Exception("Experiment " + id + " not found");

            bool isFinished;
            try
            {
                var jobState = await manager.GetExperimentJobState(new[] { id });
                isFinished = jobState[0] != ExperimentExecutionState.Active;
            }
            catch
            {
                isFinished = true;
            }

            var summary = summaries.First(s => s.Id == id);
            return new ExperimentViewModel(summary, isFinished, exp.Status.SubmissionTime);
        }

        public Task<ExperimentViewModel> GetLastExperiment()
        {
            return GetExperiment(summaries.OrderByDescending(s => s.Date).First().Id);
        }
    }
}