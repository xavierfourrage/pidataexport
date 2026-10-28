using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace pidataexport
{
    /// <summary>
    /// Interaction logic for OptionsWindow.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        public OptionsWindow()
        {
            InitializeComponent();
            LoadOptions();
        }
        private void BrowseExportFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ExportFolderBox.Text = dialog.SelectedPath;
            }
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var options = AppSettings.Options;

            options.DefaultExportFolder = ExportFolderBox.Text;
            options.AutoOpenExcelAfterExport = AutoOpenExcelCheck.IsChecked == true;
            options.MinimizeEFByDefault = MinimizeEFCheck.IsChecked == true;

            options.DefaultWidth =
                double.TryParse(DefaultWidthBox.Text, out double w) ? w : 1100;

            options.DefaultHeight =
                double.TryParse(DefaultHeightBox.Text, out double h) ? h : 700;

            AppSettings.Save();

            DialogResult = true;
            Close();
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void LoadOptions()
        {
            var options = AppSettings.Options;

            ExportFolderBox.Text = options.DefaultExportFolder;
            AutoOpenExcelCheck.IsChecked = options.AutoOpenExcelAfterExport;
            MinimizeEFCheck.IsChecked = options.MinimizeEFByDefault;

            DefaultWidthBox.Text = options.DefaultWidth.ToString();
            DefaultHeightBox.Text = options.DefaultHeight.ToString();
        }
     
    }
}
