using Measurement;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
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
            return cache.GetResults(experimentID);
        }
    }
}
