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

namespace pidataexport
{
    /// <summary>
    /// Interaction logic for ConnectionHelperWindow.xaml
    /// </summary>
    public partial class ConnectionHelperWindow : Window
    {
        public ConnectionHelperWindow()
        {
            InitializeComponent();


            LoadServers();
            /*            AfServerComboBox.Items.Add("AFServer1");
                        AfServerComboBox.Items.Add("AFServer2");
                        PiDataArchiveComboBox.Items.Add("PIServer1");
                        PiDataArchiveComboBox.Items.Add("PIServer2");*/

            this.DataContext = this;
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
        private void Ok_Click(object sender, RoutedEventArgs e)
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

                // Use creds checkbox determines method
                if (useCreds.IsChecked == true)
                {
                    try
                    {
                        afServer.ConnectWithPrompt(null);
                        afConnected = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to connect to AF Server {afServer.Name} with credentials: {ex.Message}");
                    }

                    try
                    {
                        piServer.ConnectWithPrompt();
                        piConnected = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to connect to PI Server {piServer.Name} with credentials: {ex.Message}");
                    }
                }
                else
                {
                    try
                    {
                        afServer.Connect();
                        afConnected = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to connect to AF Server {afServer.Name}: {ex.Message}");
                    }

                    try
                    {
                        piServer.Connect();
                        piConnected = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to connect to PI Server {piServer.Name}: {ex.Message}");
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

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}");
            }
        }


    }
}
