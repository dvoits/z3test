﻿using Measurement;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PerformanceTest;
using System;
using System.Collections.Generic;
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
        // Storage account
        private CloudStorageAccount storageAccount;
        private CloudBlobClient blobClient;
        private CloudBlobContainer binContainer;
        private CloudBlobContainer resultsContainer;
        private CloudBlobContainer outputContainer;
        private CloudBlobContainer configContainer;
        private CloudTableClient tableClient;
        private CloudTable experimentsTable;
        private CloudTable resultsTable;
        private CloudQueueClient queueClient;



        const string resultsContainerName = "results";
        const string binContainerName = "bin";
        const string outputContainerName = "output";
        const string configContainerName = "config";
        const string experimentsTableName = "experiments";
        const string resultsTableName = "data";


        public AzureExperimentStorage(string storageAccountName, string storageAccountKey) : this(String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", storageAccountName, storageAccountKey))
        {
        }

        public AzureExperimentStorage(string storageConnectionString)
        {
            storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            blobClient = storageAccount.CreateCloudBlobClient();
            binContainer = blobClient.GetContainerReference(binContainerName);
            outputContainer = blobClient.GetContainerReference(outputContainerName);
            configContainer = blobClient.GetContainerReference(configContainerName);
            resultsContainer = blobClient.GetContainerReference(resultsContainerName);

            tableClient = storageAccount.CreateCloudTableClient();
            experimentsTable = tableClient.GetTableReference(experimentsTableName);
            resultsTable = tableClient.GetTableReference(resultsTableName);

            queueClient = storageAccount.CreateCloudQueueClient();

            var cloudEntityCreationTasks = new Task[] {
                binContainer.CreateIfNotExistsAsync(),
                outputContainer.CreateIfNotExistsAsync(),
                configContainer.CreateIfNotExistsAsync(),
                resultsTable.CreateIfNotExistsAsync(),
                experimentsTable.CreateIfNotExistsAsync(),
                resultsContainer.CreateIfNotExistsAsync()
            };
            Task.WaitAll(cloudEntityCreationTasks);

            InitializeCache();
        }

        public IEnumerable<CloudBlockBlob> ListAzureWorkerBlobs()
        {
            return configContainer.ListBlobs().Select(listItem => listItem as CloudBlockBlob);
        }

        public async Task SaveReferenceExperiment(ReferenceExperiment reference)
        {
            string json = JsonConvert.SerializeObject(reference, Formatting.Indented);
            var blob = configContainer.GetBlockBlobReference("reference.json");
            await blob.UploadTextAsync(json);
        }

        public async Task<ReferenceExperiment> GetReferenceExperiment()
        {
            var blob = configContainer.GetBlockBlobReference("reference.json");
            string content = await blob.DownloadTextAsync();
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ContractResolver = new PrivatePropertiesResolver();
            ReferenceExperiment reference = JsonConvert.DeserializeObject<ReferenceExperiment>(content, settings);
            return reference;
        }

        internal class PrivatePropertiesResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty prop = base.CreateProperty(member, memberSerialization);
                prop.Writable = true;
                return prop;
            }
        }

        public async Task<Dictionary<ExperimentID, ExperimentEntity>> GetExperiments(ExperimentManager.ExperimentFilter? filter = default(ExperimentManager.ExperimentFilter?))
        {
            var dict = new Dictionary<ExperimentID, ExperimentEntity>();
            TableQuery<ExperimentEntity> query = new TableQuery<ExperimentEntity>();
            List<string> experimentFilters = new List<string>();
            experimentFilters.Add(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, ExperimentEntity.PartitionKeyDefault));

            if (filter.HasValue)
            {
                if (filter.Value.BenchmarkContainerEquals != null)
                    experimentFilters.Add(TableQuery.GenerateFilterCondition("BenchmarkContainer", QueryComparisons.Equal, filter.Value.BenchmarkContainerEquals));
                if (filter.Value.CategoryEquals != null)
                    experimentFilters.Add(TableQuery.GenerateFilterCondition("Category", QueryComparisons.Equal, filter.Value.CategoryEquals));
                if (filter.Value.ExecutableEquals != null)
                    experimentFilters.Add(TableQuery.GenerateFilterCondition("Executable", QueryComparisons.Equal, filter.Value.ExecutableEquals));
                if (filter.Value.ParametersEquals != null)
                    experimentFilters.Add(TableQuery.GenerateFilterCondition("Parameters", QueryComparisons.Equal, filter.Value.ParametersEquals));
                if (filter.Value.NotesEquals != null)
                    experimentFilters.Add(TableQuery.GenerateFilterCondition("Note", QueryComparisons.Equal, filter.Value.NotesEquals));
                if (filter.Value.CreatorEquals != null)
                    experimentFilters.Add(TableQuery.GenerateFilterCondition("Creator", QueryComparisons.Equal, filter.Value.CreatorEquals));
            }

            if (experimentFilters.Count > 0)
            {
                string finalFilter = experimentFilters[0];
                for (int i = 1; i < experimentFilters.Count; ++i)
                    finalFilter = TableQuery.CombineFilters(finalFilter, TableOperators.And, experimentFilters[i]);

                query = query.Where(finalFilter);
            }

            TableContinuationToken continuationToken = null;

            do
            {
                TableQuerySegment<ExperimentEntity> tableQueryResult =
                    await experimentsTable.ExecuteQuerySegmentedAsync(query, continuationToken);

                continuationToken = tableQueryResult.ContinuationToken;
                foreach (var e in tableQueryResult.Results)
                    dict.Add(int.Parse(e.RowKey), e);
            } while (continuationToken != null);

            return dict;
        }

        public async Task PutResult(ExperimentID expId, BenchmarkResult result)
        {
            var queue = GetResultsQueueReference(expId);
            var stdBlobs = await PutStdOutput(result);
            var result2 = ReplaceStreamsWithBlobNames(result, stdBlobs.Item1, stdBlobs.Item2);
            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(1);//signalling that this message contains a result
                (new BinaryFormatter()).Serialize(ms, result2);
                await queue.AddMessageAsync(new CloudQueueMessage(ms.ToArray()));
            }
        }

        private async Task<Tuple<string, string>> PutStdOutput(BenchmarkResult result)
        {
            string stdoutBlobId = "";
            string stderrBlobId = "";
            if (result.StdOut.Length > 0)
            {
                stdoutBlobId = "E" + result.ExperimentID.ToString() + "F" + result.BenchmarkFileName + "-stdout";
                var stdoutBlob = outputContainer.GetBlockBlobReference(stdoutBlobId);

                // todo: add try/catch for StorageException: https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.storage.blob.cloudblockblob.uploadfromstreamasync?redirectedfrom=MSDN&view=azure-dotnet#overloads
                await stdoutBlob.UploadFromStreamAsync(result.StdOut);
                Trace.WriteLine(string.Format("Uploaded stdout for experiment {0}", result.ExperimentID));
            }
            if (result.StdErr.Length > 0)
            {
                stderrBlobId = "E" + result.ExperimentID.ToString() + "F" + result.BenchmarkFileName + "-stderr";
                var stderrBlob = outputContainer.GetBlockBlobReference(stderrBlobId);

                await stderrBlob.UploadFromStreamAsync(result.StdErr);
                Trace.WriteLine(string.Format("Uploaded stderr for experiment {0}", result.ExperimentID));
            }
            return Tuple.Create(stdoutBlobId, stderrBlobId);
        }

        public async Task UploadOutput(string blobName, string content)
        {
            var stdoutBlob = outputContainer.GetBlockBlobReference(blobName);

            int n = 1, m = 10;
            do
            {
                try
                {
                    await stdoutBlob.UploadTextAsync(content);
                    break;
                }
                catch (StorageException ex)
                {
                    var requestInfo = ex.RequestInformation;
                    Trace.WriteLine(string.Format("Failed to upload text to the blob: {0}\nRequest information: {1}", ex.Message, requestInfo));

                    var exInfo = requestInfo.ExtendedErrorInformation;

                    var errorCode = exInfo.ErrorCode;
                    var message = string.Format("({0}) {1}", errorCode, exInfo.ErrorMessage);

                    var details = exInfo
                        .AdditionalDetails
                        .Aggregate("", (s, pair) =>
                        {
                            return s + string.Format("{0}={1},", pair.Key, pair.Value);
                        });

                    Trace.WriteLine(message + ", details: " + details);

                    if (n++ == m) throw;
                    Trace.WriteLine(string.Format("Attempt {0}/{1}...", n, m));
                }
            } while (true);
        }

        public async Task PutSerializedExperimentResults(ExperimentID expId, IEnumerable<string> results)
        {
            string fileName = GetResultsFileName(expId);
            string header = "BenchmarkFileName,AcquireTime,NormalizedRuntime,TotalProcessorTime,WallClockTime,PeakMemorySize,Status,ExitCode,StdOut,StdErr,WorkerInformation";

            using (MemoryStream zipStream = new MemoryStream())
            {
                using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    var entry = zip.CreateEntry(fileName);
                    using (var sw = new StreamWriter(entry.Open()))
                    {
                        sw.WriteLine(header);
                        foreach (var s in results)
                            sw.WriteLine(s);
                    }
                }

                var blob = resultsContainer.GetBlockBlobReference(GetResultBlobName(expId));
                zipStream.Position = 0;
                await UploadBlobAsync(zipStream, blob);
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

        public CloudBlockBlob GetExecutableReference(string name)
        {
            return binContainer.GetBlockBlobReference(name);
        }

        public string GetExecutableSasUri(string name)
        {
            var blob = binContainer.GetBlobReference(name);
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(48),
                Permissions = SharedAccessBlobPermissions.Read
            };
            string signature = blob.GetSharedAccessSignature(sasConstraints);
            return blob.Uri + signature;
        }

        private static string GetResultsFileName(int expId)
        {
            return String.Format("{0}.csv", expId);
        }

        private static string GetResultBlobName(ExperimentID expId)
        {
            return String.Format("{0}.csv.zip", expId);
        }

        private Measure.LimitsStatus StatusFromString(string status)
        {
            return (Measure.LimitsStatus)Enum.Parse(typeof(Measure.LimitsStatus), status);
        }

        /// <summary>
        /// Adds new entry to the experiments table
        /// </summary>
        /// <param name="experiment"></param>
        /// <param name="submitted"></param>
        /// <param name="creator"></param>
        /// <param name="note"></param>
        /// <returns>ID of newly created entry</returns>
        public async Task<int> AddExperiment(ExperimentDefinition experiment, DateTime submitted, string creator, string note)
        {
            TableQuery<NextExperimentIDEntity> idEntityQuery = QueryForNextId();

            bool idChanged;
            int id = -1;

            do
            {
                idChanged = false;

                var list = (await experimentsTable.ExecuteQuerySegmentedAsync(idEntityQuery, null)).ToList();

                NextExperimentIDEntity nextId = null;

                if (list.Count == 0)
                {
                    nextId = new NextExperimentIDEntity();
                    id = 1;
                    nextId.Id = 2;

                    idChanged = !(await TryInsertTableEntity(experimentsTable, nextId));
                }
                else
                {
                    nextId = list[0];
                    id = nextId.Id;
                    nextId.Id = id + 1;

                    idChanged = !(await TryUpdateTableEntity(experimentsTable, nextId));
                }
            } while (idChanged);

            var row = new ExperimentEntity(id);
            row.Submitted = submitted;
            row.Executable = experiment.Executable;
            row.DomainName = experiment.DomainName;
            row.Parameters = experiment.Parameters;
            row.BenchmarkDirectory = experiment.BenchmarkDirectory;
            row.BenchmarkFileExtension = experiment.BenchmarkFileExtension;
            row.Category = experiment.Category;
            row.BenchmarkTimeout = experiment.BenchmarkTimeout.TotalSeconds;
            row.ExperimentTimeout = experiment.ExperimentTimeout.TotalSeconds;
            row.MemoryLimitMB = experiment.MemoryLimitMB;
            row.GroupName = experiment.GroupName;
            row.Note = note;
            row.Creator = creator;

            TableOperation insertOperation = TableOperation.Insert(row);
            await experimentsTable.ExecuteAsync(insertOperation);
            return id;
        }

        private static TableQuery<NextExperimentIDEntity> QueryForNextId()
        {
            string idEntityFilter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, NextExperimentIDEntity.NextIDPartition),
                TableOperators.And,
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, NextExperimentIDEntity.NextIDRow));
            TableQuery<NextExperimentIDEntity> idEntityQuery = new TableQuery<NextExperimentIDEntity>().Where(idEntityFilter);
            return idEntityQuery;
        }

        public async Task<ExperimentEntity> GetExperiment(ExperimentID id)
        {
            TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

            return await FirstExperimentInQuery(query);
        }

        public async Task UpdateNote(int id, string note)
        {
            TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

            bool changed = false;
            do
            {
                ExperimentEntity experiment = await FirstExperimentInQuery(query);
                experiment.Note = note;

                changed = !(await TryUpdateTableEntity(experimentsTable, experiment));
            } while (changed);
        }

        public async Task SetTotalBenchmarks(int id, int totalBenchmarks)
        {
            TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

            bool changed = false;
            do
            {
                ExperimentEntity experiment = await FirstExperimentInQuery(query);
                experiment.TotalBenchmarks = totalBenchmarks;

                changed = !(await TryUpdateTableEntity(experimentsTable, experiment));
            } while (changed);
        }

        public async Task SetCompletedBenchmarks(int id, int completedBenchmarks)
        {
            TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

            bool changed = false;
            do
            {
                ExperimentEntity experiment = await FirstExperimentInQuery(query);
                experiment.CompletedBenchmarks = completedBenchmarks;

                changed = !(await TryUpdateTableEntity(experimentsTable, experiment));
            } while (changed);
        }

        public async Task SetTotalRuntime(int id, double totalRuntime)
        {
            TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

            bool changed = false;
            do
            {
                ExperimentEntity experiment = await FirstExperimentInQuery(query);
                experiment.TotalRuntime = totalRuntime;

                changed = !(await TryUpdateTableEntity(experimentsTable, experiment));
            } while (changed);
        }

        public async Task IncreaseCompletedBenchmarks(int id, int completedBenchmarksRaise)
        {
            TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

            bool changed = false;
            do
            {
                ExperimentEntity experiment = await FirstExperimentInQuery(query);
                experiment.CompletedBenchmarks += completedBenchmarksRaise;

                changed = !(await TryUpdateTableEntity(experimentsTable, experiment));
            } while (changed);
        }

        public async Task UpdateStatusFlag(ExperimentID id, bool flag)
        {
            TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

            bool changed = false;
            do
            {
                ExperimentEntity experiment = await FirstExperimentInQuery(query);
                experiment.Flag = flag;

                changed = !(await TryUpdateTableEntity(experimentsTable, experiment));
            } while (changed);
        }

        private async Task<ExperimentEntity> FirstExperimentInQuery(TableQuery<ExperimentEntity> query)
        {
            var list = (await experimentsTable.ExecuteQuerySegmentedAsync(query, null)).ToList();

            if (list.Count == 0)
                throw new ArgumentException("Experiment with given ID not found");

            var experiment = list[0];
            return experiment;
        }

        private static TableQuery<ExperimentEntity> ExperimentPointQuery(int id)
        {
            string experimentEntityFilter = TableQuery.CombineFilters(
                               TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, ExperimentEntity.PartitionKeyDefault),
                               TableOperators.And,
                               TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, ExperimentEntity.ExperimentIDToString(id)));
            TableQuery<ExperimentEntity> query = new TableQuery<ExperimentEntity>().Where(experimentEntityFilter);
            return query;
        }

        private static async Task<bool> TryInsertTableEntity(CloudTable table, ITableEntity entity)
        {
            try
            {
                await table.ExecuteAsync(TableOperation.Insert(entity));
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == 409) // The specified entity already exists.
                {
                    //Someone inserted entity before us
                    return false;
                }
                else
                {
                    throw;
                }
            }
            return true;
        }

        private static async Task<bool> TryUpdateTableEntity(CloudTable table, ITableEntity entity)
        {
            try
            {
                await table.ExecuteAsync(TableOperation.InsertOrReplace(entity), null, new OperationContext { UserHeaders = new Dictionary<string, string> { { "If-Match", entity.ETag } } });
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == 412) // The update condition specified in the request was not satisfied.
                {
                    //Someone modified entity before us
                    return false;
                }
                else
                {
                    throw;
                }
            }
            return true;
        }
    }

    public class LazyBlobStream : Stream
    {
        private CloudBlob blob;
        private Stream stream = null;
        public LazyBlobStream(CloudBlob blob)
        {
            this.blob = blob;
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (stream == null)
                stream = blob.OpenRead();

            return stream.Read(buffer, offset, count);
        }

        bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                if (stream != null)
                {
                    stream.Dispose();
                    stream = null;
                }
            }

            disposed = true;

            base.Dispose(disposing);
        }
    }

    public class ExperimentEntity : TableEntity
    {
        public static string ExperimentIDToString(ExperimentID id)
        {
            return id.ToString();//.PadLeft(6, '0');
        }
        public const string PartitionKeyDefault = "default";

        public ExperimentEntity(string id)
        {
            this.PartitionKey = PartitionKeyDefault;
            this.RowKey = id;
        }
        public ExperimentEntity(int id)
        {
            this.PartitionKey = PartitionKeyDefault;
            this.RowKey = ExperimentIDToString(id);
        }
        public ExperimentEntity() { }
        public DateTime Submitted { get; set; }
        public string Executable { get; set; }
        public string DomainName { get; set; }
        public string Parameters { get; set; }
        public string BenchmarkContainerUri { get; set; }
        public string BenchmarkDirectory { get; set; }
        public string Category { get; set; }
        public string BenchmarkFileExtension { get; set; }
        /// <summary>
        /// MegaBytes.
        /// </summary>
        public double MemoryLimitMB { get; set; }
        /// <summary>
        /// Seconds.
        /// </summary>
        public double BenchmarkTimeout { get; set; }
        /// <summary>
        /// Seconds.
        /// </summary>
        public double ExperimentTimeout { get; set; }
        public string Note { get; set; }
        public string Creator { get; set; }
        public bool Flag { get; set; }
        public string GroupName { get; set; }
        public int TotalBenchmarks { get; set; }
        public int CompletedBenchmarks { get; set; }
        /// <summary>
        /// Seconds.
        /// </summary>
        public double TotalRuntime { get; set; }
    }

    public class NextExperimentIDEntity : TableEntity
    {
        public int Id { get; set; }
        public const string NextIDPartition = "NextIDPartition";
        public const string NextIDRow = "NextIDRow";

        public NextExperimentIDEntity()
        {
            this.PartitionKey = NextIDPartition;
            this.RowKey = NextIDRow;
        }
    }
}
