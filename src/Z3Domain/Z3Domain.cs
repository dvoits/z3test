﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Measurement.Measure;

namespace Measurement
{
    public class Z3Domain : Domain
    {
        public Z3Domain()
        {
        }

        public override ProcessRunAnalysis Analyze(string inputFile, ProcessRunMeasure measure)
        {
            if (!measure.StdOut.CanSeek) throw new NotSupportedException("Standard output stream doesn't support seeking");
            if (!measure.StdErr.CanSeek) throw new NotSupportedException("Standard error stream doesn't support seeking");
            measure.StdOut.Position = 0L;
            measure.StdErr.Position = 0L;

            Counts countsTargets;
            try
            {
                using (Stream input = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
                {
                    countsTargets = CountInput(input);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to read input file: " + ex);
                countsTargets = new Counts();
            }
            Counts countsResults = CountResults(measure.StdOut);


            int exitCode = measure.ExitCode;
            LimitsStatus limits = measure.Limits;
            ResultStatus status;

            if (limits == LimitsStatus.TimeOut)
                status = ResultStatus.Timeout;
            else if (limits == LimitsStatus.MemoryOut || exitCode == 101)
                status = ResultStatus.OutOfMemory;
            else if (exitCode == 0 || exitCode == 10 || exitCode == 20)
            {
                if (countsResults.sat == 0 && countsResults.unsat == 0 && countsResults.other == 0)
                    status = ResultStatus.Error;
                else
                    status = ResultStatus.Success;
            }
            else
            {
                status = GetBugCode(measure);
            }

            return new ProcessRunAnalysis(status,
                new Dictionary<string, string>
                {
                    { "SAT", countsResults.sat.ToString() },
                    { "UNSAT", countsResults.unsat.ToString() },
                    { "UNKNOWN", countsResults.other.ToString() },

                    { "TargetSAT", countsTargets.sat.ToString() },
                    { "TargetUNSAT", countsTargets.unsat.ToString() },
                    { "TargetUNKNOWN", countsTargets.other.ToString() }
                });
        }

        private ResultStatus GetBugCode(ProcessRunMeasure measure)
        {
            ResultStatus status = ResultStatus.Error; // no bug found means general error.

            StreamReader reader = new StreamReader(measure.StdErr);
            while (!reader.EndOfStream)
            {
                string l = reader.ReadLine();
                if (l.StartsWith("(error") && l.Contains("check annotation"))
                {
                    status = ResultStatus.Bug;
                    break;
                }
            }
            measure.StdErr.Position = 0L;

            if (status == ResultStatus.Error)
            {
                reader = new StreamReader(measure.StdOut);
                while (!reader.EndOfStream)
                {
                    string l = reader.ReadLine();
                    if (l.StartsWith("(error") && l.Contains("check annotation"))
                    {
                        status = ResultStatus.Bug;
                        break;
                    }
                    else if (l.StartsWith("(error \"out of memory\")"))
                    {
                        status = ResultStatus.OutOfMemory;
                        break;
                    }
                }
                measure.StdOut.Position = 0L;
            }

            return status;
        }

        private static Counts CountResults(Stream output)
        {
            Counts res = new Counts();
            StreamReader reader = new StreamReader(output);
            while (!reader.EndOfStream)
            {
                string l = reader.ReadLine(); // does not contain \r\n
                l.TrimEnd(' ');
                if (l == "sat" || l == "SAT" || l == "SATISFIABLE" || l == "s SATISFIABLE" || l == "SuccessfulRuns = 1") // || l == "VERIFICATION FAILED")
                    res.sat++;
                else if (l == "unsat" || l == "UNSAT" || l == "UNSATISFIABLE" || l == "s UNSATISFIABLE") // || l == "VERIFICATION SUCCESSFUL")
                    res.unsat++;
                else if (l == "unknown" || l == "UNKNOWN" || l == "INDETERMINATE")
                    res.other++;
            }
            output.Position = 0L;
            return res;
        }

        private static Counts CountInput(Stream input)
        {
            Counts res = new Counts();
            StreamReader r = new StreamReader(input);
            while (!r.EndOfStream)
            {
                string l = r.ReadLine(); // does not contain \r\n
                if (l.StartsWith("(set-info :status sat)"))
                    res.sat++;
                else if (l.StartsWith("(set-info :status unsat)"))
                    res.unsat++;
                else if (l.StartsWith("(set-info :status"))
                    res.other++;
            }
            return res;
        }

        struct Counts
        {
            public int sat;
            public int unsat;
            public int other;
        }
    }
}