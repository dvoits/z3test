using AzurePerformanceTest;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzurePerformanceTest
{
    public partial class AzureExperimentStorage
    {
        private const int azureStorageBatchSize = 100;

        /// <summary>
        /// Expects that the experiments table is empty or doesn't exist and initializes it from scratch with the given experiments.
        /// </summary>
        public async Task ImportExperiments(IEnumerable<ExperimentEntity> experiments)
        {
            var nextIdQuery = QueryForNextId();
            var list = (await experimentsTable.ExecuteQuerySegmentedAsync(nextIdQuery, null)).ToList();
            if (list.Count != 0) throw new InvalidOperationException("The experiments table mustn't be initialized yet");

            var upload =
                GroupExperiments(experiments, azureStorageBatchSize)
                .Select(batch =>
                {
                    TableBatchOperation opsBatch = new TableBatchOperation();
                    int maxID = 0;
                    foreach (var item in batch)
                    {
                        opsBatch.Insert(item);
                        int id = int.Parse(item.RowKey);
                        if (id > maxID) maxID = id;
                    }
                    return Tuple.Create(experimentsTable.ExecuteBatchAsync(opsBatch), maxID);
                })
                .ToArray();

            var nextId = upload.Length > 0 ? upload.Max(t => t.Item2) + 1 : 1;
            var inserts = upload.Select(t => t.Item1);
            await Task.WhenAll(inserts);

            var nextIdEnt = new NextExperimentIDEntity();
            nextIdEnt.Id = nextId;
            await experimentsTable.ExecuteAsync(TableOperation.Insert(nextIdEnt));
        }

        private static IEnumerable<IEnumerable<ExperimentEntity>> GroupExperiments(IEnumerable<ExperimentEntity> seq, int n)
        {
            var groupsByCat = new Dictionary<string, List<ExperimentEntity>>();
            string lastCat = null;
            List<ExperimentEntity> lastGroup = null;
            foreach (ExperimentEntity item in seq)
            {
                List<ExperimentEntity> group;
                if (lastCat == item.Category)
                {
                    group = lastGroup;
                }
                else if (!groupsByCat.TryGetValue(item.Category, out group))
                {
                    group = new List<ExperimentEntity>(n);
                    groupsByCat.Add(item.Category, group);
                }

                group.Add(item);

                if (group.Count == n)
                {
                    yield return group;
                    group = groupsByCat[item.Category] = new List<ExperimentEntity>(n);
                }
                lastCat = item.Category;
                lastGroup = group;
            }
            foreach (var group in groupsByCat)
            {
                var items = group.Value;
                if (items.Count > 0)
                    yield return items;
            }
        }

        private static IEnumerable<IEnumerable<T>> Group<T>(IEnumerable<T> seq, int n)
        {
            List<T> group = new List<T>(n);
            foreach (T item in seq)
            {
                group.Add(item);
                if (group.Count == n)
                {
                    yield return group;
                    group = new List<T>(n);
                }
            }
            if (group.Count > 0)
                yield return group;
        }
    }
}
