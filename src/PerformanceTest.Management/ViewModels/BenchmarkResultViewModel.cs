using Measurement;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{

    public class BenchmarkResultViewModel : INotifyPropertyChanged
    {
        private readonly IUIService uiService;

        private BenchmarkResult result;
        private ResultStatus status;

        private double runtime;

        public event PropertyChangedEventHandler PropertyChanged;

        public BenchmarkResultViewModel(BenchmarkResult res, IUIService service)
        {
            if (res == null) throw new ArgumentNullException("benchmark");
            this.result = res;
            this.uiService = service;

            this.status = res.Status;
            this.runtime = res.NormalizedRuntime;
        }
        public int ID
        {
            get { return result.ExperimentID; }
        }
        public string Filename
        {
            get { return result.BenchmarkFileName; }
        }
        public int? ExitCode
        {
            get { return result.ExitCode; }
        }
        public double WallClockTime
        {
            get { return result.WallClockTime.TotalSeconds; }
        }
        public double TotalProcessorTime
        {
            get { return result.TotalProcessorTime.TotalSeconds; }
        }
        public ResultStatus Status
        {
            get { return status; }
        }
        public int Sat
        {
            get { return GetProperty(Z3Domain.KeySat); }
        }
        public int Unsat
        {
            get { return GetProperty(Z3Domain.KeyUnsat); }
        }
        public int Unknown
        {
            get { return GetProperty(Z3Domain.KeyUnknown); }
        }
        public int TargetSat
        {
            get { return GetProperty(Z3Domain.KeyTargetSat); }
        }
        public int TargetUnsat
        {
            get { return GetProperty(Z3Domain.KeyTargetUnsat); }
        }
        public int TargetUnknown
        {
            get { return GetProperty(Z3Domain.KeyTargetUnknown); }
        }
        public double NormalizedRuntime
        {
            get { return runtime; }
            set
            {
                runtime = value;
                NotifyPropertyChanged();
            }
        }

        public double MemorySizeMB
        {
            get { return result.PeakMemorySizeMB; }
        }

        public Task<string> GetStdOutAsync(bool useDefaultIfMissing)
        {
            return ReadOutputAsync(result.StdOut, useDefaultIfMissing);
        }

        public Task<string> GetStdErrAsync(bool useDefaultIfMissing)
        {
            return ReadOutputAsync(result.StdErr, useDefaultIfMissing);
        }


        public async Task<ShowOutputViewModel> GetOutputViewModel()
        {
            var handle = uiService.StartIndicateLongOperation("Loading benchmark output...");
            try
            {
                return await Task.Run(async () =>
                {
                    string stdOut = await GetStdOutAsync(true);
                    string stdErr = await GetStdErrAsync(true);
                    ShowOutputViewModel vm = new ShowOutputViewModel(ID, Filename, stdOut, stdErr);
                    return vm;
                });
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
        }

        private int GetProperty(string prop)
        {
            int res = result.Properties.ContainsKey(prop) ? Int32.Parse(result.Properties[prop]) : 0;
            return res;
        }

        private async Task<string> ReadOutputAsync(Stream stream, bool useDefaultIfMissing)
        {
            string text = null;
            if (stream != null)
            {
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                text = await reader.ReadToEndAsync();
                text = System.Text.RegularExpressions.Regex.Unescape(text);
            }

            if (useDefaultIfMissing && String.IsNullOrEmpty(text))
                return "*** NO OUTPUT SAVED ***";
            return text;
        }

        public BenchmarkResult GetBenchmarkResult ()
        {
            return result;
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
