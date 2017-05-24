using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Measurement
{
    public abstract class OutputDomain
    {
        private OutputPropertySettings[] outputProps;

        protected OutputDomain(IEnumerable<OutputPropertySettings> properties)
        {
            if (properties == null) throw new ArgumentNullException("properties");
            outputProps = properties.ToArray();
        }

        public abstract ResultStatus GetStatus(ProcessRunMeasure measure);

        public Dictionary<string, string> BuildProperties(ProcessRunMeasure measure)
        {
            if (measure == null) throw new ArgumentNullException("measure");

            Dictionary<string, string> values = new Dictionary<string, string>(outputProps.Length);
            foreach (OutputPropertySettings prop in outputProps)
            {
                string val;
                if (prop.TryApply(measure, out val))
                {
                    values[prop.Name] = val;
                }
            }
            return values;
        }
    }

    public enum ResultStatus
    {
        Success,
        OutOfMemory,
        Timeout,
        Error,
        Bug
    }

    public abstract class OutputPropertySettings
    {
        public string Name;

        public abstract bool TryApply(ProcessRunMeasure measure, out string value);
    }

    public sealed class ExitCodePropertySettings : OutputPropertySettings
    {
        public int ExitCode;
        public string Value;
    }

    public sealed class CountPropertySettings : OutputPropertySettings
    {
        public string RegularExpression;
    }
}
