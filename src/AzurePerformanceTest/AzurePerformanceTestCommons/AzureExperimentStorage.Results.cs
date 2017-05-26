using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ExperimentID = System.Int32;


namespace AzurePerformanceTest
{
    public partial class AzureExperimentStorage
    {
        private FileStorage cache;

        private void InitializeCache()
        {
            string appFolder = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                cache = FileStorage.Open(Path.Combine(appFolder, "cache"));
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to open local cache: " + ex.Message);

                int i = 0;
                while (true)
                {
                    try
                    {
                        FileStorage.Clear(appFolder);
                        cache = FileStorage.Open(appFolder);
                        return;
                    }
                    catch (Exception ex2)
                    {
                        Trace.WriteLine(String.Format("Failed to create new cache: {0}, attempt {1}", ex2.Message, ++i));
                        if (i == 10) throw;
                        Thread.Sleep(100);
                    }
                }
            }
        }

        public void ClearCache()
        {
            cache.Clear();
        }

        public async Task<BenchmarkResult[]> GetResults(ExperimentID experimentID)
        {
            if (!cache.HasResults(experimentID))
            {
                Trace.WriteLine(string.Format("Results for experiment {0} are missing in local cache, downloading from cloud...", experimentID));
                string blobName = GetResultBlobName(experimentID);
                var blob = resultsContainer.GetBlobReference(blobName);

                using (MemoryStream zipStream = new MemoryStream(4 << 20))
                {
                    await blob.DownloadToStreamAsync(zipStream);

                    zipStream.Position = 0;
                    using (ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
                    {
                        var entry = zip.GetEntry(GetResultsFileName(experimentID));
                        using (var tableStream = entry.Open())
                        using (var fileStream = File.Create(cache.IdToPath(experimentID)))
                        {
                            await tableStream.CopyToAsync(fileStream);
                        }
                    }
                }
            }
            return cache.GetResults(experimentID);
        }


        /// <summary>
        /// Puts the benchmark results of the given experiment to the storage.
        /// </summary>
        /// <param name="results">All results must have same experiment id. Streams should contain contents of respective stdouts/errs</param>
        public async Task PutExperimentResults(ExperimentID expId, IEnumerable<BenchmarkResult> results)
        {
            // Uploading stdout and stderr to the blob storage.
            BenchmarkResult[] results2 = await UploadStreams(results);

            // Uploading results table.
            await PutExperimentResultsWithBlobnames(expId, results2);
        }

        /// <summary>
        /// Puts the benchmark results of the given experiment to the storage.
        /// </summary>
        /// <param name="results">All results must have same experiment id. Streams should contain names of blobs containing respective stdouts/errs</param>
        public async Task PutExperimentResultsWithBlobnames(int expId, BenchmarkResult[] results)
        {
            string fileName = GetResultsFileName(expId);
            using (MemoryStream zipStream = new MemoryStream())
            {
                using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    var entry = zip.CreateEntry(fileName);
                    BenchmarkResultsStorage.SaveBenchmarks(results, entry.Open());
                }

                var blob = resultsContainer.GetBlockBlobReference(GetResultBlobName(expId));
                zipStream.Position = 0;
                await UploadBlobAsync(zipStream, blob);
            }
        }

        /// <summary>
        /// Uploads contents of stdouts/errs in results into blobs.
        /// </summary>
        /// <param name="results">Streams should contain contents of respective stdouts/errs</param>
        /// <returns>Results with blob names in streams</returns>
        public async Task<BenchmarkResult[]> UploadStreams(IEnumerable<BenchmarkResult> results)
        {
            var uploadOutputs = results
                            .Select(async (res, i) =>
                            {
                                var _blobs = await PutStdOutput(res);
                                string stdoutBlobId = _blobs.Item1;
                                string stderrBlobId = _blobs.Item2;
                                if (!String.IsNullOrEmpty(stderrBlobId) || !String.IsNullOrEmpty(stdoutBlobId))
                                    return new BenchmarkResult(res.ExperimentID,
                                        res.BenchmarkFileName,
                                        res.WorkerInformation,
                                        res.AcquireTime,
                                        res.NormalizedRuntime,
                                        res.TotalProcessorTime,
                                        res.WallClockTime,
                                        res.PeakMemorySizeMB,
                                        res.Status,
                                        res.ExitCode,
                                        string.IsNullOrEmpty(_blobs.Item1) ? new MemoryStream() : Utils.StringToStream(_blobs.Item1),
                                        string.IsNullOrEmpty(_blobs.Item2) ? new MemoryStream() : Utils.StringToStream(_blobs.Item2),
                                        res.Properties);
                                else
                                    return res;
                            });

            var results2 = await Task.WhenAll(uploadOutputs);
            return results2;
        }
    }
}
