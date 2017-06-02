using AzurePerformanceTest;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public class ConnectionStringBuilderViewModel : INotifyPropertyChanged
    {
        private ConnectionString cs;

        public event PropertyChangedEventHandler PropertyChanged;

        public ConnectionStringBuilderViewModel(string connectionString)
        {
            cs = new AzurePerformanceTest.ConnectionString(connectionString);   
            if(cs.TryGet("DefaultEndpointsProtocol") == null)
            {
                cs["DefaultEndpointsProtocol"] = "https";
            }
        }


        public string ConnectionString
        {
            get
            {
                return cs.ToString();
            }
        }


        public string StorageAccountName
        {
            get { return cs.TryGet("AccountName"); }
            set {
                cs["AccountName"] = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("ConnectionString");
            }
        }
        public string StorageAccountKey
        {
            get { return cs.TryGet("AccountKey"); }
            set
            {
                cs["AccountKey"] = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("ConnectionString");
            }
        }
        public string BatchAccountName
        {
            get { return cs.TryGet(AzureExperimentManager.KeyBatchAccount); }
            set
            {
                cs[AzureExperimentManager.KeyBatchAccount] = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("ConnectionString");
            }
        }
        public string BatchURL
        {
            get { return cs.TryGet(AzureExperimentManager.KeyBatchURL); }
            set
            {
                cs[AzureExperimentManager.KeyBatchURL] = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("ConnectionString");
            }
        }
        public string BatchKey
        {
            get { return cs.TryGet(AzureExperimentManager.KeyBatchAccessKey); }
            set
            {
                cs[AzureExperimentManager.KeyBatchAccessKey] = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("ConnectionString");
            }
        }


        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
