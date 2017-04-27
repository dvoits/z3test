using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PerformanceTest.Management
{
    public interface IMessageService
    {
        void ShowError(string error, string caption = null);
    }

    public class MessageBoxService : IMessageService
    {
        public static readonly MessageBoxService Instance = new MessageBoxService();

        public void ShowError(string error, string caption = null)
        {
            MessageBox.Show(error, caption ?? "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
