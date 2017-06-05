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

        public bool ShowProgress
        {
            get { return ReadBool("ShowProgress"); }
            set { WriteBool("ShowProgress", value); }
        }

        public string ConnectionString
        {
            get { return ReadString("ConnectionString"); }
            set { WriteString("ConnectionString", value); }
        }



        private void WriteBool(string key, bool value)
        {
            Registry.SetValue(keyName, key, value ? 1 : 0, RegistryValueKind.DWord);
        }

        private bool ReadBool(string key)
        {
            var val = Registry.GetValue(keyName, key, 0);
            return val is int && (int)val == 1;
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
