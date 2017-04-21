using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PerformanceTest.Management
{
    public partial class MainWindow : Window
    {
        private ExperimentListViewModel experimentsVm;

        public MainWindow()
        {
            InitializeComponent();

            connectionString.Text = Properties.Settings.Default.ConnectionString;
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExperimentManager manager = LocalExperimentManager.OpenExperiments(connectionString.Text);
                experimentsVm = new ExperimentListViewModel(manager);
                dataGrid.DataContext = experimentsVm;

                Properties.Settings.Default.ConnectionString = connectionString.Text;
                Properties.Settings.Default.Save();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Failed to open experiments", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
    }
}
