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
    public partial class ChooseOptionsWindow : Window
    {
        public ChooseOptionsWindow(string[] options, string[] selected)
        {
            InitializeComponent();

            listBox.SelectionMode = SelectionMode.Multiple;
            foreach (var item in options)
            {
                listBox.Items.Add(item);
                if (selected.Contains(item))
                    listBox.SelectedItems.Add(item);
            }
            listBox.Focus();
        }

        public ChooseOptionsWindow(string[] options, string selected)
        {
            InitializeComponent();

            listBox.SelectionMode = SelectionMode.Single;
            foreach (var item in options)
            {
                listBox.Items.Add(item);
            }
            listBox.SelectedItem = selected;
            listBox.Focus();
        }


        public string[] SelectedOptions
        {
            get {
                int n = listBox.SelectedItems.Count;
                string[] items = new string[n];
                for (int i = 0; i < n; i++)
                {
                    items[i] = listBox.SelectedItems[i].ToString();
                }
                return items;
            }
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            if (listBox.SelectedItems.Count != 0)
            {
                this.DialogResult = true;
                Close();
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }
    }
}
