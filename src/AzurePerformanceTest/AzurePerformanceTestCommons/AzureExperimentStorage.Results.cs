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
        /// <param name="results">All results must have same experiment id.</param>
        public async Task PutExperimentResults(ExperimentID expId, IEnumerable<BenchmarkResult> results)
        {
            // Uploading stdout and stderr to the blob storage.
            var results2 = results.ToArray();
            var uploadOutputs = results2
                .Select(async (r, i) =>
                {
                    var _blobs = await PutStdOutput(r);
                    return Tuple.Create(i, _blobs.Item1, _blobs.Item2);
                });

            var blobs = await Task.WhenAll(uploadOutputs);
            foreach (var blob in blobs)
            {
                int i = blob.Item1;
                string stdoutBlobId = blob.Item2;
                string stderrBlobId = blob.Item3;
                if(!String.IsNullOrEmpty(stderrBlobId) || !String.IsNullOrEmpty(stdoutBlobId))
                {
                    var res = results2[i];
                    results2[i] = new BenchmarkResult(res.ExperimentID,
                        res.BenchmarkFileName,
                        res.WorkerInformation,
                        res.AcquireTime,
                        res.NormalizedRuntime,
                        res.TotalProcessorTime,
                        res.WallClockTime,
                        res.PeakMemorySizeMB,
                        res.Status,
                        res.ExitCode,
                        string.IsNullOrEmpty(stdoutBlobId) ? new MemoryStream() : Utils.StringToStream(stdoutBlobId),
                        string.IsNullOrEmpty(stderrBlobId) ? new MemoryStream() : Utils.StringToStream(stderrBlobId),
                        res.Properties);
                }
            }

            // Uploading results table.
            string fileName = GetResultsFileName(expId);
            using (MemoryStream zipStream = new MemoryStream())
            {
                using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    var entry = zip.CreateEntry(fileName);
                    BenchmarkResultsStorage.SaveBenchmarks(results2.ToArray(), entry.Open());
                }

                var blob = resultsContainer.GetBlockBlobReference(GetResultBlobName(expId));
                zipStream.Position = 0;
                await UploadBlobAsync(zipStream, blob);
            }
        }
    }
}
