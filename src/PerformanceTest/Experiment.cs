using Measurement;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using ExperimentID = System.Int32;


namespace PerformanceTest
{
    public class ExperimentDefinition
    {
        public static ExperimentDefinition Create(string executable, string benchmarkContainer, string benchmarkFileExtension, string parameters,
            TimeSpan benchmarkTimeout,
            string domainName,
            string category = null, double memoryLimitMB = 0)
        {
            return new ExperimentDefinition()
            {
                Executable = executable,
                BenchmarkContainer = benchmarkContainer,
                BenchmarkFileExtension = benchmarkFileExtension,
                Parameters = parameters,
                BenchmarkTimeout = benchmarkTimeout,
                Category = category,
                MemoryLimitMB = memoryLimitMB,
                DomainName = domainName
            };
        }

        private ExperimentDefinition()
        {
        }

        /// <summary>
        /// A path to a file which is either an executable file or a zip file which contains a main executable and supporting files.
        /// The executable will run for multiple specified benchmark files to measure its performance.
        /// </summary>
        public string Executable { get; private set; }

        /// <summary>
        /// Name of a domain which determines an additional analysis and process results interpretation.
        /// </summary>
        public string DomainName { get; private set; }

        /// <summary>
        /// Command-line parameters for the executable.
        /// Special symbols:
        ///  - "{0}" will be replaced with a path to a benchmark file.
        /// </summary>
        public string Parameters { get; private set; }


        /// <summary>
        /// A shared container with the benchmark files.
        /// </summary>
        public string BenchmarkContainer { get; private set; }


        /// <summary>
        /// A category name to draw benchmarks from. Can be null or empty string.
        /// </summary>
        public string Category { get; private set; }

        /// <summary>
        /// The extension of benchmark files, e.g., "smt2" for SMT-Lib version 2 files.
        /// </summary>
        public string BenchmarkFileExtension { get; private set; }



        /// <summary>
        /// The memory limit per benchmark (megabytes).
        /// Zero means no limit.
        /// </summary>
        public double MemoryLimitMB { get; private set; }

        /// <summary>
        /// The time limit per benchmark.
        /// </summary>
        public TimeSpan BenchmarkTimeout { get; private set; }

        /// <summary>
        /// The time limit per experiment.
        /// </summary>
        public TimeSpan ExperimentTimeout { get; private set; }

        public string GroupName { get; private set; }

    }

    /// <summary>
    /// Represents the experiment that runs multiple performance measurements jobs.
    /// Aka "TitleScreen"; used in ClusterExperiments for the main table.
    /// </summary>
    public class ExperimentStatus
    {
        public ExperimentStatus(ExperimentID id, string category, DateTime submitted, string creator, string note, bool flag, int done, int total)
        {
            ID = id;
            Category = category;
            Creator = creator;
            SubmissionTime = submitted;
            Note = note;
            Flag = flag;
            BenchmarksDone = done;
            BenchmarksTotal = total;
        }

        public ExperimentID ID { get; private set; }

        public DateTime SubmissionTime;
        public string Creator;

        public string Category { get; private set; }

        /// <summary>
        /// A descriptive note, if you like.
        /// </summary>
        public string Note { get; set; }
        public bool Flag;

        public int BenchmarksDone { get; private set; }
        public int BenchmarksTotal { get; private set; }
        public int BenchmarksQueued { get { return BenchmarksTotal - BenchmarksDone; } }
    }


    /// <summary>
    /// Aka "Data".
    /// </summary>
    [Serializable]
    public class BenchmarkResult : ISerializable
    {
        public BenchmarkResult(int experimentId, string benchmarkFileName, string workerInformation, DateTime acquireTime, double normalizedRuntime,
            TimeSpan totalProcessorTime, TimeSpan wallClockTime, double memorySizeMB, ResultStatus status, int exitCode, Stream stdout, Stream stderr, 
            IReadOnlyDictionary<string, string> props)
        {
            if (props == null) throw new ArgumentNullException("props");

            this.ExperimentID = experimentId;
            this.BenchmarkFileName = benchmarkFileName;
            this.WorkerInformation = workerInformation;
            this.NormalizedRuntime = normalizedRuntime;
            this.TotalProcessorTime = totalProcessorTime;
            this.WallClockTime = wallClockTime;
            this.PeakMemorySizeMB = memorySizeMB;
            this.ExitCode = exitCode;
            this.AcquireTime = acquireTime;
            this.Status = status;
            this.Properties = props;
            this.StdOut = stdout;
            this.StdErr = stderr;
        }

        /// <summary>
        /// An experiment this benchmark is part of.
        /// </summary>
        public int ExperimentID { get; private set; }

        /// <summary>
        /// Name of a file that is passed as an argument to the target executable.
        /// </summary>
        /// <example>smtlib-latest\sample\z3.01234.smt2</example>
        public string BenchmarkFileName { get; private set; }

        public DateTime AcquireTime { get; private set; }

        public ResultStatus Status { get; private set; }

        /// <summary>
        /// A normalized total processor time that indicates the amount of time that the associated process has spent utilizing the CPU.
        /// </summary>
        public double NormalizedRuntime { get; private set; }

        public TimeSpan TotalProcessorTime { get; private set; }

        public TimeSpan WallClockTime { get; private set; }

        /// <summary>
        /// Gets the maximum amount of virtual memory, in Mega Bytes, allocated for the process.
        /// </summary>
        public double PeakMemorySizeMB { get; private set; }

        public int ExitCode { get; private set; }
        
        public Stream StdOut { get; private set; }

        public Stream StdErr { get; private set; }

        public string WorkerInformation { get; private set; }

        /// <summary>
        /// Domain-specific properties of the result.
        /// </summary>
        public IReadOnlyDictionary<string, string> Properties { get; private set; }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (!this.StdOut.CanSeek || !this.StdErr.CanSeek)
                throw new InvalidOperationException("Can't serialize BenchmarkResult with non-seekable stream(s).");

            info.AddValue("ExperimentID", this.ExperimentID);
            info.AddValue("BenchmarkFileName", this.BenchmarkFileName, typeof(string));
            info.AddValue("WorkerInformation", this.WorkerInformation, typeof(string));
            info.AddValue("NormalizedRuntime", this.NormalizedRuntime);
            info.AddValue("TotalProcessorTime", this.TotalProcessorTime, typeof(TimeSpan));
            info.AddValue("WallClockTime", this.WallClockTime, typeof(TimeSpan));
            info.AddValue("PeakMemorySizeMB", this.PeakMemorySizeMB);
            info.AddValue("ExitCode", this.ExitCode);
            info.AddValue("AcquireTime", this.AcquireTime);
            info.AddValue("Status", (int)this.Status);
            var props = new Dictionary<string, string>(this.Properties.Count);
            foreach (var prop in this.Properties)
                props.Add(prop.Key, prop.Value);
            info.AddValue("Properties", props, typeof(Dictionary<string, string>));
            info.AddValue("StdOut", StreamToByteArray(this.StdOut), typeof(byte[]));
            info.AddValue("StdErr", StreamToByteArray(this.StdErr), typeof(byte[]));
        }

        private static byte[] StreamToByteArray(Stream stream)
        {
            if (stream is MemoryStream)
            {
                return ((MemoryStream)stream).ToArray();
            }
            else
            {
                if (!stream.CanSeek)
                    throw new ArgumentException("Non-seekable stream");
                var pos = stream.Position;
                stream.Seek(0, SeekOrigin.Begin);
                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    stream.Seek(pos, SeekOrigin.Begin);
                    return ms.ToArray();
                }
            }
        }

        public BenchmarkResult(SerializationInfo info, StreamingContext context)
        {
            this.ExperimentID = info.GetInt32("ExperimentID");
            this.BenchmarkFileName = info.GetString("BenchmarkFileName");
            this.WorkerInformation = info.GetString("WorkerInformation");
            this.NormalizedRuntime = info.GetDouble("NormalizedRuntime");
            this.TotalProcessorTime = (TimeSpan)info.GetValue("TotalProcessorTime", typeof(TimeSpan));
            this.WallClockTime = (TimeSpan)info.GetValue("WallClockTime", typeof(TimeSpan));
            this.PeakMemorySizeMB = info.GetDouble("PeakMemorySizeMB");
            this.ExitCode = info.GetInt32("ExitCode");
            this.AcquireTime = info.GetDateTime("AcquireTime");
            this.Status = (ResultStatus)info.GetInt32("Status");
            this.Properties = new ReadOnlyDictionary<string, string>((Dictionary<string, string>)info.GetValue("Properties", typeof(Dictionary<string, string>)));
            this.StdOut = new MemoryStream((byte[])info.GetValue("StdOut", typeof(byte[])));
            this.StdErr = new MemoryStream((byte[])info.GetValue("StdErr", typeof(byte[])));
        }
        public void updateStatus (string status)
        {
            if (status == "Bug") this.Status = ResultStatus.Bug;
            else if (status == "Error") this.Status = ResultStatus.Error;
            else if (status == "Success") this.Status = ResultStatus.Success;
            else if (status == "OutOfMemory") this.Status = ResultStatus.OutOfMemory;
            else if (status == "Timeout") this.Status = ResultStatus.Timeout;
        }
        public void updateRuntime (double newRuntime)
        {
            this.NormalizedRuntime = newRuntime;
        }
    }

}
