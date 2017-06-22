﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch;
using ExperimentID = System.Int32;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.Batch.Common;
using System.IO;
using System.Diagnostics;
using PerformanceTest;
using Measurement;

namespace AzurePerformanceTest
{
    public class AzureExperimentManager : ExperimentManager
    {
        const int MaxTaskRetryCount = 5;
        const string DefaultPoolID = "testPool";

        AzureExperimentStorage storage;
        BatchSharedKeyCredentials batchCreds;

        protected AzureExperimentManager(AzureExperimentStorage storage, string batchUrl, string batchAccName, string batchKey)
        {
            this.storage = storage;
            this.batchCreds = new BatchSharedKeyCredentials(batchUrl, batchAccName, batchKey);
            this.BatchPoolID = DefaultPoolID;
        }

        protected AzureExperimentManager(AzureExperimentStorage storage)
        {
            this.storage = storage;
            this.batchCreds = null;
            this.BatchPoolID = DefaultPoolID;
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
            var cs = new BatchConnectionString(connectionString);
            string batchAccountName = cs.BatchAccountName;
            string batchUrl = cs.BatchURL;
            string batchAccessKey = cs.BatchAccessKey;

            cs.RemoveKeys(BatchConnectionString.KeyBatchAccount, BatchConnectionString.KeyBatchURL, BatchConnectionString.KeyBatchAccessKey);
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

        public string BatchPoolID { get; set; }

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

        /// If connection to the batch client is established and experiment is running, deletes the experiment job.
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
            ExperimentStatus status = new ExperimentStatus(
                id, def.Category, entity.Submitted, entity.Creator, entity.Note,
                entity.Flag, entity.CompletedBenchmarks, entity.TotalBenchmarks, totalRuntime, entity.WorkerInformation);
            return new Experiment { Definition = def, Status = status };
        }

        public override async Task<ExperimentExecutionState[]> GetExperimentJobState(IEnumerable<int> ids)
        {
            if (!CanStart) return null;

            try
            {
                using (var bc = BatchClient.Open(batchCreds))
                {
                    List<ExperimentExecutionState> states = new List<ExperimentExecutionState>();
                    foreach (var expId in ids)
                    {
                        var jobId = BuildJobId(expId);
                        try
                        {
                            var job = await bc.JobOperations.GetJobAsync(jobId);
                            if (job.State == null) states.Add(ExperimentExecutionState.NotFound);
                            switch (job.State.Value)
                            {
                                case JobState.Active:
                                case JobState.Disabling:
                                case JobState.Disabled:
                                case JobState.Enabling:
                                    states.Add(ExperimentExecutionState.Active);
                                    break;
                                case JobState.Completed:
                                    states.Add(ExperimentExecutionState.Completed);
                                    break;
                                case JobState.Terminating:
                                case JobState.Deleting:
                                    states.Add(ExperimentExecutionState.Terminated);
                                    break;
                                default:
                                    throw new InvalidOperationException("Unexpected job status");
                            }
                        }
                        catch (BatchException batchExc) when (batchExc.RequestInformation != null && batchExc.RequestInformation.HttpStatusCode.HasValue && batchExc.RequestInformation.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            states.Add(ExperimentExecutionState.NotFound);
                        }
                    }
                    return states.ToArray();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Failed to get job status: " + ex);
                return null;
            }
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

        public async Task<PoolDescription[]> GetAvailablePools()
        {
            if (!CanStart) throw new InvalidOperationException("Cannot start experiment since the manager is in read mode");

            var result = await Task.Run(() =>
            {
                using (var bc = BatchClient.Open(batchCreds))
                {
                    var pools = bc.PoolOperations.ListPools();
                    var descr = pools.Select(p => new PoolDescription
                    {
                        Id = p.Id,
                        AllocationState = p.AllocationState,
                        PoolState = p.State,
                        DedicatedNodes = p.CurrentDedicatedComputeNodes ?? 0,
                        VirtualMachineSize = p.VirtualMachineSize,
                        RunningJobs = 0
                    }).ToArray();

                    var jobPools =
                        bc.JobOperations.ListJobs()
                        .Where(j => j.State != null && (j.State == JobState.Enabling || j.State == JobState.Active))
                        .Select(j => j.PoolInformation.PoolId);

                    Dictionary<string, int> count = new Dictionary<string, ExperimentID>();
                    foreach (var poolId in jobPools)
                    {
                        int n;
                        if (count.TryGetValue(poolId, out n))
                            count[poolId] = n + 1;
                        else
                            count[poolId] = 1;
                    }

                    foreach (var pool in descr)
                    {
                        int n;
                        if (count.TryGetValue(pool.Id, out n))
                            pool.RunningJobs = n;
                    }

                    return descr;
                }
            });
            return result;
        }

        public override async Task<ExperimentID> StartExperiment(ExperimentDefinition definition, string creator = null, string note = null)
        {
            if (!CanStart) throw new InvalidOperationException("Cannot start experiment since the manager is in read mode");

            var refExp = await storage.GetReferenceExperiment();
            var poolId = this.BatchPoolID;
            int id;

            using (var bc = BatchClient.Open(batchCreds))
            {
                var pool = await bc.PoolOperations.GetPoolAsync(poolId);
                id = await storage.AddExperiment(definition, DateTime.Now, creator, note, pool.VirtualMachineSize);
                CloudJob job = bc.JobOperations.CreateJob();
                job.Id = BuildJobId(id);
                job.OnAllTasksComplete = OnAllTasksComplete.TerminateJob;
                job.PoolInformation = new PoolInformation { PoolId = poolId };
                job.JobPreparationTask = new JobPreparationTask
                {
                    CommandLine = "cmd /c (robocopy %AZ_BATCH_TASK_WORKING_DIR% %AZ_BATCH_NODE_SHARED_DIR%\\" + job.Id + " /e /purge) ^& IF %ERRORLEVEL% LEQ 1 exit 0",
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
                
                string executableFolder = "exec";
                job.JobPreparationTask.ResourceFiles.Add(new ResourceFile(storage.GetExecutableSasUri(definition.Executable), Path.Combine(executableFolder, definition.Executable)));

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

                    Domain refdomain;
                    if (refExp.Definition.DomainName == "Z3")
                        refdomain = new Z3Domain();
                    else
                        throw new InvalidOperationException("Reference experiment uses unknown domain.");

                    SortedSet<string> extensions;
                    if (string.IsNullOrEmpty(refExp.Definition.BenchmarkFileExtension))
                        extensions = new SortedSet<string>(refdomain.BenchmarkExtensions.Distinct());
                    else
                        extensions = new SortedSet<string>(refExp.Definition.BenchmarkFileExtension.Split('|').Select(s => s.Trim().TrimStart('.')).Distinct());

                    foreach (CloudBlockBlob blob in benchStorage.ListBlobs(refExp.Definition.BenchmarkDirectory, refExp.Definition.Category))
                    {
                        string[] parts = blob.Name.Split('/');
                        string shortName = parts[parts.Length - 1];
                        var shortnameParts = shortName.Split('.');
                        if (shortnameParts.Length == 1 && !extensions.Contains(""))
                            continue;
                        var ext = shortnameParts[shortnameParts.Length - 1];
                        if (!extensions.Contains(ext))
                            continue;

                        job.JobPreparationTask.ResourceFiles.Add(new ResourceFile(benchStorage.GetBlobSASUri(blob), Path.Combine(refBenchFolder, shortName)));
                    }
                }

                job.Constraints = new JobConstraints();

                if (definition.ExperimentTimeout != TimeSpan.Zero)
                    job.Constraints.MaxWallClockTime = definition.ExperimentTimeout;

                job.Constraints.MaxTaskRetryCount = MaxTaskRetryCount;
                string taskId = "taskStarter";

                string taskCommandLine = string.Format("cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\" + job.Id + "\\AzureWorker.exe --manage-tasks {0} \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\" \"{8}\" \"{9}\"", 
                    id, definition.BenchmarkContainerUri, definition.BenchmarkDirectory,
                    definition.Category, definition.BenchmarkFileExtension, definition.DomainName, definition.Executable, definition.Parameters, 
                    definition.BenchmarkTimeout.TotalSeconds.ToString(), definition.MemoryLimitMB.ToString());

                job.JobManagerTask = new JobManagerTask(taskId, taskCommandLine);

                await job.CommitAsync();
            }

            return id;
        }

        public async override Task RestartBenchmarks(int id, IEnumerable<string> benchmarkNames, string newBenchmarkContainerUri = null)
        {
            if (!CanStart) throw new InvalidOperationException("Cannot start experiment since the manager is in read mode");

            var exp = await storage.GetExperiment(id);
            if (newBenchmarkContainerUri == null)
            {
                if (exp.BenchmarkContainerUri != ExperimentDefinition.DefaultContainerUri)
                    throw new ArgumentException("No newBenchmarkContainerUri provided, but experiment uses a non-default container.");
                else
                    newBenchmarkContainerUri = ExperimentDefinition.DefaultContainerUri;
            }
            var refExp = await storage.GetReferenceExperiment();
            var poolId = this.BatchPoolID;

            var jobId = BuildJobId(id);

            string tempBlobName = Guid.NewGuid().ToString();
            await storage.TempBlobContainer.GetBlockBlobReference(tempBlobName).UploadTextAsync(string.Join("\n", benchmarkNames));

            using (var bc = BatchClient.Open(batchCreds))
            {
                //var pool = await bc.PoolOperations.GetPoolAsync(poolId);
                try
                {
                    await bc.JobOperations.DeleteJobAsync(jobId);
                }
                catch (BatchException batchExc) when (batchExc.RequestInformation != null && batchExc.RequestInformation.HttpStatusCode.HasValue && batchExc.RequestInformation.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    //Not found - nothing to delete
                }

                CloudJob job = bc.JobOperations.CreateJob();
                job.Id = jobId;
                job.OnAllTasksComplete = OnAllTasksComplete.TerminateJob;
                job.PoolInformation = new PoolInformation { PoolId = poolId };
                job.JobPreparationTask = new JobPreparationTask
                {
                    CommandLine = "cmd /c (robocopy %AZ_BATCH_TASK_WORKING_DIR% %AZ_BATCH_NODE_SHARED_DIR%\\" + job.Id + " /e /purge) ^& IF %ERRORLEVEL% LEQ 1 exit 0",
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

                string executableFolder = "exec";
                job.JobPreparationTask.ResourceFiles.Add(new ResourceFile(storage.GetExecutableSasUri(exp.Executable), Path.Combine(executableFolder, exp.Executable)));

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

                    Domain refdomain;
                    if (refExp.Definition.DomainName == "Z3")
                        refdomain = new Z3Domain();
                    else
                        throw new InvalidOperationException("Reference experiment uses unknown domain.");

                    SortedSet<string> extensions;
                    if (string.IsNullOrEmpty(refExp.Definition.BenchmarkFileExtension))
                        extensions = new SortedSet<string>(refdomain.BenchmarkExtensions.Distinct());
                    else
                        extensions = new SortedSet<string>(refExp.Definition.BenchmarkFileExtension.Split('|').Select(s => s.Trim().TrimStart('.')).Distinct());

                    foreach (CloudBlockBlob blob in benchStorage.ListBlobs(refExp.Definition.BenchmarkDirectory, refExp.Definition.Category))
                    {
                        string[] parts = blob.Name.Split('/');
                        string shortName = parts[parts.Length - 1];
                        var shortnameParts = shortName.Split('.');
                        if (shortnameParts.Length == 1 && !extensions.Contains(""))
                            continue;
                        var ext = shortnameParts[shortnameParts.Length - 1];
                        if (!extensions.Contains(ext))
                            continue;

                        job.JobPreparationTask.ResourceFiles.Add(new ResourceFile(benchStorage.GetBlobSASUri(blob), Path.Combine(refBenchFolder, shortName)));
                    }
                }

                job.Constraints = new JobConstraints();

                job.Constraints.MaxTaskRetryCount = MaxTaskRetryCount;
                string taskId = "taskStarter";

                string taskCommandLine = string.Format("cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\" + job.Id + "\\AzureWorker.exe --manage-retry {0} \"{1}\" \"{2}\"",
                    id, tempBlobName, newBenchmarkContainerUri);

                job.JobManagerTask = new JobManagerTask(taskId, taskCommandLine);

                bool failedToCommit = false;
                int tryBackAwayMultiplier = 1;
                int tryNo = 0;
                do
                {
                    try
                    {
                        failedToCommit = false;
                        await job.CommitAsync();
                    }
                    catch (BatchException batchExc) when (batchExc.RequestInformation != null && batchExc.RequestInformation.HttpStatusCode.HasValue && batchExc.RequestInformation.HttpStatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        if (tryNo == 7)//arbitrarily picked constant
                            throw;

                        ++tryNo;
                        failedToCommit = true;
                        await Task.Run(() => System.Threading.Thread.Sleep(tryBackAwayMultiplier * 500));
                        tryBackAwayMultiplier = tryBackAwayMultiplier * 2;
                    }
                }
                while (failedToCommit);
            }
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

    public sealed class PoolDescription
    {
        public string Id { get; set; }

        public AllocationState? AllocationState { get; set; }

        public PoolState? PoolState { get; set; }

        public string VirtualMachineSize { get; set; }

        public int DedicatedNodes { get; set; }

        public int RunningJobs { get; set; }
    }
}
