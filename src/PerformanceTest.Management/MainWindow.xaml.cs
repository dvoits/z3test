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
        private readonly RecentValuesStorage recentValues;

        private AzureExperimentManagerViewModel managerVm;
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

            recentValues = new RecentValuesStorage();
            connectionString.Text = recentValues.ConnectionString;

            domainResolver = new DomainResolver(new[] { Measurement.Domain.Default, new Measurement.Z3Domain() });

            ProgramStatusViewModel statusVm = new ProgramStatusViewModel();
            statusBar.DataContext = statusVm;
            uiService = new UIService(statusVm);
        }

        private Task<AzureExperimentManagerViewModel> ConnectAsync(string connectionString)
        {
            return Task.Run(() => // run in thread pool
            {
                AzureExperimentManager azureManager = AzureExperimentManager.Open(connectionString);
                return new AzureExperimentManagerViewModel(azureManager, uiService, domainResolver);
            });
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnConnect.IsEnabled = false;
                if (experimentsVm == null)
                {
                    var handle = uiService.StartIndicateLongOperation("Connecting...");
                    try
                    {
                        managerVm = await ConnectAsync(connectionString.Text);
                        experimentsVm = managerVm.BuildListView();
                    }
                    finally
                    {
                        uiService.StopIndicateLongOperation(handle);
                    }

                    DataContext = experimentsVm;
                    recentValues.ConnectionString = connectionString.Text;
                    connectionString.IsReadOnly = true;
                    btnConnect.Content = "Disconnect";
                    btnConnect.IsEnabled = true;
                    btnNewJob.IsEnabled = true;
                    btnUpdate.IsEnabled = true;
                    menuNewJob.IsEnabled = true;
                    menuNewCatchAll.IsEnabled = true;
                    btnEdit.Visibility = Visibility.Collapsed;
                }
                else // disconnect
                {
                    experimentsVm = null;
                    connectionString.IsReadOnly = false;
                    btnConnect.Content = "Connect";
                    btnConnect.IsEnabled = true;
                    DataContext = null;
                    btnNewJob.IsEnabled = false;
                    btnUpdate.IsEnabled = false;
                    menuNewJob.IsEnabled = false;
                    menuNewCatchAll.IsEnabled = false;
                    btnEdit.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                btnConnect.IsEnabled = true;
                uiService.ShowError(ex, "Failed to connect");
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
            dataGrid.Columns[c - 6].Visibility = Visibility.Visible;
        }

        private void OptShowProgress_Unchecked(object sender, RoutedEventArgs e)
        {
            int c = dataGrid.Columns.Count();
            dataGrid.Columns[c - 1].Visibility = Visibility.Hidden;
            dataGrid.Columns[c - 2].Visibility = Visibility.Hidden;
            dataGrid.Columns[c - 3].Visibility = Visibility.Hidden;
            dataGrid.Columns[c - 4].Visibility = Visibility.Hidden;
            dataGrid.Columns[c - 5].Visibility = Visibility.Hidden;
            dataGrid.Columns[c - 6].Visibility = Visibility.Hidden;
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
                    experimentsVm.DeleteExperiment(id);
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
                var experiments = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().ToArray();
                managerVm.SaveCSVData(dlg.FileName, experiments);
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

                var experiments = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().ToArray();
                managerVm.SaveMetaData(dlg.FileName, experiments);
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

        private async void showTally(object target, ExecutedRoutedEventArgs e)
        {
            try
            {
                double total;
                var handle = uiService.StartIndicateLongOperation("Computing run time for the selected experiments...");
                var ids = (dataGrid.SelectedItems).Cast<ExperimentStatusViewModel>().Select(st => st.ID).ToArray();
                try
                {
                    total = await experimentsVm.GetRuntimes(ids);
                }
                finally
                {
                    uiService.StopIndicateLongOperation(handle);
                }
                TimeSpan ts = TimeSpan.FromSeconds(total);
                MessageBox.Show(this,
                               "The total amount of runtime spent computing the selected results is " + ts.ToString() + ".", "Tally",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);

            }
            catch (Exception ex)
            {
                uiService.ShowError(ex, "Failed to compute run time");
            }
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

        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void btnNewJob_Click(object sender, RoutedEventArgs args)
        {
            try
            {
                NewJobDialog dlg = new NewJobDialog();
                string creator = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                var vm = new NewExperimentViewModel(managerVm, uiService, recentValues, creator);
                dlg.DataContext = vm;
                dlg.Owner = this;
                if (dlg.ShowDialog() == true)
                {
                    var handle = uiService.StartIndicateLongOperation("Submitting new experiment...");
                    try
                    {
                        vm.SaveRecentSettings();
                    }
                    catch (Exception ex)
                    {
                        uiService.ShowWarning(ex.Message, "Failed to save recent settings");
                    }

                    Tuple<string, int?, Exception>[] result;
                    try
                    {
                        result = await managerVm.SubmitExperiments(vm, creator);
                    }
                    catch (Exception ex)
                    {
                        uiService.ShowError(ex, "Failed to submit an experiment");
                        return;
                    }
                    finally
                    {
                        uiService.StopIndicateLongOperation(handle);
                    }

                    experimentsVm.Refresh();

                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < result.Length; i++)
                    {
                        if (i > 0) sb.AppendLine();

                        var r = result[i];
                        if (r.Item2.HasValue)
                            sb.AppendFormat("Experiment for category {0} successfully submitted with id {1}.", r.Item1, r.Item2.Value);
                        if (r.Item3 != null)
                            sb.AppendFormat("Experiment for category {0} could not be submitted: {1}.", r.Item1, r.Item3.Message);
                    }
                    uiService.ShowInfo(sb.ToString(), "New experiments");
                }
            }
            catch (Exception e)
            {
                uiService.ShowError(e, "New experiment");
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
                System.ComponentModel.PropertyChangedEventHandler onPropertyChanded = (s, args) =>
                {
                    try
                    {
                        if (args.PropertyName == nameof(vm.Status))
                        {
                            item.NewStatus(vm.Status);
                        }
                        else if (args.PropertyName == nameof(vm.ExecutionStatus) && vm.ExecutionStatus != null)
                        {
                            item.JobStatus = vm.ExecutionStatus;
                        }
                    }
                    catch (Exception ex)
                    {
                        uiService.ShowError(ex, "Failed to refresh experiment status");
                    }
                };
                vm.PropertyChanged += onPropertyChanded;

                ExperimentProperties dlg = new ExperimentProperties();
                dlg.DataContext = vm;
                dlg.Owner = this;
                dlg.Closed += (s, args) =>
                {
                    vm.PropertyChanged -= onPropertyChanded;
                };

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
                //    ExperimentDefinition def = ids[i].Definition;
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
            dlg.SetUIService(uiService);
            dlg.Owner = this;
            dlg.Show();

            var vm = managerVm.BuildComparingResults(ids[0].ID, ids[1].ID, ids[0].Definition, ids[1].Definition);
            dlg.DataContext = vm;
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
                Scatterplot sp = new Scatterplot(vm, ids[0], ids[1], ids[0].Definition.BenchmarkTimeout.TotalSeconds, ids[1].Definition.BenchmarkTimeout.TotalSeconds, ids[0].Definition.MemoryLimitMB, ids[1].Definition.MemoryLimitMB, uiService);
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
            string sharedDirectory = "";
            if (st.Definition.BenchmarkDirectory != null && st.Definition.BenchmarkDirectory != "") {                
                sharedDirectory = st.Definition.BenchmarkDirectory + "/" + st.Definition.Category;
            }
            else sharedDirectory = st.Definition.Category;
           
            var vm = managerVm.BuildResultsView(st.ID, sharedDirectory);
            dlg.DataContext = vm;
            dlg.Owner = this;
            dlg.Show();
        }
        private void canSaveBinary(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 1;
        }
        private async void saveBinary(object target, ExecutedRoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog dlg = new System.Windows.Forms.SaveFileDialog();
            dlg.Filter = "Executable files (*.zip)|*.zip|All files (*.*)|*.*";
            dlg.FilterIndex = 1;
            dlg.RestoreDirectory = true;

            var handle = uiService.StartIndicateLongOperation("Save binary...");
            try
            {
                var experiment = (ExperimentStatusViewModel)dataGrid.SelectedItem;
                dlg.FileName = "binary_" + experiment.ID + ".zip";

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    Stream result = await managerVm.SaveExecutable(dlg.FileName, experiment.Definition.Executable);
                    string fn = dlg.FileName;
                    FileStream file = File.Open(fn, FileMode.OpenOrCreate, FileAccess.Write);
                    result.CopyTo(file);
                    file.Close();
                }
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex, "Failed to save binary");
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
        }
        private void canSaveOutput(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 1;
        }
        private void saveOutput(object target, ExecutedRoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.ShowNewFolderButton = true;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var experiment = (ExperimentStatusViewModel)dataGrid.SelectedItem;
                managerVm.SaveOutput(dlg.SelectedPath, experiment);
            }
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
            System.Windows.Forms.SaveFileDialog dlg = new System.Windows.Forms.SaveFileDialog();
            dlg.Filter = "LaTeX files (*.tex)|*.tex|All files (*.*)|*.*";
            dlg.FilterIndex = 1;
            dlg.RestoreDirectory = true;
            dlg.FileName = "matrix.tex";

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var experiments = dataGrid.SelectedItems.Cast<ExperimentStatusViewModel>().ToArray();
                managerVm.SaveMatrix(dlg.FileName, experiments);
            }
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

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConnectionStringBuilderViewModel vm = new ConnectionStringBuilderViewModel(connectionString.Text);
                ConnectionStringBuilder dlg = new ConnectionStringBuilder();
                dlg.DataContext = vm;
                dlg.Owner = this;
                if (dlg.ShowDialog() == true)
                {
                    connectionString.Text = vm.ConnectionString;
                }
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex, "Failed to edit the connection string");
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                recentValues.ShowProgress = mnuOptProgress.IsChecked;
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex, "Failed to save recent values");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                mnuOptProgress.IsChecked = recentValues.ShowProgress;
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex, "Failed to restore recent values");
            }
        }

        private void txtFilter_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as ExperimentListViewModel;
                if (vm != null)
                {
                    vm.FilterKeyword = txtFilter.Text;
                }
            }
        }
    }
}
