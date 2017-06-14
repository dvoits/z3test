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
                int i = -1;
                string stdoutBlobId;
                do
                {
                    ++i;
                    stdoutBlobId = BlobNameForStdOut(result.ExperimentID, result.BenchmarkFileName, i.ToString());
                }
                while (!await UploadOutput(stdoutBlobId, result.StdOut, false));

                Trace.WriteLine(string.Format("Uploaded stdout for experiment {0}", result.ExperimentID));
                azureResult.StdOut = null;
                azureResult.StdOutExtStorageIdx = i.ToString();
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
                azureResult.StdOutExtStorageIdx = "";
            }
            if (result.StdErr.Length > MaxStdErrLength)
            {
                int i = -1;
                string stderrBlobId;
                do
                {
                    ++i;
                    stderrBlobId = BlobNameForStdErr(result.ExperimentID, result.BenchmarkFileName, i.ToString());
                }
                while (!await UploadOutput(stderrBlobId, result.StdErr, true));

                Trace.WriteLine(string.Format("Uploaded stderr for experiment {0}", result.ExperimentID));
                azureResult.StdErr = null;
                azureResult.StdErrExtStorageIdx = i.ToString();
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
                azureResult.StdErrExtStorageIdx = "";
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
                string.IsNullOrEmpty(azureResult.StdOutExtStorageIdx) ? Utils.StringToStream(azureResult.StdOut) : new LazyBlobStream(outputContainer.GetBlobReference(BlobNameForStdOut(azureResult.ExperimentID, azureResult.BenchmarkFileName, azureResult.StdOutExtStorageIdx))),
                string.IsNullOrEmpty(azureResult.StdErrExtStorageIdx) ? Utils.StringToStream(azureResult.StdErr) : new LazyBlobStream(outputContainer.GetBlobReference(BlobNameForStdErr(azureResult.ExperimentID, azureResult.BenchmarkFileName, azureResult.StdErrExtStorageIdx))),
                new ReadOnlyDictionary<string, string>(azureResult.Properties)
                );
        }

        private static string BlobNamePrefix(int experimentID)
        {
            return String.Concat("E", experimentID.ToString(), "F");
        }

        private static string BlobNameForStdErr(int experimentID, string benchmarkFileName, string index)
        {
            return String.Concat(BlobNamePrefix(experimentID), benchmarkFileName, "-stderr", index);
        }

        private static string BlobNameForStdOut(int experimentID, string benchmarkFileName, string index)
        {
            return String.Concat(BlobNamePrefix(experimentID), benchmarkFileName, "-stdout", index);
        }

        public async Task<bool> UploadOutput(string blobName, Stream content, bool replaceIfExists)
        {
            return await UploadBlobAsync(outputContainer, blobName, content, replaceIfExists);
        }


        private async Task<bool> UploadBlobAsync(CloudBlobContainer container, string blobName, Stream content, bool replaceIfExists)
        {
            try
            {
                var stdoutBlob = container.GetBlockBlobReference(blobName);
                if (!replaceIfExists && await stdoutBlob.ExistsAsync())
                {
                    Trace.WriteLine(string.Format("Blob {0} already exists", blobName));
                    return false;
                }

                await stdoutBlob.UploadFromStreamAsync(content,
                    replaceIfExists ? AccessCondition.GenerateEmptyCondition() : AccessCondition.GenerateIfNotExistsCondition(),
                    new BlobRequestOptions()
                    {
                        RetryPolicy = new Microsoft.WindowsAzure.Storage.RetryPolicies.ExponentialRetry(TimeSpan.FromMilliseconds(100), 14)
                    },
                    null);
                return true;
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == 412) // The update condition specified in the request was not satisfied.
                    return false;

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
