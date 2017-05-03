using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ExperimentID = System.Int32;

namespace PerformanceTest
{

    public abstract class ExperimentManager
    {
        protected readonly ReferenceExperiment reference;

        protected ExperimentManager(ReferenceExperiment reference)
        {
            if (reference == null) throw new ArgumentNullException("reference");
            this.reference = reference;
        }

        /// <summary>
        /// Schedules execution of a new experiment from the given experiment definition.
        /// </summary>
        /// <param name="definition">Describes the experiment to be performed.</param>
        /// <returns>Identifier of the new experiment for further reference.</returns>
        public abstract Task<ExperimentID> StartExperiment(ExperimentDefinition definition, string creator = null, string note = null);

        /// <summary>
        /// Returns a definition of an existing experiment.
        /// </summary>
        public abstract Task<ExperimentDefinition> GetDefinition(ExperimentID id);

        /// <summary>
        /// Returns current execution status of existing experiments.
        /// </summary>
        public abstract Task<IEnumerable<ExperimentStatus>> GetStatus(IEnumerable<ExperimentID> ids);

        /// <summary>
        /// Allows to get a result of each of the experiment's benchmarks.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public abstract Task<BenchmarkResult>[] GetResults(ExperimentID id);

        public abstract void DeleteExperiment(ExperimentID id);

        public abstract Task UpdatePriority(ExperimentID id, string priority);
        public abstract Task UpdateStatusFlag(ExperimentID id, bool flag);
        public abstract Task UpdateNote(ExperimentID id, string note);
        public abstract Task<IEnumerable<ExperimentID>> FindExperiments(ExperimentFilter? filter = null);

        public struct ExperimentFilter
        {
            public string BenchmarkContainerEquals { get; set; }

            public string CategoryEquals { get; set; }

            public string ExecutableEquals { get; set; }

            public string ParametersEquals { get; set; }

            public string NotesEquals { get; set;}

            public string CreatorEquals { get; set;}

        }
    }

    public class ReferenceExperiment
    {
        public ReferenceExperiment()
        {
        }

        public ReferenceExperiment(ExperimentDefinition def, int repetitions, double referenceValue)
        {
            if (def == null) throw new ArgumentNullException("def");
            if (repetitions < 1) throw new ArgumentOutOfRangeException("repetitions", "Number of repetitions must be greater than zero");
            Definition = def;
            Repetitions = repetitions;
            ReferenceValue = referenceValue;
        }

        public ExperimentDefinition Definition { get; private set; }
        public int Repetitions { get; private set; }

        public double ReferenceValue { get; private set; }

    }
}
