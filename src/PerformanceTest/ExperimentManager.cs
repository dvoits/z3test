﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ExperimentID = System.Int32;

namespace PerformanceTest
{

    public abstract class ExperimentManager
    {
        /// <summary>
        /// Schedules execution of a new experiment from the given experiment definition.
        /// </summary>
        /// <param name="definition">Describes the experiment to be performed.</param>
        /// <returns>Identifier of the new experiment for further reference.</returns>
        public abstract Task<ExperimentID> StartExperiment(ExperimentDefinition definition, string creator = null, string note = null);

        /// <summary>
        /// Returns current execution status of existing experiments.
        /// </summary>
        public abstract Task<IEnumerable<ExperimentStatus>> GetStatus(IEnumerable<ExperimentID> ids);

        /// <summary>
        /// Allows to get a result of each of the experiment's benchmarks.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>List of results of currently completed benchmarks</returns>
        public abstract Task<BenchmarkResult[]> GetResults(ExperimentID id);

        public abstract Task DeleteExperiment(ExperimentID id);
        public abstract Task UpdateStatusFlag(ExperimentID id, bool flag);
        public abstract Task UpdateNote(ExperimentID id, string note);

        public abstract Task<IEnumerable<Experiment>> FindExperiments(ExperimentFilter? filter = null);

        /// <summary>
        /// Tries to find an experiment with given id and returns information about that experiment.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Returns null, if there is no experiment with given id.</returns>
        public abstract Task<Experiment> TryFindExperiment(ExperimentID id);


        public struct ExperimentFilter
        {
            public string BenchmarkContainerEquals { get; set; }

            public string CategoryEquals { get; set; }

            public string ExecutableEquals { get; set; }

            public string ParametersEquals { get; set; }

            public string NotesEquals { get; set; }

            public string CreatorEquals { get; set; }
        }
    }

    public class Experiment
    {
        public ExperimentID ID { get { return Status.ID; } }
        public ExperimentDefinition Definition;
        public ExperimentStatus Status;
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

    public class ExperimentStatistics
    {
        private readonly Measurement.AggregatedAnalysis analysis;

        public ExperimentStatistics(Measurement.AggregatedAnalysis analysis)
        {
            if (analysis == null)
                throw new ArgumentNullException("analysis");
            this.analysis = analysis;
        }

        public Measurement.AggregatedAnalysis AggregatedResults { get { return analysis; } }
    }
}
