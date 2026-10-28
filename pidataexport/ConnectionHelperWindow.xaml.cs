using OSIsoft.AF.PI;
using OSIsoft.AF;
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
using System.IO;
using Newtonsoft.Json;

namespace pidataexport
{
    /// <summary>
    /// Interaction logic for ConnectionHelperWindow.xaml
    /// </summary>
    public partial class ConnectionHelperWindow : Window
    {

        public PIServer PiServer { get; private set; }
        public PISystem PiSystem { get; private set; }

        public ConnectionHelperWindow()
        {
            InitializeComponent();
           /* LoadRememberedSettings();*/

            LoadServers();
            this.DataContext = this;
        }
        public void InitializeFromConfig(string afServerName, string piServerName)
        {
            // Populate servers exactly the same way as today
            LoadServers();

            // Preselect AF server
            var afItem = AFServersComboBox.Items
                .Cast<PISystem>()
                .FirstOrDefault(s => s.Name == afServerName);

            if (afItem != null)
                AFServersComboBox.SelectedItem = afItem;

            // Preselect PI Data Archive
            var piItem = PIDAServersComboBox.Items
                .Cast<PIServer>()
                .FirstOrDefault(s => s.Name == piServerName);

            if (piItem != null)
                PIDAServersComboBox.SelectedItem = piItem;
        }
        private void LoadServers()
        {
            try
            {
                PISystems _piSystems = new PISystems();
                PIServers _piServers = new PIServers();

                foreach (var afserver in _piSystems)
                {
                    AFServersComboBox.Items.Add(afserver);
                }
                foreach (var pida in _piServers)
                {
                    PIDAServersComboBox.Items.Add(pida);
                }
                    
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to PI system: {ex.Message}");
            }
        }
        private async void Ok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PISystem afServer = AFServersComboBox.SelectedValue as PISystem;
                PIServer piServer = PIDAServersComboBox.SelectedValue as PIServer;

                if (afServer == null || piServer == null)
                {
                    MessageBox.Show("Please select both a PI AF Server and a PI Data Archive.");
                    return;
                }

                bool afConnected = false;
                bool piConnected = false;

                var win = new LoadingBox
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                if (useCreds.IsChecked == true)
                {                

                    try
                    {
                        // Show window FIRST
                        win.ConnectionStatusText.Text = $"Connecting to AF Server {afServer.Name}...";
                        win.Show();

                        // Force UI to render before background work
                        await Task.Delay(50);

                        await Task.Run(() => afServer.ConnectWithPrompt(null));
                        afConnected = true;
                        PiSystem=afServer;

                        win.ConnectionStatusText.Text = $"Connecting to PI DA Server {piServer.Name}...";

                        await Task.Delay(50);

                        await Task.Run(() => piServer.ConnectWithPrompt());
                        piConnected = true;
                        PiServer = piServer;
                        win.Close();
                    }
                    catch (Exception ex)
                    {
                        win.Close();
                        MessageBox.Show($"Connection failed: {ex.Message}");
                    }
                }

                else
                {

                    try
                    {
                        // Show loading UI
                        win.ConnectionStatusText.Text = $"Connecting to AF Server {afServer.Name}...";
                        win.Show();

                        // Let UI render
                        await Task.Delay(50);

                        // Background AF connection
                        await Task.Run(() => afServer.Connect());
                        afConnected = true;
                        PiSystem = afServer;
                        // Update message
                        win.ConnectionStatusText.Text = $"Connecting to PI DA Server {piServer.Name}...";

                        await Task.Delay(50);

                        // Background PI connection
                        await Task.Run(() => piServer.Connect());
                        piConnected = true;
                        PiServer = piServer;
                        // Done
                        win.Close();
                    }
                    catch (Exception ex)
                    {
                        win.Close();
                        MessageBox.Show($"Connection failed: {ex.Message}");
                    }
                }


                // Only proceed if both are connected
                if (!afConnected || !piConnected)
                    return;

                // Update labels in MainWindow
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.AFServer.Content = afServer.Name;
                    mainWindow.PIDAServer.Content = piServer.Name;

                    // Update _piSystem reference so LoadAFDatabases works on selected AF server
                    mainWindow._piSystem = afServer;
                    mainWindow.LoadAFDatabases();
                }
                //TEMPORARILY REMOVING REMEMBER CHOICE OPTION BOX IN VERSION V1.2
              /*  if (rememberChoice.IsChecked == true)
                {
                    SaveConnectionSettings(
                        AFServersComboBox.SelectedItem?.ToString(),
                        PIDAServersComboBox.SelectedItem?.ToString()
                    );
                }*/

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}");
            }
        }
        private void SaveConnectionSettings(string afServer, string piServer)
        {
            var settings = new ConnectionSettings
            {
                AFServer = afServer,
                PIServer = piServer
            };

            string folder = GetConfigFolder();
            string file = System.IO.Path.Combine(folder, "connection_settings.json");

            File.WriteAllText(file, JsonConvert.SerializeObject(settings));

        }
        private string GetConfigFolder()
        {
            string exeFolder = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location
            );

            string configFolder = System.IO.Path.Combine(exeFolder, "config");
            Directory.CreateDirectory(configFolder);

            return configFolder;
        }
      

        private void ComboBoxes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasAF = AFServersComboBox.SelectedItem != null;
            bool hasPI = PIDAServersComboBox.SelectedItem != null;

          /*  rememberChoice.IsEnabled = hasAF && hasPI;*/
        }


    }
}
