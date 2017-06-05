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
        private readonly IUIService uiService;
        private readonly Func<string[], string[]> getSubOptions = null;
        private string[] path = new string[] { };

        public ChooseOptionsWindow(string[] options, string[] selected, IUIService uiService)
        {
            if (uiService == null) throw new ArgumentNullException("uiService");
            this.uiService = uiService;

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

        public ChooseOptionsWindow(string[] options, string selected, IUIService uiService, Func<string[], string[]> getSubOptions = null)
        {
            if (uiService == null) throw new ArgumentNullException("uiService");
            this.uiService = uiService;

            InitializeComponent();

            listBox.SelectionMode = SelectionMode.Single;
            foreach (var item in options)
            {
                listBox.Items.Add(item);
            }
            listBox.SelectedItem = selected;
            listBox.Focus();

            if (getSubOptions != null)
            {
                this.getSubOptions = getSubOptions;
                listBox.MouseDoubleClick += OnDoubleClick;
            }
        }

        private void OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (listBox.SelectedItem != null && listBox.SelectedItem is string)
                {
                    pnPath.Visibility = Visibility.Visible;

                    string sel = (string)listBox.SelectedItem;
                    string[] path2 = new string[path.Length + 1];
                    for (int i = 0; i < path.Length; i++)
                        path2[i] = path[i];
                    path2[path.Length] = sel;
                    string[] sub = getSubOptions(path2);
                    Array.Sort<string>(sub);

                    path = path2;
                    listBox.Items.Clear();
                    foreach (var item in sub)
                    {
                        listBox.Items.Add(item);
                    }

                    tbPath.Text = String.Join("/", path2);
                }
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex);
            }
        }


        public string[] SelectedOptions
        {
            get
            {
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
