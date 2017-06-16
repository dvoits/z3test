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
        const int regexGroup_Commit = 2;

        const string batchPool = "small_pool";


        static int Main(string[] args)
        {
            try
            {
                Run(ReadConnectionString()).Wait();
                return 0;
            }
            catch(Exception ex)
            {
                Trace.WriteLine("ERROR: " + ex);
                return 1;
            }
        }

        static async Task Run(string connectionString)
        {
            RepositoryContent binary = await GetRecentNightlyBuild();
            if (binary == null)
            {
                Trace.WriteLine("Repository has no new build.");
                return;
            }
            Trace.WriteLine("Last nightly build contains " + binary.Name);

            AzureExperimentManager manager = AzureExperimentManager.Open(connectionString);
            string lastNightlyExecutable = await GetLastNightlyExperiment(manager);
            if(lastNightlyExecutable == binary.Name)
            {
                Trace.WriteLine("No changes found since last nightly experiment.");
                return;
            }

            using (MemoryStream stream = new MemoryStream(binary.Size))
            {
                await Download(binary, stream);
                stream.Position = 0;

                Trace.WriteLine("Opening an experiment manager...");
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

        static async Task<string> GetLastNightlyExperiment(AzureExperimentManager manager)
        {
            Trace.WriteLine("Looking for most recent nightly experiment...");

            // Returns a list ordered by submission time
            var experiments = await manager.FindExperiments(new ExperimentManager.ExperimentFilter() { CreatorEquals = creator });
            var mostRecent = experiments.FirstOrDefault();
            if (mostRecent == null) return null;
                        
            var metadata = await manager.Storage.GetExecutableMetadata(mostRecent.Definition.Executable);
            string fileName = null;
            if(metadata.TryGetValue(AzureExperimentStorage.KeyFileName, out fileName))
            {
                Trace.WriteLine("Last nightly experiment was run for " + fileName);
            }

            return fileName;
        }

        static async Task<RepositoryContent> GetRecentNightlyBuild()
        {
            Trace.WriteLine("Looking for most recent nightly build...");
            var github = new GitHubClient(new ProductHeaderValue("Z3-Tests-Nightly-Runner"));
            var nightly = await github.Repository.Content.GetAllContents("Z3Prover", "bin", githubNightlyFolder);

            Regex regex = new Regex(executableFileName, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            var files = nightly.Select(f => Tuple.Create(f, regex.Match(f.Name))).Where(fm => fm.Item2.Success).ToArray();
            if (files.Length == 0) return null; // no matching files found
            if (files.Length == 1) return files[0].Item1; // single matching file

            // Multiple matching files, should take the most recent
            DateTimeOffset max = DateTimeOffset.MinValue;
            RepositoryContent recent = null; 

            foreach (var fm in files)
            {
                string sha = fm.Item2.Groups[regexGroup_Commit].Value;
                var commit = await github.Repository.Commit.Get("Z3Prover", "z3", sha);
                var date = commit.Commit.Committer.Date;
                if(date > max)
                {
                    max = date;
                    recent = fm.Item1;
                }
            }
            return recent;
        }

        static string ReadConnectionString()
        {
            return File.ReadAllText("connectionString.txt");
        }
    }
}
