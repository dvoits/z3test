using System;
using System.Collections.Generic;
using System.Globalization;
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
        public ExperimentProperties()
        {
            InitializeComponent();
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            ExperimentPropertiesViewModel vm = DataContext as ExperimentPropertiesViewModel;
            if(vm != null)
            {
                if (vm.SubmitNote.CanExecute(null))
                    vm.SubmitNote.Execute(null);
            }
            Close();
        }
    }

    public class BoolToAsteriskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool && (bool) value)
            {
                return "*";
            }
            else return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
