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
    /// Interaction logic for ExperimentProperties.xaml
    /// </summary>
    public partial class ExperimentProperties : Window
    {
        private int id;
        private ExperimentListViewModel experimentsVm;
        private ExperimentStatusViewModel statusVm;
        private bool changed;
        //private ExperimentDefinitionViewModel defVm; 
        public ExperimentProperties(ExperimentListViewModel experimentsVm, int id)
        {
            InitializeComponent();
            this.experimentsVm = experimentsVm;
            statusVm = experimentsVm.Items.Where(item => item.ID == id).ToArray()[0];
            this.id = id;
            Loaded += delegate { update(); };
            changed = false;
        }
        private void submitNote()
        {
            if (changed)
            {
                //experimentsVm.UpdateNote(id, txtNote.Text);
            }
        }
        private void update()
        {
            lblID.Content = "Experiment #" + id.ToString();
            txtSubmissionTime.Text = statusVm.Submitted;
            txtCategory.Text = statusVm.Category;

            lblTotal.Content = statusVm.BenchmarksTotal;
            lblFinished.Content = statusVm.BenchmarksDone;
            lblRunning.Content = statusVm.BenchmarksQueued;
            lblRunning.Foreground = (statusVm.BenchmarksQueued == 0) ? Brushes.Green : Brushes.Red;
            
            //some labels: sat, unsat, unknown, ...., bug, error, timeout, memout

            
            txtCreator.Text = statusVm.Creator;
            txtNote.Text = statusVm.Note;
        }
        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            submitNote();
            Close();
        }

        private void updateButton_Click(object sender, RoutedEventArgs e)
        {
            submitNote();
            update();
        }
        private void txtNote_TextChanged(object sender, TextChangedEventArgs e)
        {
            changed = true;
        }
    }
}
