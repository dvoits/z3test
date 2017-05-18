using Measurement;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ExperimentID = System.Int32;

namespace AzurePerformanceTest
{
    public class AzureExperimentStorage
    {
        // Storage account
        private CloudStorageAccount storageAccount;
        private CloudBlobClient blobClient;
        private CloudBlobContainer binContainer;
        private CloudBlobContainer outputContainer;
        private CloudBlobContainer configContainer;
        private CloudTableClient tableClient;
        private CloudTable experimentsTable;
        private CloudTable resultsTable;

        const string binContainerName = "bin";
        const string outputContainerName = "output";
        const string configContainerName = "config";
        const string experimentsTableName = "experiments";
        const string resultsTableName = "data";

        public AzureExperimentStorage(string storageAccountName, string storageAccountKey) : this (String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", storageAccountName, storageAccountKey))
        {
        }

        public AzureExperimentStorage(string storageConnectionString)
        {
            storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            blobClient = storageAccount.CreateCloudBlobClient();
            binContainer = blobClient.GetContainerReference(binContainerName);
            outputContainer = blobClient.GetContainerReference(outputContainerName);
            configContainer = blobClient.GetContainerReference(configContainerName);

            tableClient = storageAccount.CreateCloudTableClient();
            experimentsTable = tableClient.GetTableReference(experimentsTableName);
            resultsTable = tableClient.GetTableReference(resultsTableName);

            var cloudEntityCreationTasks = new Task[] { binContainer.CreateIfNotExistsAsync(),
                outputContainer.CreateIfNotExistsAsync(),
                configContainer.CreateIfNotExistsAsync(),
                resultsTable.CreateIfNotExistsAsync(),
                experimentsTable.CreateIfNotExistsAsync()
            };
            Task.WaitAll(cloudEntityCreationTasks);
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

        public async Task<Dictionary<ExperimentID, ExperimentEntity>> GetExperiments()
        {
            var dict = new Dictionary<ExperimentID, ExperimentEntity>();
            TableQuery<ExperimentEntity> query = new TableQuery<ExperimentEntity>();
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


        public async Task<BenchmarkResult[]> GetResults(ExperimentID experimentID)
        {
            List<BenchmarkEntity> resultList = new List<BenchmarkEntity>();
            TableQuery<BenchmarkEntity> query = new TableQuery<BenchmarkEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, ExperimentEntity.ExperimentIDToString(experimentID)));
            TableContinuationToken continuationToken = null;

            do
            {
                TableQuerySegment<BenchmarkEntity> tableQueryResult =
                    await resultsTable.ExecuteQuerySegmentedAsync(query, continuationToken);

                continuationToken = tableQueryResult.ContinuationToken;
                resultList.AddRange(tableQueryResult.Results);
            } while (continuationToken != null);

            return resultList.Select(row =>
                new BenchmarkResult(experimentID, row.BenchmarkFileName, row.WorkerInformation, row.NormalizedRuntime, row.AcquireTime,
                    new ProcessRunMeasure(TimeSpan.FromSeconds(row.TotalProcessorTime), TimeSpan.FromSeconds(row.WallClockTime), row.PeakMemorySize << 20,
                        StatusFromString(row.Status), row.ExitCode, new LazyBlobStream(outputContainer.GetBlobReference(row.StdOut)), new LazyBlobStream(outputContainer.GetBlobReference(row.StdErr))))
                ).ToArray();
        }

        private Measure.CompletionStatus StatusFromString(string status)
        {
            return (Measure.CompletionStatus)Enum.Parse(typeof(Measure.CompletionStatus), status);
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
            string idEntityFilter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, NextExperimentIDEntity.NextIDPartition),
                TableOperators.And,
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, NextExperimentIDEntity.NextIDRow));
            TableQuery<NextExperimentIDEntity> idEntityQuery = new TableQuery<NextExperimentIDEntity>().Where(idEntityFilter);

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

                    try
                    {
                        await experimentsTable.ExecuteAsync(TableOperation.Insert(nextId));
                    }
                    catch (StorageException ex)
                    {
                        if (ex.RequestInformation.HttpStatusCode == 409) // The specified entity already exists.
                        {
                            //Someone created ID entity before us
                            idChanged = true;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                else
                {
                    nextId = list[0];
                    id = nextId.Id;
                    nextId.Id = id + 1;

                    try
                    {
                        TableResult tblr = await experimentsTable.ExecuteAsync(TableOperation.InsertOrReplace(nextId), null, new OperationContext { UserHeaders = new Dictionary<String, String> { { "If-Match", nextId.ETag } } });
                    }
                    catch (StorageException ex)
                    {
                        if (ex.RequestInformation.HttpStatusCode == 412) // The update condition specified in the request was not satisfied.
                        {
                            //Someone modified ID entity before us
                            idChanged = true;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            } while (idChanged);

            var row = new ExperimentEntity(id);
            row.Submitted = submitted;
            row.Executable = experiment.Executable;
            row.Parameters = experiment.Parameters;
            row.BenchmarkContainer = experiment.BenchmarkContainer;
            row.BenchmarkFileExtension = experiment.BenchmarkFileExtension;
            row.Category = experiment.Category;
            row.BenchmarkTimeout = experiment.BenchmarkTimeout.TotalSeconds;
            row.ExperimentTimeout = experiment.ExperimentTimeout.TotalSeconds;
            row.MemoryLimit = (int)(experiment.MemoryLimit >> 20); // bytes to MB
            row.GroupName = experiment.GroupName;
            row.Note = note;
            row.Creator = creator;

            TableOperation insertOperation = TableOperation.Insert(row);
            await experimentsTable.ExecuteAsync(insertOperation);
            return id;
        }

        public async Task<ExperimentEntity> GetExperiment(ExperimentID id)
        {
            TableQuery<ExperimentEntity> query = new TableQuery<ExperimentEntity>().Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, ExperimentEntity.ExperimentIDToString(id)));

            var list = (await experimentsTable.ExecuteQuerySegmentedAsync(query, null)).ToList();

            if (list.Count == 0)
                throw new ArgumentException("Experiment with given ID not found");

            return list[0];
        }

        public async Task UpdateNote(int id, string note)
        {
            throw new NotImplementedException();
        }

        public async Task UpdateStatusFlag(ExperimentID id, bool flag)
        {
            throw new NotImplementedException();
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
            return id.ToString().PadLeft(6, '0');
        }
        public static string PartitionKeyDefault = "default";

        public ExperimentEntity (string id)
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
        public string Parameters { get; set; }
        public string BenchmarkContainer { get; set; }
        public string Category { get; set; }
        public string BenchmarkFileExtension { get; set; }
        /// <summary>
        /// MegaBytes.
        /// </summary>
        public int MemoryLimit { get; set; }
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
    }

    public class NextExperimentIDEntity : TableEntity
    {
        public int Id { get; set; }
        public static string NextIDPartition = "NextIDPartition";
        public static string NextIDRow = "NextIDRow";

        public NextExperimentIDEntity() {
            this.PartitionKey = NextIDPartition;
            this.RowKey = NextIDRow;
        }
    }

    public class BenchmarkEntity : TableEntity
    {
        public static string IDFromFileName(string fileName)
        {
            return fileName.Replace('\\', '>').Replace('/', '<');
        }
        public BenchmarkEntity(string experimentID, string benchmarkID)
        {
            this.PartitionKey = experimentID;
            this.RowKey = IDFromFileName(benchmarkID);
            this.BenchmarkFileName = benchmarkID;
        }
        public BenchmarkEntity(int experimentID, string benchmarkID)
        {
            this.PartitionKey = ExperimentEntity.ExperimentIDToString(experimentID);
            this.RowKey = IDFromFileName(benchmarkID);
            this.BenchmarkFileName = benchmarkID;
        }
        public BenchmarkEntity() { }
        public string BenchmarkFileName { get; set; }
        public DateTime AcquireTime { get; set; }
        public double NormalizedRuntime { get; set; }
        public double TotalProcessorTime { get; set; }
        public double WallClockTime { get; set; }
        public int PeakMemorySize { get; set; }
        public string Status { get; set; }
        public int ExitCode { get; set; }

        public string StdOut { get; set; }
        public string StdErr { get; set; }
        public string WorkerInformation { get; set; }

    }
}
