using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PerformanceTest;

using ExperimentID = System.Int32;

namespace AzurePerformanceTest
{
    public class AzureExperimentManager : ExperimentManager
    {
        AzureExperimentStorage storage;

        protected AzureExperimentManager(AzureExperimentStorage storage)
        {
            this.storage = storage;
        }

        public static async Task<AzureExperimentManager> New(AzureExperimentStorage storage, ReferenceExperiment reference)
        {
            await storage.SaveReferenceExperiment(reference);
            return new AzureExperimentManager(storage);
        }

        public static AzureExperimentManager Open(AzureExperimentStorage storage)
        {
            return new AzureExperimentManager(storage);
        }

        public override async Task DeleteExperiment(ExperimentID id)
        {
            throw new NotImplementedException();
        }

        public override async Task<IEnumerable<ExperimentID>> FindExperiments(ExperimentFilter? filter = default(ExperimentFilter?))
        {
            IEnumerable<KeyValuePair<int, ExperimentEntity>> experiments = await storage.GetExperiments();

            if (filter.HasValue)
            {
                experiments =
                    experiments
                    .Where(q =>
                    {
                        var id = q.Key;
                        var e = q.Value;
                        return (filter.Value.BenchmarkContainerEquals == null || e.BenchmarkContainer == filter.Value.BenchmarkContainerEquals) &&
                                    (filter.Value.CategoryEquals == null || e.Category == null || e.Category.Contains(filter.Value.CategoryEquals)) &&
                                    (filter.Value.ExecutableEquals == null || e.Executable == null || e.Executable == filter.Value.ExecutableEquals) &&
                                    (filter.Value.ParametersEquals == null || e.Parameters == null || e.Parameters == filter.Value.ParametersEquals) &&
                                    (filter.Value.NotesEquals == null || e.Note == null || e.Note.Contains(filter.Value.NotesEquals)) &&
                                    (filter.Value.CreatorEquals == null || e.Creator == null || e.Creator.Contains(filter.Value.CreatorEquals));
                    })
                    .OrderByDescending(q => q.Value.Submitted);
            }

            return experiments.OrderByDescending(q => q.Value.Submitted).Select(e => e.Key);
        }

        public override Task<ExperimentDefinition> GetDefinition(ExperimentID id)
        {
            throw new NotImplementedException();
        }

        public override Task<BenchmarkResult>[] GetResults(ExperimentID id)
        {
            throw new NotImplementedException();
        }

        public override Task<IEnumerable<ExperimentStatus>> GetStatus(IEnumerable<ExperimentID> ids)
        {
            throw new NotImplementedException();
        }

        public override Task<ExperimentID> StartExperiment(ExperimentDefinition definition, string creator = null, string note = null)
        {
            throw new NotImplementedException();
        }

        public override Task UpdateNote(int id, string note)
        {
            throw new NotImplementedException();
        }

        public override Task UpdatePriority(int id, string priority)
        {
            throw new NotImplementedException();
        }

        public override Task UpdateStatusFlag(ExperimentID id, bool flag)
        {
            throw new NotImplementedException();
        }
    }
}
