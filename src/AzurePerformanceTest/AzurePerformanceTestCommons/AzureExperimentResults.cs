using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzurePerformanceTest
{
    public class AzureExperimentResults : ExperimentResults
    {
        /// <summary>Etag of the blob which contained the given result. Null means that the blob didn't exist and no results.</summary>
        private readonly string etag;

        public AzureExperimentResults(int expId, BenchmarkResult[] results, string etag) : base(expId, results)
        {
            this.etag = etag;
        }
    }
}
