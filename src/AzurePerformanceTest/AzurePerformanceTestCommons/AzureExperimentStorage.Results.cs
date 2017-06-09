using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ExperimentID = System.Int32;


namespace AzurePerformanceTest
{
    public partial class AzureExperimentStorage
    {
        const int MaxStdOutLength = 4096;
        const int MaxStdErrLength = 4096;


        public async Task<BenchmarkResult[]> GetResults(ExperimentID experimentId)
        {
            AzureBenchmarkResult[] azureResults = await GetAzureExperimentResults(experimentId);

            BenchmarkResult[] results = azureResults.Select(ParseAzureBenchmarkResult).ToArray();

            return results;
        }


        public async Task PutResult(ExperimentID expId, BenchmarkResult result)
        {
            var queue = GetResultsQueueReference(expId);
            var result2 = await PrepareBenchmarkResult(result);
            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(1);//signalling that this message contains a result
                (new BinaryFormatter()).Serialize(ms, result2);
                await queue.AddMessageAsync(new CloudQueueMessage(ms.ToArray()));
            }
        }

        private async Task<AzureBenchmarkResult> PrepareBenchmarkResult(BenchmarkResult result)
        {
            AzureBenchmarkResult azureResult = new AzureBenchmarkResult();
            azureResult.AcquireTime = result.AcquireTime;
            azureResult.BenchmarkFileName = result.BenchmarkFileName;
            azureResult.ExitCode = result.ExitCode;
            azureResult.ExperimentID = result.ExperimentID;
            azureResult.NormalizedRuntime = result.NormalizedRuntime;
            azureResult.PeakMemorySizeMB = result.PeakMemorySizeMB;
            azureResult.Properties = new Dictionary<string, string>();
            foreach (var prop in result.Properties)
                azureResult.Properties.Add(prop.Key, prop.Value);
            azureResult.Status = result.Status;
            if (result.StdOut.Length > MaxStdOutLength)
            {
                string stdoutBlobId = BlobNameForStdOut(result.ExperimentID, result.BenchmarkFileName);
                await UploadOutput(stdoutBlobId, result.StdOut, true);
                Trace.WriteLine(string.Format("Uploaded stdout for experiment {0}", result.ExperimentID));
                azureResult.StdOut = null;
                azureResult.StdOutStoredExternally = true;
            }
            else
            {
                if (result.StdOut.Length > 0)
                {
                    long pos = 0;
                    if (result.StdOut.CanSeek)
                    {
                        pos = result.StdOut.Position;
                        result.StdOut.Seek(0, SeekOrigin.Begin);
                    }
                    using (StreamReader sr = new StreamReader(result.StdOut))
                    {
                        azureResult.StdOut = await sr.ReadToEndAsync();
                    }
                    if (result.StdOut.CanSeek)
                    {
                        result.StdOut.Seek(pos, SeekOrigin.Begin);
                    }
                }
                else
                    azureResult.StdOut = "";
                azureResult.StdOutStoredExternally = false;
            }
            if (result.StdErr.Length > MaxStdErrLength)
            {
                string stderrBlobId = BlobNameForStdErr(result.ExperimentID, result.BenchmarkFileName);
                await UploadOutput(stderrBlobId, result.StdErr, true);
                Trace.WriteLine(string.Format("Uploaded stderr for experiment {0}", result.ExperimentID));
                azureResult.StdErr = null;
                azureResult.StdErrStoredExternally = true;
            }
            else
            {
                if (result.StdErr.Length > 0)
                {
                    long pos = 0;
                    if (result.StdErr.CanSeek)
                    {
                        pos = result.StdErr.Position;
                        result.StdErr.Seek(0, SeekOrigin.Begin);
                    }
                    using (StreamReader sr = new StreamReader(result.StdErr))
                    {
                        azureResult.StdErr = await sr.ReadToEndAsync();
                    }
                    if (result.StdErr.CanSeek)
                    {
                        result.StdErr.Seek(pos, SeekOrigin.Begin);
                    }
                }
                else
                    azureResult.StdErr = "";
                azureResult.StdErrStoredExternally = false;
            }
            azureResult.TotalProcessorTime = result.TotalProcessorTime;
            azureResult.WallClockTime = result.WallClockTime;
            azureResult.WorkerInformation = result.WorkerInformation;

            return azureResult;
        }

        public BenchmarkResult ParseAzureBenchmarkResult(AzureBenchmarkResult azureResult)
        {
            return new BenchmarkResult(
                azureResult.ExperimentID,
                azureResult.BenchmarkFileName,
                azureResult.WorkerInformation,
                azureResult.AcquireTime,
                azureResult.NormalizedRuntime,
                azureResult.TotalProcessorTime,
                azureResult.WallClockTime,
                azureResult.PeakMemorySizeMB,
                azureResult.Status,
                azureResult.ExitCode,
                azureResult.StdOutStoredExternally ? new LazyBlobStream(outputContainer.GetBlobReference(BlobNameForStdOut(azureResult.ExperimentID, azureResult.BenchmarkFileName))) : Utils.StringToStream(azureResult.StdOut),
                azureResult.StdErrStoredExternally ? new LazyBlobStream(outputContainer.GetBlobReference(BlobNameForStdErr(azureResult.ExperimentID, azureResult.BenchmarkFileName))) : Utils.StringToStream(azureResult.StdErr),
                new ReadOnlyDictionary<string, string>(azureResult.Properties)
                );
        }

        private async Task<Tuple<string, string>> PutStdOutput(BenchmarkResult result)
        {
            string stdoutBlobId = "";
            string stderrBlobId = "";
            if (result.StdOut.Length > 0)
            {
                stdoutBlobId = BlobNameForStdOut(result.ExperimentID, result.BenchmarkFileName);
                await UploadOutput(stdoutBlobId, result.StdOut, true);
                Trace.WriteLine(string.Format("Uploaded stdout for experiment {0}", result.ExperimentID));
            }
            if (result.StdErr.Length > 0)
            {
                stderrBlobId = BlobNameForStdErr(result.ExperimentID, result.BenchmarkFileName);
                await UploadOutput(stderrBlobId, result.StdErr, true);
                Trace.WriteLine(string.Format("Uploaded stderr for experiment {0}", result.ExperimentID));
            }
            return Tuple.Create(stdoutBlobId, stderrBlobId);
        }

        private static string BlobNamePrefix(int experimentID)
        {
            return String.Concat("E", experimentID.ToString(), "F");
        }

        private static string BlobNameForStdErr(int experimentID, string benchmarkFileName)
        {
            return String.Concat(BlobNamePrefix(experimentID), benchmarkFileName, "-stderr");
        }

        private static string BlobNameForStdOut(int experimentID, string benchmarkFileName)
        {
            return String.Concat(BlobNamePrefix(experimentID), benchmarkFileName, "-stdout");
        }

        public Task UploadOutput(string blobName, Stream content, bool replaceIfExists)
        {
            return UploadBlobAsync(outputContainer, blobName, content, replaceIfExists);
        }


        private async Task UploadBlobAsync(CloudBlobContainer container, string blobName, Stream content, bool replaceIfExists)
        {
            try
            {
                var stdoutBlob = container.GetBlockBlobReference(blobName);
                if (!replaceIfExists && await stdoutBlob.ExistsAsync())
                {
                    Trace.WriteLine(string.Format("Blob {0} already exists", blobName));
                    return;
                }

                await stdoutBlob.UploadFromStreamAsync(content,
                    replaceIfExists ? AccessCondition.GenerateEmptyCondition() : AccessCondition.GenerateIfNotExistsCondition(),
                    new BlobRequestOptions()
                    {
                        RetryPolicy = new Microsoft.WindowsAzure.Storage.RetryPolicies.ExponentialRetry(TimeSpan.FromMilliseconds(100), 14)
                    },
                    null);
            }
            catch (StorageException ex)
            {
                var requestInfo = ex.RequestInformation;
                Trace.WriteLine(string.Format("Failed to upload text to the blob: {0}, blob name: {1}, response code: {2}", ex.Message, blobName, requestInfo.HttpStatusCode));
                throw;
            }
        }

        public async Task<CloudQueue> CreateResultsQueue(ExperimentID id)
        {
            var reference = queueClient.GetQueueReference(QueueNameForExperiment(id));
            await reference.CreateIfNotExistsAsync();
            return reference;
        }

        public CloudQueue GetResultsQueueReference(ExperimentID id)
        {
            return queueClient.GetQueueReference(QueueNameForExperiment(id));
        }

        public async Task DeleteResultsQueue(ExperimentID id)
        {
            var reference = queueClient.GetQueueReference(QueueNameForExperiment(id));
            await reference.DeleteIfExistsAsync();
        }

        private string QueueNameForExperiment(ExperimentID id)
        {
            return "exp" + id.ToString();
        }

        /// <summary>
        /// Puts the benchmark results of the given experiment to the storage.
        /// </summary>
        /// <param name="results">All results must have same experiment id.
        public async Task PutAzureExperimentResults(int expId, AzureBenchmarkResult[] results, bool replaceIfExists)
        {
            string fileName = GetResultsFileName(expId);
            using (MemoryStream zipStream = new MemoryStream())
            {
                using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    var entry = zip.CreateEntry(fileName);
                    AzureBenchmarkResult.SaveBenchmarks(results, entry.Open());
                }

                zipStream.Position = 0;
                await UploadBlobAsync(resultsContainer, GetResultBlobName(expId), zipStream, replaceIfExists);
            }
        }

        public async Task<AzureBenchmarkResult[]> GetAzureExperimentResults(ExperimentID experimentId)
        {
            AzureBenchmarkResult[] results;

            string blobName = GetResultBlobName(experimentId);
            var blob = resultsContainer.GetBlobReference(blobName);
            try
            {
                using (MemoryStream zipStream = new MemoryStream(4 << 20))
                {
                    await blob.DownloadToStreamAsync(zipStream,
                        AccessCondition.GenerateEmptyCondition(),
                        new Microsoft.WindowsAzure.Storage.Blob.BlobRequestOptions
                        {
                            RetryPolicy = new Microsoft.WindowsAzure.Storage.RetryPolicies.ExponentialRetry(TimeSpan.FromMilliseconds(100), 10)
                        }, null);

                    zipStream.Position = 0;
                    using (ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
                    {
                        var entry = zip.GetEntry(GetResultsFileName(experimentId));
                        using (var tableStream = entry.Open())
                        {
                            results = AzureBenchmarkResult.LoadBenchmarks(experimentId, tableStream);
                        }
                    }
                }
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == 404) // Not found == no results
                    return new AzureBenchmarkResult[] { };
                throw;
            }
            return results;
        }
    }
}
