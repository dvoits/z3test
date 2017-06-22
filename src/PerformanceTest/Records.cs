﻿using Measurement;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Records
{
    public class Records
    {
        private readonly IReadOnlyDictionary<string, Record> records;
        private readonly IReadOnlyDictionary<string, CategoryRecord> categoryRecords;

        public Records(IReadOnlyDictionary<string, Record> records, IReadOnlyDictionary<string, CategoryRecord> categoryRecords)
        {
            this.records = records;
            this.categoryRecords = categoryRecords;
        }

        /// <summary>Record for each benchmark (maps from benchmark filename).</summary>
        public IReadOnlyDictionary<string, Record> BenchmarkRecords { get { return records; } }

        /// <summary>Returns the benchmark records aggregated by categories (maps from category name).</summary>
        public IReadOnlyDictionary<string, CategoryRecord> CategoryRecords { get { return categoryRecords; } }

        public static Records Build(IEnumerable<BenchmarkResult> results, Domain domain)
        {
            Dictionary<string, Record> records = new Dictionary<string, Record>();
            Dictionary<string, CategoryRecord> categoryRecords = new Dictionary<string, CategoryRecord>();

            foreach (var r in results)
            {
                if (domain.CanConsiderAsRecord(new ProcessRunAnalysis(r.Status, r.Properties)))
                {
                    Record record;
                    if (!records.TryGetValue(r.BenchmarkFileName, out record) || record.Runtime > r.NormalizedRuntime)
                    {
                        // New record found
                        records[r.BenchmarkFileName] = new Record(r.ExperimentID, r.NormalizedRuntime);


                        // Category
                        int i = r.BenchmarkFileName.IndexOf("/");
                        string category = i >= 0 ? r.BenchmarkFileName.Substring(0, i) : string.Empty;

                        CategoryRecord catRecord;
                        if (!categoryRecords.TryGetValue(category, out catRecord))
                            catRecord = new CategoryRecord(0, 0);

                        categoryRecords[category] = catRecord.Add(r.NormalizedRuntime, 1);
                    }
                }
            }

            return new Records(new ReadOnlyDictionary<string, Record>(records), new ReadOnlyDictionary<string, CategoryRecord>(categoryRecords));
        }
    }

    public struct Record
    {
        public Record(int expId, double runtime)
        {
            this.ExperimentId = expId;
            this.Runtime = runtime;
        }

        public int ExperimentId { get; private set; }

        public double Runtime { get; private set; }
    }

    public struct CategoryRecord
    {
        public CategoryRecord(double runtime, int files)
        {
            this.Runtime = runtime;
            this.Files = files;
        }

        public int Files { get; private set; }

        public double Runtime { get; private set; }

        public CategoryRecord Add(double runtime, int files)
        {
            return new CategoryRecord(Runtime + runtime, Files + files);
        }
    }

}
