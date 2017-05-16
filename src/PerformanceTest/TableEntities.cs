﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest
{

    public class ExperimentEntity
    {
        public int ID { get; set; }
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

    public class BenchmarkResultEntity
    {
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
