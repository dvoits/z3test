﻿using Measurement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest
{
    public static class Utils
    {
        public static T Median<T>(T[] data, Func<T,T,T> mean)
        {
            int len = data.Length;
            Array.Sort(data);
            int im = len >> 1;
            T m;
            if (len % 2 == 1)
                m = data[im];
            else
                m = mean (data[im], data[im - 1]);
            return m;
        }

        public static double Median(double[] data)
        {
            return Median(data, (x,y) => 0.5*(x+y));
        }

        public static ProcessRunMeasure AggregateMeasures(ProcessRunMeasure[] measures)
        {
            if (measures.Length == 0) throw new ArgumentException("measures", "At least one element expected");
            if (measures.Length == 1) return measures[0];
            int exitCode = measures[0].ExitCode;
            foreach(ProcessRunMeasure m in measures)
            {
                if (m.Status != Measure.CompletionStatus.Success || m.ExitCode != exitCode) return m;
            }

            TimeSpan totalProcessorTime = Median(measures.Select(m => m.TotalProcessorTime).ToArray(), (t1, t2) => TimeSpan.FromTicks((t1 + t2).Ticks >> 1));
            TimeSpan wallClockTime = Median(measures.Select(m => m.WallClockTime).ToArray(), (t1, t2) => TimeSpan.FromTicks((t1 + t2).Ticks >> 1));
            long peakMemorySize = measures.Select(m => m.PeakMemorySize).Max();

            return new ProcessRunMeasure(
                totalProcessorTime,
                wallClockTime,
                peakMemorySize,
                measures[0].Status,
                exitCode,
                measures[0].StdOut,
                measures[0].StdErr);
        }
    }
}