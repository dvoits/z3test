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
    /// Interaction logic for CompareExperiments.xaml
    /// </summary>
    public partial class CompareExperiments : Window
    {
        public static RoutedCommand CopyFilenameCommand = new RoutedCommand();
        public CompareExperiments()
        {
            InitializeComponent();
        }

        private void canCopyFilename(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 1;
        }
        private void CopyFilename(object target, ExecutedRoutedEventArgs e)
        {
            ExperimentComparingResultsViewModel elem = (ExperimentComparingResultsViewModel)dataGrid.SelectedItem;
            Clipboard.SetText(elem.Filename);
        }
        private void dataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGrid.SelectedItems.Count != 1)
                return;
            //not implemented
        }
        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            //updateGrid((RadioButton)sender);
        }
    }
}
