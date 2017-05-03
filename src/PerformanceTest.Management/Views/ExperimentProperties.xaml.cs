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
        //private ExperimentListViewModel experimentsVm;
        private ExperimentStatusViewModel statusVm;
        private bool changed;
        //private ExperimentDefinitionViewModel defVm; 
        public ExperimentProperties(ExperimentListViewModel experimentsVm, int id)
        {
            InitializeComponent();
          //  this.experimentsVm = experimentsVm;
            statusVm = experimentsVm.Items.Where(item => item.ID == id).ToArray()[0];
            this.id = id;
            Loaded += delegate { update(); };
            changed = false;
        }
        private void submitNote()
        {
            if (changed)
            {
                statusVm.Note = txtNote.Text;
            }
        }
        private void update()
        {
            lblID.Content = this.Title = "Experiment #" + id.ToString();
            txtSubmissionTime.Text = statusVm.Submitted;
            txtCategory.Text = statusVm.Category;

            lblTotal.Content = statusVm.Total;
            lblFinished.Content = statusVm.Done;
            lblRunning.Content = statusVm.Queued;
            lblRunning.Foreground = (statusVm.Queued == 0) ? Brushes.Green : Brushes.Red;

            lblSAT.Content = 0;
            lblUNSAT.Content = 0;
            lblUnknown.Content = 0;
            lblOver.Content = 0;
            lblUnder.Content = 0;
            
            int bugs = 0; //where result code = 3
            int errors = 0; //where result code = 4
            int timeout = 0; //where result code = 5
            int memoryout = 0; //where result code = 6

            lblBug.Content = bugs;
            lblNonzero.Content = errors;
            lblMemdout.Content = memoryout;
            lblTimedout.Content = timeout;

            lblBug.Foreground = (bugs == 0) ? Brushes.Black : Brushes.Red;
            lblNonzero.Foreground = (errors == 0) ? Brushes.Black : Brushes.Red;
            lblMemdout.Foreground = (memoryout == 0) ? Brushes.Black : Brushes.Red;
            lblTimedout.Foreground = (timeout == 0) ? Brushes.Black : Brushes.Red;

            txtCreator.Text = statusVm.Creator;
            txtNote.Text = statusVm.Note;

            lblMachineStatus.Content = "Unable to retrieve status";
            lblMachineStatus.Foreground = Brushes.Orange;
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
