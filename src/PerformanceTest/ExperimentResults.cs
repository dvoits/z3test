using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest
{
    public class ExperimentResults
    {
        private readonly int id;
        private readonly BenchmarkResult[] results;

        public ExperimentResults(int expId, BenchmarkResult[] results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            this.results = results;
            this.id = expId;
        }

        public int ExperimentId
        {
            get { return id; }
        }

        /// <summary>
        /// Gets a list of benchmarks results ordered by the file name.
        /// </summary>
        public BenchmarkResult[] Benchmarks
        {
            get { return results; }
        }

        public async Task<bool> TryDelete(IEnumerable<BenchmarkResult> toRemove)
        {
            throw new NotImplementedException();
        }
    }
}
