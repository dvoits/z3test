using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Z3Data;

namespace ImportTimeline
{
    class Program
    {
        const int azureStorageBatchSize = 100;

        static void Main(string[] args)
        {
            if(args.Length != 2)
            {
                Console.WriteLine("ImportTimeline.exe <path-to-data> <storage connection string>");
                return;
            }

            string pathToData = args[0];
            string connectionString = args[1];

            Console.WriteLine("Uploading experiments table from {0}...", pathToData);
            UploadExperiments(pathToData);
            
        }

        static void UploadExperiments(string pathToData)
        {
            var files = Directory.EnumerateFiles(pathToData, "*_meta.csv");
            files.AsParallel().ForAll(file =>
            {
                var metadata = new MetaData(file);
                Console.WriteLine(metadata.SubmissionTime);

            });
            //Group(files, azureStorageBatchSize).AsParallel();
        }

        static IEnumerable<T[]> Group<T>(IEnumerable<T> seq, int n)
        {
            int i = 0;
            T[] group = null;
            foreach (T item in seq)
            {
                if(i == 0) group = new T[n];
                group[i++] = item;
                if(i == n)
                {
                    yield return group;
                    i = 0;
                }
            }
            if(i > 0)
            {
                Array.Resize(ref group, i);
                yield return group;
            }
        }
    }
}
