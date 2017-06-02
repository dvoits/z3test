using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public sealed class RecentValuesStorage
    {
        const string userRoot = "HKEY_CURRENT_USER";
        const string subkey = "PerformanceTest.Management";
        const string keyName = userRoot + "\\" + subkey;

        public RecentValuesStorage()
        {
        }

        public string ConnectionString
        {
            get { return ReadString("ConnectionString"); }
            set { WriteString("ConnectionString", value); }
        }

        private string ReadString(string key)
        {
            return Registry.GetValue(keyName, key, "") as string;
        }

        private void WriteString(string key, string value)
        {
            Registry.SetValue(keyName, key, value, RegistryValueKind.String);
        }

    }
}
