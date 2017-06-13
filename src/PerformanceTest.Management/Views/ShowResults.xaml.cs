using Measurement;
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
using System.Windows.Shapes;

namespace PerformanceTest.Management
{
    /// <summary>
    /// Interaction logic for ShowResults.xaml
    /// </summary>
    public partial class ShowResults : Window
    {
        public static RoutedCommand ReclassifyOKCommand = new RoutedCommand();
        public static RoutedCommand ReclassifyBugCommand = new RoutedCommand();
        public static RoutedCommand ReclassifyErrorCommand = new RoutedCommand();
        public static RoutedCommand ReclassifyTimeoutCommand = new RoutedCommand();
        public static RoutedCommand ReclassifyMemoutCommand = new RoutedCommand();
        public static RoutedCommand RequeueCommand = new RoutedCommand();
        public static RoutedCommand CopyFilenameCommand = new RoutedCommand();
        public ShowResults()
        {
            InitializeComponent();
        }

        private void canReclassify(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count > 0;
        }
        private void Reclassify(ResultStatus rc)
        {
            throw new NotImplementedException();
            //try
            //{
            //    var elems = dataGrid.SelectedItems.Cast<BenchmarkResultViewModel>();
            //    foreach (var vm in elems)
            //    {
            //        vm.Status = rc;
            //    }
            //    Console.WriteLine();
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(this, "Exception: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            //}
        }
        private void ReclassifyOK(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Reclassify(ResultStatus.Success);
            Mouse.OverrideCursor = null;
        }
        private void ReclassifyBug(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Reclassify(ResultStatus.Bug);
            Mouse.OverrideCursor = null;
        }
        private void ReclassifyError(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Reclassify(ResultStatus.Error);
            Mouse.OverrideCursor = null;
        }
        private void ReclassifyTimeout(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Reclassify(ResultStatus.Timeout);
            Mouse.OverrideCursor = null;
        }
        private void ReclassifyMemout(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Reclassify(ResultStatus.OutOfMemory);
            Mouse.OverrideCursor = null;
        }
        private void canRequeue(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count >= 1;
        }
        private void Requeue(object target, ExecutedRoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
        private void canCopyFilename(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 1;
        }
        private void CopyFilename(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            BenchmarkResultViewModel elem = (BenchmarkResultViewModel)dataGrid.SelectedItem;
            Clipboard.SetText(elem.Filename);
            Mouse.OverrideCursor = null;
        }
        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ShowResultsViewModel;
            if ((RadioButton)sender == radioAll) vm.FilterResultsByError(-1);
            else if ((RadioButton)sender == radioSAT) vm.FilterResultsByError(0);
            else if ((RadioButton)sender == radioUNSAT) vm.FilterResultsByError(1);
            else if ((RadioButton)sender == radioUNKNOWN) vm.FilterResultsByError(2);
            else if ((RadioButton)sender == radioBUGS) vm.FilterResultsByError(3);
            else if ((RadioButton)sender == radioERROR) vm.FilterResultsByError(4);
            else if ((RadioButton)sender == radioTimeouts) vm.FilterResultsByError(5);
            else if ((RadioButton)sender == radioMemouts) vm.FilterResultsByError(6);
            else if ((RadioButton)sender == radioOver) vm.FilterResultsByError(7);
            else if ((RadioButton)sender == radioUnder) vm.FilterResultsByError(8);
            else if ((RadioButton)sender == radioMoreThan)
            {
                int limit;
                try
                {
                    limit = Convert.ToInt32(txtSeconds.Text);
                }
                catch (FormatException)
                {
                    txtSeconds.Text = "0";
                    limit = 0;
                }
                vm.FilterResultsByRuntime(limit);
            }
            else if ((RadioButton)sender == radioOutputContains) vm.FilterResultsByText(txtOutputMatch.Text, 1);
            else if ((RadioButton)sender == radioFNSAT) vm.FilterResultsByText("sat", 0);
            else if ((RadioButton)sender == radioFNUNSAT) vm.FilterResultsByText("unsat", 0);
            else if ((RadioButton)sender == radioFNTEXT) vm.FilterResultsByText(txtFilename.Text, 0);


        }
        private void txtFilename_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as ShowResultsViewModel;
                vm.FilterResultsByText(txtFilename.Text, 0);
                radioFNTEXT.IsChecked = true;
            }
        }
        private void txtSeconds_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as ShowResultsViewModel;
                int limit;
                try
                {
                    limit = Convert.ToInt32(txtSeconds.Text);
                }
                catch (FormatException)
                {
                    txtSeconds.Text = "0";
                    limit = 0;
                }
                vm.FilterResultsByRuntime(limit);
                radioMoreThan.IsChecked = true;
            }
        }

        private void txtOutputMatch_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as ShowResultsViewModel;
                vm.FilterResultsByText(txtOutputMatch.Text, 1);
                radioOutputContains.IsChecked = true;
            }
        }
        private async void dataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGrid.SelectedItems.Count != 1)
                return;

            try
            {
                BenchmarkResultViewModel vm = (BenchmarkResultViewModel)dataGrid.SelectedItem;
                ShowOutput w = new ShowOutput();
                w.Owner = this;
                w.Show();

                w.DataContext = await vm.GetOutputViewModel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Failed to load output");
            }
        }
    }
}
