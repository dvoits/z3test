using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AzurePerformanceTest
{
    public class AzureBenchmarkStorage
    {
        public const string DefaultContainerName = "input";

        // Storage account
        //private CloudStorageAccount storageAccount;
        //private CloudBlobClient blobClient;
        private CloudBlobContainer inputsContainer;
        private string uri = null;
        private string signature = null;


        public AzureBenchmarkStorage(string storageAccountName, string storageAccountKey, string inputsContainerName) : this(String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", storageAccountName, storageAccountKey), inputsContainerName)
        {
        }

        public AzureBenchmarkStorage(string storageConnectionString, string inputsContainerName)
        {
            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            inputsContainer = blobClient.GetContainerReference(inputsContainerName);

            inputsContainer.CreateIfNotExists();
        }

        public AzureBenchmarkStorage(string containerUri)
        {
            this.uri = containerUri;
            var parts = containerUri.Split('?');
            if (parts.Length != 2)
                throw new ArgumentException("Incorrect uri");

            this.signature = "?" + parts[1];
            inputsContainer = new CloudBlobContainer(new Uri(containerUri));
        }

        public string GetContainerSASUri()
        {
            if (uri != null)
                return uri;

            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(48),
                Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List
            };
            string signature = inputsContainer.GetSharedAccessSignature(sasConstraints);
            return inputsContainer.Uri + signature;
        }

        public async Task<BlobResultSegment> ListBlobsSegmentedAsync(string prefix = "", BlobContinuationToken currentToken = null)
        {
            return await inputsContainer.ListBlobsSegmentedAsync(prefix, true, BlobListingDetails.All, null, currentToken, null, null);
        }

        public string GetBlobSASUri(CloudBlob blob)
        {
            if (this.signature != null)
                return blob.Uri + this.signature;
            else
            {
                SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
                {
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(48),
                    Permissions = SharedAccessBlobPermissions.Read
                };
                return blob.Uri + blob.GetSharedAccessSignature(sasConstraints);
            }
        }

        public string GetBlobSASUri(string blobName)
        {
            var blob = inputsContainer.GetBlobReference(blobName);
            return GetBlobSASUri(blob);
        }
    }
}
