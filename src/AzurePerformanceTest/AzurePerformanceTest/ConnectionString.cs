using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzurePerformanceTest
{
    public sealed class ConnectionString
    {
        private readonly string connectionString;
        private Dictionary<string, string> dict;

        public ConnectionString(string connectionString)
        {
            if (connectionString == null) throw new ArgumentNullException("connectionString");
            this.connectionString = connectionString;

            string[] items = connectionString.Split(';');
            dict = new Dictionary<string, string>(items.Length);
            foreach (var item in items)
            {
                if (!item.Contains("=")) continue;
                var keyValue = item.Split(new[] { '=' }, 2);
                dict.Add(keyValue[0].Trim(), keyValue[1].Trim());
            }
        }

        public string this[string key]
        {
            get { return dict[key]; }
            set { dict[key] = value; }
        }

        /// <summary>
        /// Returns null, if key is missing.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string TryGet(string key)
        {
            string value;
            if (dict.TryGetValue(key, out value)) return value;
            return null;
        }
       
        public void RemoveKeys(params string[] keys)
        {
            foreach(string key in keys)
            {
                dict.Remove(key);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in dict)
            {
                sb.AppendFormat("{0}={1};", item.Key, item.Value);
            }
            return sb.ToString();
        }
    }
}
