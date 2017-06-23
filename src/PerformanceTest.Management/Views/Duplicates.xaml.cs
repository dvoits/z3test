﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Interaction logic for Duplicates.xaml
    /// </summary>
    public partial class Duplicates : Window
    {
        private DuplicatesViewModel vm;
        public Duplicates(DuplicatesViewModel vm)
        {
            InitializeComponent();
            this.DataContext = this.vm = vm;
        }
        private void dataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGrid.SelectedItems.Count == 1)
            {
                var item = (BenchmarkResultViewModel)dataGrid.SelectedItem;
                var items = dataGrid.Items.Cast<BenchmarkResultViewModel>().ToList();
                items.Remove(item);
                vm.Pick(items);
                Close();
            }
        }
    }
}
