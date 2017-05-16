using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PerformanceTest
{
    class AzureDataAccess
    {
        public static async Task<IEnumerable<TEntity>> GetEntitiesAsync<TEntity>(TableQuery<TEntity> query, CancellationToken cancellationToken)
        {
            List<TEntity> results = new List<TEntity>();
            TableContinuationToken currentToken = null;
            TableQuery<TEntity> q = query;
            do
            {
                var segment = await q.ExecuteSegmentedAsync(currentToken, cancellationToken);
                results.AddRange(segment.Results);

                currentToken = segment.ContinuationToken;
                if (currentToken != null && query.TakeCount.HasValue)
                {
                    int moreItems = query.TakeCount.Value - results.Count;
                    if (moreItems <= 0) break;
                    q = query.Take(moreItems);
                }
            } while (currentToken != null && !cancellationToken.IsCancellationRequested);
            return results;
        }
    }
}
