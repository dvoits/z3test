using AzurePerformanceTest;
using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly IDomainResolver domainResolver;

        private ExperimentManagerViewModel managerVm;
        private ExperimentListViewModel experimentsVm;

        public static RoutedCommand SaveMetaCSVCommand = new RoutedCommand();
        public static RoutedCommand FlagCommand = new RoutedCommand();
        public static RoutedCommand TallyCommand = new RoutedCommand();
        public static RoutedCommand CopyCommand = new RoutedCommand();
        public static RoutedCommand MoveCommand = new RoutedCommand();
        public static RoutedCommand RestartCommand = new RoutedCommand();
        public static RoutedCommand CreateGroupCommand = new RoutedCommand();
        public static RoutedCommand CompareCommand = new RoutedCommand();
        public static RoutedCommand ScatterplotCommand = new RoutedCommand();
        public MainWindow()
        {
            InitializeComponent();

            connectionString.Text = Properties.Settings.Default.ConnectionString;

            domainResolver = new DomainResolver(new[] { Measurement.Domain.Default, new Measurement.Z3Domain() });
        }

        private ExperimentManagerViewModel Connect(string connectionString)
        {
            if (Directory.Exists(connectionString))
            {
                LocalExperimentManager manager = LocalExperimentManager.OpenExperiments(connectionString, domainResolver);
                return new LocalExperimentManagerViewModel(manager, UIService.Instance);
            }else
            {
                AzureExperimentManager azureManager = AzureExperimentManager.OpenWithoutStart(new AzureExperimentStorage(connectionString));
                return new AzureExperimentManagerViewModel(azureManager, UIService.Instance);
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (experimentsVm == null)
                {
                    managerVm = Connect(connectionString.Text);
                    experimentsVm = managerVm.BuildListView();

                    dataGrid.DataContext = experimentsVm;
                    Properties.Settings.Default.ConnectionString = connectionString.Text;
                    Properties.Settings.Default.Save();
                    connectionString.IsEnabled = false;
                    btnConnect.Content = "Disconnect";
                    btnConnect.IsEnabled = true;
                    btnNewJob.IsEnabled = true;
                    btnUpdate.IsEnabled = true;
                    menuNewJob.IsEnabled = true;
                    menuNewCatchAll.IsEnabled = true;
                } else
                {
                    experimentsVm = null;
                    connectionString.IsEnabled = true;
                    btnConnect.Content = "Connect";
                    btnConnect.IsEnabled = true;
                    dataGrid.DataContext = null;
                    btnNewJob.IsEnabled = false;
                    btnUpdate.IsEnabled = false;
                    menuNewJob.IsEnabled = false;
                    menuNewCatchAll.IsEnabled = false;
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Failed to open experiments", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            experimentsVm.Refresh();
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
                StreamWriter f = new StreamWriter(dlg.FileName, false);
                //not implemented 


                f.WriteLine();
                f.Close();
            }
        }
        private List<int> computeUnique()
        {
            List<int> ids = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().Select(st => st.ID).ToList();
            List<int> res = new List<int>();
            for (int i = 0; i < ids.Count; i++)
            {
                //what is a filename
                List<string> filenames = new List<string>();
                res.Add(filenames.Count);
            }
            return res;
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
                throw new NotImplementedException();
                //StreamWriter f = new StreamWriter(dlg.FileName, false);
                //f.WriteLine("\"ID\",\"# Total\",\"# SAT\",\"# UNSAT\",\"# UNKNOWN\",\"# Timeout\",\"# Memout\",\"# Bug\",\"# Error\",\"# Unique\",\"Parameters\",\"Note\""); 
                //var ids = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().Select(st => st.ID).ToArray();
                //var count = ids.Length;
                //var unique = computeUnique();
                //for (var i = 0; i < count; i++)
                //{
                //    //not implemented 
                //    var experiment = experimentsVm.Items.Where(st => st.ID == ids[i]).ToArray()[0];
                //    var def = managerVm.GetDefinition(ids[i]).Result;
                //    string ps = def.Parameters;
                //    string note = experiment.Note;
                //    int total =  0;
                //    int sat = 0;
                //    int unsat = 0;
                //    int unknown = 0;
                //    int timeouts = 0;
                //    int memouts = 0;
                //    int bugs = 0;
                //    int errors = 0;

                //    f.WriteLine(ids[i] + "," +
                //                total + "," +
                //                sat + "," +
                //                unsat + "," +
                //                unknown + "," +
                //                timeouts + "," +
                //                memouts + "," +
                //                bugs + "," +
                //                errors + "," +
                //                unique[i] + "," +
                //                "\"" + ps + "\"," +
                //                "\"" + note + "\"");
                //}
                //f.WriteLine();
                //f.Close();
            }

        }

        private void canToggleFlag(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count >= 1;
        }

        private void toggleFlag(object target, ExecutedRoutedEventArgs e)
        {
            var vms = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>();
            foreach (var vm in vms)
            {
                vm.Flag = !vm.Flag;
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
            TimeSpan ts = TimeSpan.FromSeconds(total);
            MessageBox.Show(this,
                           "The total amount of runtime spent computing the selected results is " + ts.ToString() + ".", "Tally",
                           MessageBoxButton.OK,
                           MessageBoxImage.Information);

            Mouse.OverrideCursor = null;
        }

        private void canCopy(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count >= 1;
        }
        private void Copy(object target, ExecutedRoutedEventArgs e)
        {
            CopyDialog dlg = new CopyDialog();
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                string backupDB = dlg.txtDB.Text;
                if (connectionString.Text == backupDB)
                {
                    MessageBox.Show(this, "Refusing to copy to the same database.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                try {
                    throw new NotImplementedException();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Failed to open experiments", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void canMove(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count >= 1;
        }
        private void Move(object target, ExecutedRoutedEventArgs e)
        {
            CopyDialog dlg = new CopyDialog();
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                string backupDB = dlg.txtDB.Text;
                if (connectionString.Text == backupDB)
                {
                    MessageBox.Show(this, "Refusing to move to the same database.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                try
                {
                    throw new NotImplementedException();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Failed to open experiments", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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

        private async void btnNewJob_Click(object sender, RoutedEventArgs e)
        {
            NewJobDialog dlg = new NewJobDialog();
            var vm = new NewExperimentViewModel(managerVm, UIService.Instance);
            dlg.DataContext = vm;
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                ExperimentDefinition def = 
                    ExperimentDefinition.Create(
                        vm.Executable, vm.BenchmarkLibrary, vm.Extension, vm.Parameters, 
                        TimeSpan.FromSeconds(vm.BenchmarkTimeoutSec), vm.Domain, 
                        vm.Categories, vm.BenchmarkMemoryLimitMb);
                try
                {
                    await managerVm.SubmitExperiment(def, System.Security.Principal.WindowsIdentity.GetCurrent().Name, vm.Note);
                }catch(Exception ex)
                {
                    UIService.Instance.ShowError(ex.Message, "Failed to submit an experiment");
                }
            }
        }
        private void canShowProperties(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 1;
        }
        private void showProperties(object target, ExecutedRoutedEventArgs e)
        {
            int id = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().Select(st => st.ID).ToArray()[0];
            ExperimentProperties dlg = new ExperimentProperties();
            var vm = new ExperimentPropertiesViewModel(experimentsVm, id);
            dlg.DataContext = vm;
            dlg.Owner = this;
            dlg.Show();
        }
        private void canRestartCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count > 0;
        }
        private void Restart(object target, ExecutedRoutedEventArgs e)
        {
            try
            {
                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Exception: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void canCreateGroup(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count >= 1;
        }
        private void CreateGroup(object target, ExecutedRoutedEventArgs e)
        {
            bool first = true;
            string category = "";
            var ids = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().ToArray();
            for (var i = 0; i < ids.Length; i++)
            {
                if (first)
                {
                    category = ids[i].Category;
                    first = false;
                }
                else if (ids[i].Category != category)
                {
                    MessageBox.Show(this, "Jobs in a group need to have the same category.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            CreateGroupDialog dlg = new CreateGroupDialog();
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                string name = dlg.txtGroupName.Text;
                string note = dlg.txtNote.Text;
                //for (var i = 0; i < ids.Length; i++)
                //{
                //    ExperimentDefinition def = managerVm.GetDefinition(ids[i].ID).Result;
                //    //change groupName
                //    //change note
                //}
                //not implemented

            }
        }

        private void canCompare(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 2;
        }
        private void Compare(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var ids = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().Select(st => st.ID).ToArray();
            CompareExperiments dlg = new CompareExperiments();
            var vm = new CompareExperimentsViewModel(ids[0], ids[1], managerVm, UIService.Instance);
            dlg.DataContext = vm;
            dlg.Owner = this;
            dlg.Show();
            Mouse.OverrideCursor = null;
        }
        private void canShowScatterplot(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 2;
        }
        private void showScatterplot(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var ids = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().Select(st => st.ID).ToArray();
            //Scatterplot sp = new Scatterplot();
            //var vm = new NewExperimentViewModel(managerVm,ids[0], ids[1], UIService.Instance);
            //sp.DataContext = vm;
            //sp.Owner = this;
            //sp.Show();
            Mouse.OverrideCursor = null;
        }
    }
}
