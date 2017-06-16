using AzurePerformanceTest;
using Octokit;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NightlyRunner
{
    class Program
    {
        const string creator = "Nightly";
        const string benchmarkDirectory = "";
        const string benchmarkCategory = "smtlib-latest";
        const string benchmarkFileExtension = "smt2|smt";
        const string parameters = "model_validate=true -smt2 -file:{0}";
        const string domainName = "Z3";
        static readonly TimeSpan benchmarkTimeout = TimeSpan.FromSeconds(1200);
        const double memoryLimitMB = 2048;

        const string githubNightlyFolder = "nightly";
        const string executableFileName = @"^z3-(\d+\.\d+\.\d+).([\d\w]+)-x86-win.zip$";

        const string batchPool = "small_pool";


        static void Main(string[] args)
        {
            // z3-4.5.1.d3320f8b8143-x64-win.zip
            // z3-4.5.1.d3320f8b8143-x64-win.zip.log
            // z3-4.5.1.d3320f8b8143-x86-win.zip
            // z3-4.5.1.d3320f8b8143-x86-win.zip.log
            // z3-4.5.1.d8a02bc0400e-x86-ubuntu-14.04.zip

            Run(ReadConnectionString()).Wait();
        }

        static async Task Run(string connectionString)
        {
            Trace.WriteLine("Looking for most recent nightly build...");
            RepositoryContent binary = await GetRecentNightlyBuild();
            if (binary == null)
            {
                Trace.WriteLine("Repository has no new build.");
                return;
            }

            using (MemoryStream stream = new MemoryStream(binary.Size))
            {
                await Download(binary, stream);
                stream.Position = 0;

                Trace.WriteLine("Opening an experiment manager...");
                AzureExperimentManager manager = AzureExperimentManager.Open(connectionString);
                await SubmitExperiment(manager, stream, binary.Name);
            }
        }

        private static async Task Download(RepositoryContent binary, Stream stream)
        {
            Trace.WriteLine(string.Format("Downloading new nightly build from {0} ({1:F2} MB)...", binary.DownloadUrl, binary.Size / 1024.0 / 1024.0));

            HttpWebRequest request = WebRequest.CreateHttp(binary.DownloadUrl);
            request.AutomaticDecompression = DecompressionMethods.GZip;

            using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
            using (Stream respStream = response.GetResponseStream())
            {
                await respStream.CopyToAsync(stream);
            }
        }

        static async Task SubmitExperiment(AzureExperimentManager manager, Stream source, string fileName)
        {
            Trace.WriteLine("Uploading new executable...");
            string packageName = await manager.Storage.UploadNewExecutable(source, fileName, creator);
            Trace.WriteLine("Successfully uploaded as " + packageName);


            ExperimentDefinition definition =
                ExperimentDefinition.Create(
                    packageName,
                    ExperimentDefinition.DefaultContainerUri,
                    benchmarkDirectory,
                    benchmarkFileExtension,
                    parameters,
                    benchmarkTimeout,
                    domainName,
                    benchmarkCategory,
                    memoryLimitMB);

            Trace.WriteLine(string.Format("Starting nightly experiment in Batch pool \"{0}\"...", batchPool));
            manager.BatchPoolID = batchPool;
            var experimentId = await manager.StartExperiment(definition, creator, "Nightly run");
            Trace.WriteLine(string.Format("Done, experiment id {0}.", experimentId));
        }

        static async Task<RepositoryContent> GetRecentNightlyBuild()
        {
            var github = new GitHubClient(new ProductHeaderValue("Z3-Tests-Nightly-Runner"));
            var nightly = await github.Repository.Content.GetAllContents("Z3Prover", "bin", githubNightlyFolder);

            Regex regex = new Regex(executableFileName, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (var file in nightly)
            {
                if (regex.IsMatch(file.Name))
                {
                    return file;
                }
            }

            return null;
        }

        static string ReadConnectionString()
        {
            return File.ReadAllText("connectionString.txt");
        }
    }
}
