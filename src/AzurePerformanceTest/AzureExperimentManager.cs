using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Queryable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PerformanceTest
{
    public sealed class AzureExperimentManager : ExperimentManager
    {
        private readonly CloudTable tableExperiments;

        public static AzureExperimentManager Connect(string tablesConnectionString)
        {
            CloudStorageAccount tablesStorageAccount = CloudStorageAccount.Parse(tablesConnectionString);
            CloudTableClient tableClient = tablesStorageAccount.CreateCloudTableClient();
            return new AzureExperimentManager(tableClient);
        }

        private AzureExperimentManager(CloudTableClient tableClient) 
        {
            tableExperiments = tableClient.GetTableReference("Experiments");
        }

        public override Task DeleteExperiment(int id)
        {
            throw new NotImplementedException();
        }

        public override async Task<IEnumerable<int>> FindExperiments(ExperimentFilter? filter = default(ExperimentFilter?))
        {
            //TableQuery<ExperimentEntity> query = tableExperiments.CreateQuery<ExperimentEntity>().Select(new[] { "ID", "Submitted" }).AsTableQuery();
            TableQuery<ExperimentEntity> query = tableExperiments.CreateQuery<ExperimentEntity>().AsTableQuery();
            var entities = await AzureDataAccess.GetEntitiesAsync(query, CancellationToken.None);
            return entities.OrderByDescending(e => e.Submitted).Select(e => e.ID);
        }

        public override Task<ExperimentDefinition> GetDefinition(int id)
        {
            throw new NotImplementedException();
        }

        public override Task<BenchmarkResult>[] GetResults(int id)
        {
            throw new NotImplementedException();
        }

        public override Task<IEnumerable<ExperimentStatus>> GetStatus(IEnumerable<int> ids)
        {
            throw new NotImplementedException();
        }

        public override Task<int> StartExperiment(ExperimentDefinition definition, string creator = null, string note = null)
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

        public override Task UpdateStatusFlag(int id, bool flag)
        {
            throw new NotImplementedException();
        }
    }
}
