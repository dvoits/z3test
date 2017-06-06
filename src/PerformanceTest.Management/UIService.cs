using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace PerformanceTest.Management
{
    public interface IUIService
    {
        void ShowError(string error, string caption = null);
        void ShowError(Exception ex, string caption = null);

        void ShowWarning(string warning, string caption = null);
        /// <summary>Prompts a user to select a folder.</summary>
        /// <returns>Returns a selected folder path or null, if the user has cancelled selection.</returns>
        string ChooseFolder(string initialFolder, string description = null);

        string[] ChooseFiles(string initialPath, string filter, string defaultExtension);

        string[] ChooseOptions(string title, string[] options, string[] selectedOptions);

        Task<string[]> BrowseTree(string title, string[] selected, Func<string[], Task<string[]>> getChildren);

        string ChooseOption(string title, string[] options, string selectedOption);

        long StartIndicateLongOperation(string status = null);

        void StopIndicateLongOperation(long handle);
    }

    public class UIService : IUIService
    {
        private ProgramStatusViewModel statusVm;

        public UIService(ProgramStatusViewModel statusVm)
        {
            if (statusVm == null) throw new ArgumentNullException("statusVm");
            this.statusVm = statusVm;
        }
        public void ShowWarning(string warning, string caption = null)
        {
            MessageBox.Show(warning, caption ?? "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }



        public void ShowError(Exception ex, string caption = null)
        {
            Trace.WriteLine("Application error: " + ex);

            string message = GetMessage(ex);
            ShowError(message, caption);
        }

        public void ShowError(string error, string caption = null)
        {
            MessageBox.Show(error, caption ?? "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private string GetMessage(Exception ex)
        {
            string message;
            if (ex == null) message = "An error has occured.";
            else
            {
                AggregateException aex = ex as AggregateException;
                List<string> lines = new List<string>();
                if (aex != null && aex.InnerExceptions.Count > 1)
                {
                    foreach (var x in aex.InnerExceptions)
                    {
                        lines.Add(GetMessage(x));
                    }
                    message = String.Join(Environment.NewLine, lines.Distinct());
                }
                else
                {
                    message = ex.Message;
                }
            }
            return message;
        }

        public string ChooseFolder(string initialFolder, string description = null)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            if (description != null)
                dlg.Description = description;
            if (Directory.Exists(initialFolder))
                dlg.SelectedPath = initialFolder;

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return dlg.SelectedPath;
            }
            return null;
        }

        public string[] ChooseFiles(string initialPath, string filter, string defaultExtension)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = filter;
            dlg.CheckFileExists = true;
            dlg.InitialDirectory = initialPath != null ? Path.GetDirectoryName(initialPath) : null;
            dlg.Multiselect = true;
            dlg.DefaultExt = defaultExtension;
            if (initialPath != null && File.Exists(initialPath))
                dlg.FileName = initialPath;

            if (dlg.ShowDialog() == true)
            {
                return dlg.FileNames;
            }
            else
            {
                return null;
            }
        }


        public string[] ChooseOptions(string title, string[] options, string[] selectedOptions)
        {
            var dlg = new ChooseOptionsWindow(options, selectedOptions);
            dlg.Title = title;
            if (dlg.ShowDialog() == true)
            {
                return dlg.SelectedOptions;
            }
            return null;
        }

        public string ChooseOption(string title, string[] options, string selectedOption)
        {
            var dlg = new ChooseOptionsWindow(options, selectedOption);
            dlg.Title = title;
            if (dlg.ShowDialog() == true)
            {
                return dlg.SelectedOptions.Length > 0 ? dlg.SelectedOptions[0] : null;
            }
            return null;
        }

        public async Task<string[]> BrowseTree(string title, string[] selected, Func<string[], Task<string[]>> getChildren)
        {
            var dlg = new BrowseDialog();
            BrowseTreeViewModel vm;

            var handle = StartIndicateLongOperation("Listing items...");
            try
            {
                var root = await GetTreeChildren(null, getChildren);
                vm = new BrowseTreeViewModel(title, root);
                await vm.Select(selected);
                dlg.DataContext = vm;
            }
            finally
            {
                StopIndicateLongOperation(handle);
            }

            if(dlg.ShowDialog() == true)
            {
                return vm.SelectedPath != null ? vm.SelectedPath.Select(t => t.Text).ToArray() : new string[0];
            }
            return null;
        }

        private async Task<BrowseTreeItemViewModel[]> GetTreeChildren(BrowseTreeItemViewModel parent, Func<string[], Task<string[]>> getChildrenContent)
        {
            List<string> path = new List<string>();
            BrowseTreeItemViewModel t = parent;
            while (t != null)
            {
                path.Add(t.Text);
                t = t.Parent;
            }
            path.Reverse();
            var children = await getChildrenContent(path.ToArray());
            return children.Select(child => new BrowseTreeItemViewModel(child, parent, new GetChildren(p => GetTreeChildren(p, getChildrenContent)))).ToArray();
        }

        private long opsId = 0;
        private List<Tuple<long, string>> statuses = new List<Tuple<long, string>>();

        public long StartIndicateLongOperation(string status = null)
        {
            if (status == null) status = "Working...";

            statuses.Add(Tuple.Create(opsId, status));
            if (statuses.Count == 1)
            {
                Mouse.OverrideCursor = Cursors.Wait;
            }

            statusVm.Status = status;
            return unchecked(opsId++);
        }

        public void StopIndicateLongOperation(long handle)
        {
            for (int i = statuses.Count; --i>=0; )
            {
                var s = statuses[i];
                if(s.Item1 == handle)
                {
                    statuses.RemoveAt(i);
                    if (statuses.Count == 0)
                    {
                        Mouse.OverrideCursor = null;
                        statusVm.Status = "Ready.";
                    }else
                    {
                        statusVm.Status = statuses.Last().Item2;
                    }
                    return;
                }
            }
        }
    }
}
