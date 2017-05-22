﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PerformanceTest;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch;

using ExperimentID = System.Int32;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AzurePerformanceTest
{
    public class AzureExperimentManager : ExperimentManager
    {
        AzureExperimentStorage storage;
        BatchSharedKeyCredentials batchCreds;

        protected AzureExperimentManager(AzureExperimentStorage storage, string batchUrl, string batchAccName, string batchKey)
        {
            this.storage = storage;
            this.batchCreds = new BatchSharedKeyCredentials(batchUrl, batchAccName, batchKey);
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

        public override async Task DeleteExperiment(ExperimentID id)
        {
            throw new NotImplementedException();
        }

        public override async Task<IEnumerable<ExperimentID>> FindExperiments(ExperimentFilter? filter = default(ExperimentFilter?))
        {
            IEnumerable<KeyValuePair<int, ExperimentEntity>> experiments = await storage.GetExperiments(filter);

            return experiments.OrderByDescending(q => q.Value.Submitted).Select(e => e.Key);
        }

        public override async Task<ExperimentDefinition> GetDefinition(ExperimentID id)
        {
            var experimentEntity = await storage.GetExperiment(id);
            return ExperimentDefinition.Create(
                experimentEntity.Executable,
                experimentEntity.BenchmarkContainer,
                experimentEntity.BenchmarkFileExtension,
                experimentEntity.Parameters,
                TimeSpan.FromSeconds(experimentEntity.BenchmarkTimeout),
                experimentEntity.Category,
                experimentEntity.MemoryLimit << 20
                );
        }

        public override async Task<BenchmarkResult[]> GetResults(ExperimentID id)
        {
            return await storage.GetResults(id);
        }

        public override Task<IEnumerable<ExperimentStatus>> GetStatus(IEnumerable<ExperimentID> ids)
        {
            throw new NotImplementedException();
        }

        public override async Task<ExperimentID> StartExperiment(ExperimentDefinition definition, string creator = null, string note = null)
        {
            var id = await storage.AddExperiment(definition, DateTime.Now, creator, note);
            //TODO: schedule execution

            using (var bc = BatchClient.Open(batchCreds))
            {
                CloudJob job = bc.JobOperations.CreateJob();
                job.Id = "exp" + id.ToString();
                job.PoolInformation = new PoolInformation { PoolId = "testPool" };
                job.JobPreparationTask = new JobPreparationTask
                {
                    CommandLine = "cmd /c (robocopy %AZ_BATCH_TASK_WORKING_DIR% %AZ_BATCH_NODE_SHARED_DIR%) ^& IF %ERRORLEVEL% LEQ 1 exit 0",
                    ResourceFiles = new List<ResourceFile>(),
                    WaitForSuccess = true
                };

                SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
                {
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(48),
                    Permissions = SharedAccessBlobPermissions.Read
                };

                foreach (CloudBlockBlob blob in storage.configContainer.ListBlobs())
                {
                    string sasBlobToken = blob.GetSharedAccessSignature(sasConstraints);
                    string blobSasUri = String.Format("{0}{1}", blob.Uri, sasBlobToken);
                    job.JobPreparationTask.ResourceFiles.Add(new ResourceFile(blobSasUri, blob.Name));
                }

                await job.CommitAsync();

                string taskId = "taskStarter";

                string taskCommandLine = string.Format("cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\AzureWorker.exe --add-tasks {0} \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\"", id, definition.Category, definition.Executable, definition.Parameters, definition.BenchmarkTimeout.Seconds.ToString(), definition.MemoryLimit.ToString());
                CloudTask task = new CloudTask(taskId, taskCommandLine);

                // Add the tasks as a collection opposed to a separate AddTask call for each. Bulk task submission
                // helps to ensure efficient underlying API calls to the Batch service.
                await bc.JobOperations.AddTaskAsync(job.Id, task);
            }

            return id;
        }

        public override async Task UpdateNote(int id, string note)
        {
            await storage.UpdateNote(id, note);
        }

        public override Task UpdatePriority(int id, string priority)
        {
            throw new NotImplementedException();
        }

        public override async Task UpdateStatusFlag(ExperimentID id, bool flag)
        {
            await storage.UpdateStatusFlag(id, flag);
        }
    }
}
