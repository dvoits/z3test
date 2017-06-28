using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AzurePerformanceTest.AzureExperimentStorage;

namespace AzurePerformanceTest
{
    public class AzureExperimentResults : ExperimentResults
    {
        /// <summary>Etag of the blob which contained the given result. Null means that the blob didn't exist and no results.</summary>
        private readonly string etag;
        private readonly int expId;
        private readonly Dictionary<BenchmarkResult, AzureBenchmarkResult> externalOutputs;
        private readonly AzureExperimentStorage storage;

        public AzureExperimentResults(AzureExperimentStorage storage, int expId, AzureBenchmarkResult[] results, string etag) : base(expId, Parse(results, storage))
        {
            this.expId = expId;
            this.storage = storage;
            this.etag = etag;

            var benchmarks = Benchmarks;
            externalOutputs = new Dictionary<BenchmarkResult, AzureBenchmarkResult>();
            for (int i = 0; i < results.Length; i++)
            {
                var r = results[i];
                if (!string.IsNullOrEmpty(r.StdOutExtStorageIdx) || !string.IsNullOrEmpty(r.StdErrExtStorageIdx))
                    externalOutputs.Add(benchmarks[i], r);
            }
        }

        public override async Task<bool> TryDelete(IEnumerable<BenchmarkResult> toRemove)
        {
            var benchmarks = Benchmarks;
            var removeSet = new HashSet<BenchmarkResult>(toRemove);
            if (removeSet.Count == 0) return true;

            int n = benchmarks.Length;
            List<AzureBenchmarkResult> newAzureResults = new List<AzureBenchmarkResult>(n);
            List<BenchmarkResult> newResults = new List<BenchmarkResult>(n);
            List<AzureBenchmarkResult> deleteOuts = new List<AzureBenchmarkResult>();
            for (int i = 0, j = 0; i < n; i++)
            {
                var b = benchmarks[i];
                if (!removeSet.Contains(b)) // remains
                {
                    var azureResult = AzureExperimentStorage.ToAzureBenchmarkResult(b);
                    newAzureResults.Add(azureResult);
                    newResults.Add(b);
                }
                else // to be removed
                {
                    removeSet.Remove(b);

                    AzureBenchmarkResult ar;
                    if (externalOutputs.TryGetValue(b, out ar))
                    {
                        deleteOuts.Add(ar);
                    }
                }
            }
            if (removeSet.Count != 0) throw new ArgumentException("Some of the given results to remove do not belong to the experiment results");

            // Updating blob with results table
            bool success;
            if (etag != null) // blob already exists
                success = await storage.PutAzureExperimentResults(expId, newAzureResults.ToArray(), UploadBlobMode.ReplaceExact, etag);
            else // blob didn't exist
                success = await storage.PutAzureExperimentResults(expId, newAzureResults.ToArray(), UploadBlobMode.CreateNew);

            if (!success) return false;

            // Update benchmarks array
            Replace(newResults.ToArray());

            // Deleting blobs with output
            foreach (var ar in deleteOuts)
            {
                try
                {
                    var _ = storage.DeleteOutputs(ar);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(string.Format("Exception when deleting output: {0}", ex));
                }
            }

            return true;
        }

        private static BenchmarkResult[] Parse(AzureBenchmarkResult[] results, AzureExperimentStorage storage)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (results == null) throw new ArgumentNullException(nameof(results));

            int n = results.Length;
            BenchmarkResult[] benchmarks = new BenchmarkResult[n];
            for (int i = 0; i < n; i++)
            {
                benchmarks[i] = storage.ParseAzureBenchmarkResult(results[i]);
            }
            return benchmarks;
        }
    }
}
