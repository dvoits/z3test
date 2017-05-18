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
            IEnumerable<KeyValuePair<int, ExperimentEntity>> experiments = await storage.GetExperiments(filter);

            return experiments.OrderByDescending(q => q.Value.Submitted).Select(e => e.Key);
        }

        public override async Task<ExperimentDefinition> GetDefinition(ExperimentID id)
        {
            var experimentEntity = await storage.GetExperiment(id);
            return ExperimentDefinition.Create(
                experimentEntity.Executable,
                experimentEntity.BenchmarkContainer,
                experimentEntity.BenchmarkFileExtension,
                experimentEntity.Parameters,
                TimeSpan.FromSeconds(experimentEntity.BenchmarkTimeout),
                experimentEntity.Category,
                experimentEntity.MemoryLimit << 20
                );
        }

        public override async Task<BenchmarkResult[]> GetResults(ExperimentID id)
        {
            return await storage.GetResults(id);
        }

        public override Task<IEnumerable<ExperimentStatus>> GetStatus(IEnumerable<ExperimentID> ids)
        {
            throw new NotImplementedException();
        }

        public override async Task<ExperimentID> StartExperiment(ExperimentDefinition definition, string creator = null, string note = null)
        {
            var id = await storage.AddExperiment(definition, DateTime.Now, creator, note);
            //TODO: schedule execution
            return id;
        }

        public override async Task UpdateNote(int id, string note)
        {
            await storage.UpdateNote(id, note);
        }

        public override Task UpdatePriority(int id, string priority)
        {
            throw new NotImplementedException();
        }

        public override async Task UpdateStatusFlag(ExperimentID id, bool flag)
        {
            await storage.UpdateStatusFlag(id, flag);
        }
    }
}
