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
        private readonly IUIService uiService;

        private ExperimentManagerViewModel managerVm;
        private ExperimentListViewModel experimentsVm;

        public static RoutedCommand SaveMetaCSVCommand = new RoutedCommand();
        public static RoutedCommand SaveOutputCommand = new RoutedCommand();
        public static RoutedCommand SaveMatrixCommand = new RoutedCommand();
        public static RoutedCommand SaveBinaryCommand = new RoutedCommand();
        public static RoutedCommand FlagCommand = new RoutedCommand();
        public static RoutedCommand TallyCommand = new RoutedCommand();
        public static RoutedCommand CopyCommand = new RoutedCommand();
        public static RoutedCommand MoveCommand = new RoutedCommand();
        public static RoutedCommand RestartCommand = new RoutedCommand();
        public static RoutedCommand CreateGroupCommand = new RoutedCommand();
        public static RoutedCommand CompareCommand = new RoutedCommand();
        public static RoutedCommand ScatterplotCommand = new RoutedCommand();
        public static RoutedCommand ReinforcementsCommand = new RoutedCommand();
        public static RoutedCommand RequeueIErrorsCommand = new RoutedCommand();
        public static RoutedCommand RecoveryCommand = new RoutedCommand();
        public static RoutedCommand DuplicatesCommand = new RoutedCommand();

        public MainWindow()
        {
            InitializeComponent();

            connectionString.Text = Properties.Settings.Default.ConnectionString;

            domainResolver = new DomainResolver(new[] { Measurement.Domain.Default, new Measurement.Z3Domain() });

            ProgramStatusViewModel statusVm = new ProgramStatusViewModel();
            statusBar.DataContext = statusVm;

            uiService = new UIService(statusVm);
        }

        private ExperimentManagerViewModel Connect(string connectionString)
        {
            if (Directory.Exists(connectionString))
            {
                LocalExperimentManager manager = LocalExperimentManager.OpenExperiments(connectionString, domainResolver);
                return new LocalExperimentManagerViewModel(manager, uiService, domainResolver);
            }
            else
            {
                AzureExperimentManager azureManager = AzureExperimentManager.OpenWithoutStart(new AzureExperimentStorage(connectionString));
                return new AzureExperimentManagerViewModel(azureManager, uiService, domainResolver);
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnConnect.IsEnabled = false;
                if (experimentsVm == null)
                {
                    var handle = uiService.StartIndicateLongOperation("Connecting...");
                    try
                    {                        
                        managerVm = Connect(connectionString.Text);
                        experimentsVm = managerVm.BuildListView();
                    }
                    finally
                    {
                        uiService.StopIndicateLongOperation(handle);
                    }

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
                }
                else // disconnect
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
            catch (Exception ex)
            {
                btnConnect.IsEnabled = true;
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
                for (int i = 0; i < count; i++)
                {
                    int id = ids[i];
                    try
                    {
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
            Mouse.OverrideCursor = Cursors.Wait;

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
            var vm = new NewExperimentViewModel(managerVm, uiService);
            dlg.DataContext = vm;
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                ExperimentDefinition def =
                    ExperimentDefinition.Create(
                        vm.Executable, vm.BenchmarkContainerUri, vm.BenchmarkLibrary, vm.Extension, vm.Parameters,
                        TimeSpan.FromSeconds(vm.BenchmarkTimeoutSec), vm.Domain,
                        vm.Categories, vm.BenchmarkMemoryLimitMb);
                try
                {
                    await managerVm.SubmitExperiment(def, System.Security.Principal.WindowsIdentity.GetCurrent().Name, vm.Note);
                }
                catch (Exception ex)
                {
                    uiService.ShowError(ex.Message, "Failed to submit an experiment");
                }
            }
        }
        private void canShowProperties(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 1;
        }
        private async void showProperties(object target, ExecutedRoutedEventArgs e)
        {
            var item = dataGrid.SelectedItem as ExperimentStatusViewModel;
            if (item == null) return;

            var handle = uiService.StartIndicateLongOperation("Loading properties of the experiment...");
            try
            {
                var vm = await managerVm.BuildProperties(item.ID);

                ExperimentProperties dlg = new ExperimentProperties();
                dlg.DataContext = vm;
                dlg.Owner = this;
                dlg.Show();
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }

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
            var ids = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().ToArray();
            CompareExperiments dlg = new CompareExperiments();
            var vm = managerVm.BuildComparingResults(ids[0].ID, ids[1].ID, ids[0].Definition, ids[1].Definition);
            dlg.DataContext = vm;
            dlg.Owner = this;
            dlg.Show();
        }
        private void canShowScatterplot(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 2;
        }
        private void showScatterplot(object target, ExecutedRoutedEventArgs e)
        {
            var handle = uiService.StartIndicateLongOperation("Building scatter plot...");
            try
            {
                var ids = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().ToArray();
                var vm = managerVm.BuildComparingResults(ids[0].ID, ids[1].ID, ids[0].Definition, ids[1].Definition);
                Scatterplot sp = new Scatterplot(vm, ids[0], ids[1]);
                sp.Show();
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }

        }
        private void dataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGrid.SelectedItems.Count != 1)
                return;

            var st = (ExperimentStatusViewModel)dataGrid.SelectedItem;
            ShowResults dlg = new ShowResults();
            var vm = managerVm.BuildResultsView(st.ID, st.Definition.BenchmarkDirectory);
            dlg.DataContext = vm;
            dlg.Owner = this;
            dlg.Show();
        }
        private void canSaveBinary(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 1;
        }
        private void saveBinary(object target, ExecutedRoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
        private void canSaveOutput(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 1;
        }
        private void saveOutput(object target, ExecutedRoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
        private void canSaveMatrix(object sender, CanExecuteRoutedEventArgs e)
        {
            if (managerVm == null || dataGrid.SelectedItems.Count <= 1)
            {
                e.CanExecute = false;
                return;
            }
            else
            {

                var sts = dataGrid.SelectedItems.Cast<ExperimentStatusViewModel>().ToArray();
                string rc = sts[0].Category;
                for (var i = 0; i < sts.Length; i++)
                {
                    if (sts[i].Category != rc)
                    {
                        e.CanExecute = false;
                        return;
                    }
                }
            }
            e.CanExecute = true;
        }
        private void saveMatrix(object target, ExecutedRoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
        private void canShowReinforcements(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 1;
        }
        private void showReinforcements(object target, ExecutedRoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
        private void canRequeueIErrors(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count > 0;
        }
        private void requeueIErrors(object target, ExecutedRoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
        private void canRecovery(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count > 0;
        }
        private void recovery(object target, ExecutedRoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
        private void canShowDuplicates(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count >= 1;
        }
        private void showDuplicates(object target, ExecutedRoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
