using System;
using System.Collections.Generic;
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
        void ShowWarning(string warning, string caption = null);
        /// <summary>Prompts a user to select a folder.</summary>
        /// <returns>Returns a selected folder path or null, if the user has cancelled selection.</returns>
        string ChooseFolder(string initialFolder, string description = null);

        string[] ChooseFiles(string initialPath, string filter, string defaultExtension);

        string[] ChooseOptions(string title, string[] options, string[] selectedOptions);

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
        public void ShowError(string error, string caption = null)
        {
            MessageBox.Show(error, caption ?? "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
