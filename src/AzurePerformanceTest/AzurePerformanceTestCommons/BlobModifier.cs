﻿using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace AzurePerformanceTest
{
    public class BlobModifier : IDisposable
    {
        private static IRetryPolicy retryPolicy = new ExponentialRetry(TimeSpan.FromMilliseconds(100), 10);

        public static async Task<bool> Modify(CloudBlockBlob blob, Func<Stream, Stream> modifier, int maxAttempts)
        {
            int attempt = 0;
            bool success = false;

            do
            {
                using (BlobModifier blobModifier = await BlobModifier.Get(blob))
                using (Stream newContent = modifier(blobModifier.Content))
                {
                    success = await blobModifier.TryModify(newContent);
                    attempt++;
                }
            } while (!success && attempt < maxAttempts);

            return success;
        }

        public static async Task<BlobModifier> Get(CloudBlockBlob blob)
        {
            Stream content = new MemoryStream();
            await blob.DownloadToStreamAsync(content,
                AccessCondition.GenerateEmptyCondition(),
                new BlobRequestOptions { RetryPolicy = retryPolicy }, null);
            string originalETag = blob.Properties.ETag;
            return new BlobModifier(blob, content, originalETag);
        }


        private readonly CloudBlockBlob blob;
        private readonly Stream content;
        private readonly string originalETag;

        private BlobModifier(CloudBlockBlob blob, Stream content, string etag)
        {
            this.blob = blob;
            this.content = content;
            this.originalETag = etag;
        }

        public Stream Content
        {
            get { return content; }
        }

        public async Task<bool> TryModify(Stream newContent)
        {
            if (newContent == null) throw new ArgumentNullException(nameof(newContent));
            try
            {
                await blob.UploadFromStreamAsync(newContent,
                    AccessCondition.GenerateIfMatchCondition(originalETag),
                    new BlobRequestOptions { RetryPolicy = retryPolicy },
                    null);
                return true;
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
                {
                    Trace.WriteLine("Precondition failure. Blob's orignal etag no longer matches");
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }

        public void Dispose()
        {
            content.Dispose();
        }
    }
}
