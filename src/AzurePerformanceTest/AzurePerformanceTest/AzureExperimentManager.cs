using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PerformanceTest;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch;
using Measurement;
using ExperimentID = System.Int32;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.Batch.Common;
using System.IO;
using System.Diagnostics;

namespace AzurePerformanceTest
{
    public class AzureExperimentManager : ExperimentManager
    {
        public const string KeyBatchAccount = "BatchAccount";
        public const string KeyBatchURL = "BatchURL";
        public const string KeyBatchAccessKey = "BatchAccessKey";

        AzureExperimentStorage storage;
        BatchSharedKeyCredentials batchCreds;

        protected AzureExperimentManager(AzureExperimentStorage storage, string batchUrl, string batchAccName, string batchKey)
        {
            this.storage = storage;
            this.batchCreds = new BatchSharedKeyCredentials(batchUrl, batchAccName, batchKey);
        }

        protected AzureExperimentManager(AzureExperimentStorage storage)
        {
            this.storage = storage;
            this.batchCreds = null;
        }

        public static async Task<AzureExperimentManager> New(AzureExperimentStorage storage, ReferenceExperiment reference, string batchUrl, string batchAccName, string batchKey)
        {
            await storage.SaveReferenceExperiment(reference);
            return new AzureExperimentManager(storage, batchUrl, batchAccName, batchKey);
        }

        public static AzureExperimentManager Open(AzureExperimentStorage storage, string batchUrl, string batchAccName, string batchKey)
        {
            return new AzureExperimentManager(storage, batchUrl, batchAccName, batchKey);
        }

        public static AzureExperimentManager Open(string connectionString)
        {
            ConnectionString cs = new ConnectionString(connectionString);
            string batchAccountName = cs.TryGet(KeyBatchAccount);
            string batchUrl = cs.TryGet(KeyBatchURL);
            string batchAccessKey = cs.TryGet(KeyBatchAccessKey);

            cs.RemoveKeys(KeyBatchAccount, KeyBatchURL, KeyBatchAccessKey);
            string storageConnectionString = cs.ToString();

            AzureExperimentStorage storage = new AzureExperimentStorage(storageConnectionString);
            if (batchAccountName != null)
                return Open(storage, batchUrl, batchAccountName, batchAccessKey);
            return OpenWithoutStart(storage);
        }

        /// <summary>
        /// Creates a manager in a mode when it can open data but not start new experiments.
        /// </summary>
        public static AzureExperimentManager OpenWithoutStart(AzureExperimentStorage storage)
        {
            return new AzureExperimentManager(storage);
        }

        public AzureExperimentStorage Storage { get { return storage; } }

        public bool CanStart
        {
            get { return batchCreds != null; }
        }

        public override async Task DeleteExperiment(ExperimentID id)
        {
            await StopJob(id);

            // Removing experiment entity, results and outputs.
            await storage.DeleteExperiment(id);
        }

        /// If can connect to client and experiment is running, stopping the experiment job.
        private async Task StopJob(int id)
        {
            var jobId = BuildJobId(id);
            BatchClient bc;
            try
            {
                bc = await BatchClient.OpenAsync(batchCreds);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to open batch client when tried to stop the job: " + ex.Message);
                return;
            }

            
            try
            {
                await bc.JobOperations.DeleteJobAsync(jobId);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to delete the job " + jobId + ": " + ex.Message);
            }
            finally
            {
                bc.Dispose();
            }
        }

        public override Task DeleteExecutable(string executableName)
        {
            if (executableName == null) throw new ArgumentNullException("executableName");
            return storage.DeleteExecutable(executableName);
        }

        public override async Task<Experiment> TryFindExperiment(int id)
        {
            try
            {
                var e = await storage.GetExperiment(id);
                return ExperimentFromEntity(id, e);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public override async Task<IEnumerable<Experiment>> FindExperiments(ExperimentFilter? filter = default(ExperimentFilter?))
        {
            IEnumerable<KeyValuePair<int, ExperimentEntity>> experiments = await storage.GetExperiments(filter);

            return
                experiments
                .OrderByDescending(q => q.Value.Submitted)
                .Select(e => ExperimentFromEntity(e.Key, e.Value));
        }

        private Experiment ExperimentFromEntity(int id, ExperimentEntity entity)
        {
            var totalRuntime = TimeSpan.FromSeconds(entity.TotalRuntime);
            ExperimentDefinition def = DefinitionFromEntity(entity);
            ExperimentStatus status = new ExperimentStatus(id, def.Category, entity.Submitted, entity.Creator, entity.Note,
                entity.Flag, entity.CompletedBenchmarks, entity.TotalBenchmarks, totalRuntime);
            return new Experiment { Definition = def, Status = status };
        }

        private ExperimentDefinition DefinitionFromEntity(ExperimentEntity experimentEntity)
        {
            return ExperimentDefinition.Create(
                experimentEntity.Executable,
                experimentEntity.BenchmarkContainerUri,
                experimentEntity.BenchmarkDirectory,
                experimentEntity.BenchmarkFileExtension,
                experimentEntity.Parameters,
                TimeSpan.FromSeconds(experimentEntity.BenchmarkTimeout),
                experimentEntity.DomainName,
                experimentEntity.Category,
                experimentEntity.MemoryLimitMB);
        }

        public override async Task<BenchmarkResult[]> GetResults(ExperimentID id)
        {
            return await storage.GetResults(id);
        }

        public override async Task<IEnumerable<ExperimentStatus>> GetStatus(IEnumerable<ExperimentID> ids)
        {
            // todo: can be done in a more efficient way
            var req = ids.Select(id => storage.GetExperiment(id));
            var exps = await Task.WhenAll(req);
            return exps.Select(entity => ExperimentFromEntity(int.Parse(entity.RowKey), entity).Status);
        }
        public override async Task<ExperimentID> StartExperiment(ExperimentDefinition definition, string creator = null, string note = null)
        {
            if (!CanStart) throw new InvalidOperationException("Cannot start experiment since the manager is in read mode");

            var refExp = await storage.GetReferenceExperiment();
            var id = await storage.AddExperiment(definition, DateTime.Now, creator, note);
            //TODO: schedule execution

            using (var bc = BatchClient.Open(batchCreds))
            {
                CloudJob job = bc.JobOperations.CreateJob();
                job.Id = BuildJobId(id);
                job.OnAllTasksComplete = OnAllTasksComplete.TerminateJob;
                job.PoolInformation = new PoolInformation { PoolId = "testPool" };
                job.JobPreparationTask = new JobPreparationTask
                {
                    CommandLine = "cmd /c (robocopy %AZ_BATCH_TASK_WORKING_DIR% %AZ_BATCH_NODE_SHARED_DIR% /e /purge) ^& IF %ERRORLEVEL% LEQ 1 exit 0",
                    ResourceFiles = new List<ResourceFile>(),
                    WaitForSuccess = true
                };

                SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
                {
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(48),
                    Permissions = SharedAccessBlobPermissions.Read
                };

                foreach (CloudBlockBlob blob in storage.ListAzureWorkerBlobs())
                {
                    string sasBlobToken = blob.GetSharedAccessSignature(sasConstraints);
                    string blobSasUri = String.Format("{0}{1}", blob.Uri, sasBlobToken);
                    job.JobPreparationTask.ResourceFiles.Add(new ResourceFile(blobSasUri, blob.Name));
                }

                if (refExp != null)
                {
                    string refContentFolder = "refdata";
                    string refBenchFolder = Path.Combine(refContentFolder, "data");
                    var refExpExecUri = storage.GetExecutableSasUri(refExp.Definition.Executable);
                    job.JobPreparationTask.ResourceFiles.Add(new ResourceFile(refExpExecUri, Path.Combine(refContentFolder, refExp.Definition.Executable)));
                    AzureBenchmarkStorage benchStorage;
                    if (refExp.Definition.BenchmarkContainerUri == ExperimentDefinition.DefaultContainerUri)
                        benchStorage = storage.DefaultBenchmarkStorage;
                    else
                        benchStorage = new AzureBenchmarkStorage(refExp.Definition.BenchmarkContainerUri);

                    foreach (CloudBlockBlob blob in benchStorage.ListBlobs(refExp.Definition.BenchmarkDirectory, refExp.Definition.Category))
                    {
                        string[] parts = blob.Name.Split('/');
                        string shortName = parts[parts.Length - 1];
                        job.JobPreparationTask.ResourceFiles.Add(new ResourceFile(benchStorage.GetBlobSASUri(blob), Path.Combine(refBenchFolder, shortName)));
                    }
                }

                await job.CommitAsync();

                string taskId = "taskStarter";

                string taskCommandLine = string.Format("cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\AzureWorker.exe --add-tasks {0} \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\"", id, definition.BenchmarkContainerUri, definition.BenchmarkDirectory,
                    definition.Category, definition.Executable, definition.Parameters, definition.BenchmarkTimeout.TotalSeconds.ToString(), definition.MemoryLimitMB.ToString());
                CloudTask task = new CloudTask(taskId, taskCommandLine);

                await bc.JobOperations.AddTaskAsync(job.Id, task);
            }

            return id;
        }

        private static string BuildJobId(int experimentId)
        {
            return "exp" + experimentId.ToString();
        }

        public override async Task UpdateNote(int id, string note)
        {
            await storage.UpdateNote(id, note);
        }

        public override async Task UpdateStatusFlag(ExperimentID id, bool flag)
        {
            await storage.UpdateStatusFlag(id, flag);
        }
        public override Task UpdateResultStatus(ExperimentID id, ResultStatus status)
        {
            throw new NotImplementedException();
            //await storage.UpdateResultStatus(id, status);
        }
        public override Task UpdateRuntime(ExperimentID id, double runtime)
        {
            throw new NotImplementedException();
        }
    }
}
