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

        public static RoutedCommand SaveMetaCSVCommand = new RoutedCommand();
        public static RoutedCommand FlagCommand = new RoutedCommand();
        public static RoutedCommand TallyCommand = new RoutedCommand();
        public MainWindow()
        {
            InitializeComponent();

            connectionString.Text = Properties.Settings.Default.ConnectionString;
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (experimentsVm == null)
                {
                    ExperimentManager manager = LocalExperimentManager.OpenExperiments(connectionString.Text);

                    experimentsVm = new ExperimentListViewModel(manager);
                    dataGrid.DataContext = experimentsVm;
                    Properties.Settings.Default.ConnectionString = connectionString.Text;
                    Properties.Settings.Default.Save();
                    connectionString.IsEnabled = false;
                    btnConnect.Content = "Disconnect";
                    btnConnect.IsEnabled = true;
                    btnNewJob.IsEnabled = true;
                    btnUpdate.IsEnabled = true;
                } else
                {
                    experimentsVm = null;
                    connectionString.IsEnabled = true;
                    btnConnect.Content = "Connect";
                    btnConnect.IsEnabled = true;
                    dataGrid.DataContext = null;
                    btnNewJob.IsEnabled = false;
                    btnUpdate.IsEnabled = false;
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Failed to open experiments", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            ExperimentManager manager = LocalExperimentManager.OpenExperiments(connectionString.Text);

            experimentsVm = new ExperimentListViewModel(manager);
            dataGrid.DataContext = experimentsVm;
            //experimentsVm.FindExperiments(txtFilter.Text);
        }
        private void OptShowProgress_Checked(object sender, RoutedEventArgs e)
        {
            int c = dataGrid.Columns.Count();
            dataGrid.Columns[c - 1].Visibility = Visibility.Visible;
            dataGrid.Columns[c - 2].Visibility = Visibility.Visible;
            dataGrid.Columns[c - 3].Visibility = Visibility.Visible;
            dataGrid.Columns[c - 4].Visibility = Visibility.Visible;
            dataGrid.Columns[c - 5].Visibility = Visibility.Visible;
        }

        private void OptShowProgress_Unchecked(object sender, RoutedEventArgs e)
        {
            int c = dataGrid.Columns.Count();
            dataGrid.Columns[c - 1].Visibility = Visibility.Hidden;
            dataGrid.Columns[c - 2].Visibility = Visibility.Hidden;
            dataGrid.Columns[c - 3].Visibility = Visibility.Hidden;
            dataGrid.Columns[c - 4].Visibility = Visibility.Hidden;
            dataGrid.Columns[c - 5].Visibility = Visibility.Hidden;
        }

        private void canDeleteExperiment(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count >= 1;
        }
        private void deleteExperiment(object target, ExecutedRoutedEventArgs e)
        {
            MessageBoxResult r = MessageBox.Show("Are you sure you want to delete the selected experiments?", "Sure?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (r == MessageBoxResult.Yes)
            {
                var ids = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().Select(st => st.ID).ToArray();
                var count = ids.Length;
                for (int i = 0; i < count; i++) {
                    int id = ids[i];
                    try { 
                        experimentsVm.DeleteExperiment(id);
                    }
                    catch (Exception ex)
                    {
                        string msg = String.Format("Error: could not delete experiment #{0} because of: {1} ", id, ex.Message);
                        r = MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    }
                }
            }
        }

        private void canSaveCSV(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count >= 1;
        }
        private void saveCSV(object target, ExecutedRoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog dlg = new System.Windows.Forms.SaveFileDialog();
            dlg.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
            dlg.FilterIndex = 1;
            dlg.RestoreDirectory = true;
            dlg.FileName = "summary.csv";

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                //not implemented 
            }
        }
        private void canSaveMetaCSV(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count >= 1;
        }
        private void saveMetaCSV(object target, ExecutedRoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog dlg = new System.Windows.Forms.SaveFileDialog();
            dlg.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
            dlg.FilterIndex = 1;
            dlg.RestoreDirectory = true;
            dlg.FileName = "meta.csv";

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                //not implemented 
            }

        }
        private void canShowFlag(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count >= 1;
        }
        private void showFlag(object target, ExecutedRoutedEventArgs e)
        {
            var ids = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().Select(st => st.ID).ToArray();
            var count = ids.Length;
            for (var i = 0; i < count; i++)
            {
                experimentsVm.UpdateFlag(ids[i]);
            }
        }
        private void canShowTally(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count > 0;
        }
        private void showTally(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            double total = 0.0;
            var ids = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().Select(st => st.ID).ToArray();
            var count = ids.Length;
            for (var i = 0; i < count; i++)
            {
                int id = ids[i];
                total += experimentsVm.GetRuntime(id); 
            }
            TimeSpan ts = TimeSpan.FromHours(total);
            MessageBox.Show(this,
                           "The total amount of runtime spent computing the selected results is " + ts.ToString() + ".", "Tally",
                           MessageBoxButton.OK,
                           MessageBoxImage.Information);

            Mouse.OverrideCursor = null;
        }
        private void filter_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                experimentsVm.FindExperiments(txtFilter.Text);
        }
        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
