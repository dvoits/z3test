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
            BenchmarkResult[] results =
                cache.GetResults(experimentID)
                .Select(r =>
                {
                    Stream stdout = null;
                    if(r.StdOut != null && r.StdOut.Length > 0)
                    {
                        string blobName = Utils.StreamToString(r.StdOut, false);
                        stdout = new LazyBlobStream(outputContainer.GetBlobReference(blobName));
                    }

                    Stream stderr = null;
                    if (r.StdErr != null && r.StdErr.Length > 0)
                    {
                        string blobName = Utils.StreamToString(r.StdErr, false);
                        stderr = new LazyBlobStream(outputContainer.GetBlobReference(blobName));
                    }

                    if (stdout != null || stderr != null)
                    {
                        return new BenchmarkResult(r.ExitCode, r.BenchmarkFileName, r.WorkerInformation, r.AcquireTime, r.NormalizedRuntime, r.TotalProcessorTime, r.WallClockTime,
                            r.PeakMemorySizeMB, r.Status, r.ExitCode,
                            stdout == null ? r.StdOut : stdout,
                            stderr == null ? r.StdErr : stderr,
                            r.Properties);
                    }
                    else return r;
                })
                .ToArray();
            return results;
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
                                return ReplaceStreamsWithBlobNames(res, stdoutBlobId, stderrBlobId);
                            });

            var results2 = await Task.WhenAll(uploadOutputs);
            return results2;
        }

        private static BenchmarkResult ReplaceStreamsWithBlobNames(BenchmarkResult res, string stdoutBlobId, string stderrBlobId)
        {
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
                    string.IsNullOrEmpty(stdoutBlobId) ? new MemoryStream() : Utils.StringToStream(stdoutBlobId),
                    string.IsNullOrEmpty(stderrBlobId) ? new MemoryStream() : Utils.StringToStream(stderrBlobId),
                    res.Properties);
            else
                return res;
        }
    }
}
