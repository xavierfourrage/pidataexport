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
using System.Windows.Navigation;
using System.Windows.Shapes;
using OSIsoft.AF;
using OSIsoft.AF.Asset;
using OSIsoft.AF.EventFrame;
using OSIsoft.AF.PI;
using OSIsoft.AF.Search;
using OSIsoft.AF.Time;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Collections.ObjectModel;
using System.Threading;
using OSIsoft.AF.Data;
using System.IO;
using System.Globalization;
using OSIsoft.AF.Analysis;
using Newtonsoft.Json;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.Diagnostics;

namespace pidataexport
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        /*        private ObservableCollection<EventFrameRow> _eventFrameRows;*/
        private ObservableCollection<EventFrameRow> _eventFrameRows = new ObservableCollection<EventFrameRow>();

        private ICollectionView _collectionView;

        private ObservableCollection<AttributeRow> _attributeRows = new ObservableCollection<AttributeRow>();
        private ICollectionView _attrCollectionView;
        

        private readonly ObservableCollection<EFAttributeRow> _efAttributeRows = new ObservableCollection<EFAttributeRow>();
        private ICollectionView _efAttrCollectionView;

        private CancellationTokenSource _cts;

        private PIServer _piServer;
        public PISystem _piSystem;
        private bool _isLoadingConfiguration = false;
        public MainWindow()
        {
            InitializeComponent();

            if (AppSettings.Options.DefaultWidth > 0)
                Width = AppSettings.Options.DefaultWidth;

            if (AppSettings.Options.DefaultHeight > 0)
                Height = AppSettings.Options.DefaultHeight;

            EquipementComboBox0.Loaded += EquipementComboBox0_Loaded;

            AFDatabaseComboBox.Items.Clear();



            _eventFrameRows = new ObservableCollection<EventFrameRow>();
            _collectionView = CollectionViewSource.GetDefaultView(_eventFrameRows);
            /*EFDataGrid.ItemsSource = _collectionView;*/

            EFDataGrid.ItemsSource = _eventFrameRows;

            _attributeRows = new ObservableCollection<AttributeRow>();
            _attrCollectionView = CollectionViewSource.GetDefaultView(_attributeRows);
            EltAttrDataGrid.ItemsSource = _attributeRows;


            this.DataContext = this;
        }
        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            var config = new AppConfiguration
            {
                AFServer = AFServer.Content?.ToString(),
                PIDAServer = PIDAServer.Content?.ToString(),
                AFDatabase = (AFDatabaseComboBox.SelectedItem as AFDatabase)?.Name,
                PhaseFilter = Eventname.Text,
                StartTime = StartDatePicker.SelectedDate,
                EndTime = EndDatePicker.SelectedDate,
                SelectedTemplate= SelectedTemplateLabel.Content.ToString(),
            };

            var items = EquipementComboBox0.Items.Cast<MultiSelectItem>();
            config.SelectedElements = items
                .Where(i => i.IsSelected)
                .Select(i => i.Name)
                .ToList();

            string folder = GetConfigFolder();

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                InitialDirectory = folder,
                Filter = "JSON configuration (*.json)|*.json",
                DefaultExt = ".json",
                FileName = "config.json"
            };

            if (dialog.ShowDialog() != true)
                return;

            string file = dialog.FileName;

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);

            try
            {
                File.WriteAllText(file, json);
                MessageBox.Show("Configuration saved:\n" + file);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving configuration:\n" + ex.Message);
            }
        }
        private void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            string folder = GetConfigFolder();

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                InitialDirectory = folder,
                Filter = "JSON configuration (*.json)|*.json"
            };

            if (dialog.ShowDialog() != true)
                return;

            LoadConfigurationFromFile(dialog.FileName);
        }
        private void FileMenu_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                RecentConfigsMenu.Items.Clear();

                string folder = GetConfigFolder();

                if (!Directory.Exists(folder))
                    return;

                var files = Directory.GetFiles(folder, "*.json")
                                     .OrderByDescending(File.GetLastWriteTime);

                if (!files.Any())
                {
                    RecentConfigsMenu.Items.Add(new MenuItem
                    {
                        Header = "(No configurations found)",
                        IsEnabled = false
                    });
                    return;
                }

                foreach (var file in files)
                {
                    var item = new MenuItem
                    {
                        Header = System.IO.Path.GetFileName(file),
                        Tag = file
                    };

                    item.Click += RecentConfig_Click;

                    RecentConfigsMenu.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading recent configurations:\n" + ex.Message);
            }
        }
        private void RecentConfig_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuItem;
            string file = item?.Tag as string;

            if (string.IsNullOrEmpty(file))
                return;

            LoadConfigurationFromFile(file);
        }
        private void LoadConfigurationFromFile(string file)
        {
            try
            {
                _isLoadingConfiguration = true;

                var json = File.ReadAllText(file);
                var config = JsonConvert.DeserializeObject<AppConfiguration>(json);

                //---------------------------------------
                // Open Connection Helper with defaults
                //---------------------------------------

                var connectionWindow = new ConnectionHelperWindow()
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                }; 

                connectionWindow.InitializeFromConfig(
                    config.AFServer,
                    config.PIDAServer
                );

                bool? result = connectionWindow.ShowDialog();

                if (result != true)
                    return;

                var afServer = connectionWindow.PiSystem;
                var piServer = connectionWindow.PiServer;

                if (afServer == null || piServer == null)
                {
                    MessageBox.Show("Connection not established.");
                    return;
                }

                AFServer.Content = afServer.Name;
                PIDAServer.Content = piServer.Name;

                //---------------------------------------
                // Populate AF Database ComboBox
                //---------------------------------------

                AFDatabaseComboBox.Items.Clear();

                foreach (AFDatabase database in afServer.Databases)
                {
                    AFDatabaseComboBox.Items.Add(database);
                }

                //---------------------------------------
                // Select the saved database
                //---------------------------------------

                var dbItem = AFDatabaseComboBox.Items
                    .Cast<AFDatabase>()
                    .FirstOrDefault(d => d.Name == config.AFDatabase);

                if (dbItem != null)
                {
                    AFDatabaseComboBox.SelectedItem = dbItem;
                }

                //---------------------------------------
                // Load elements for this database
                //---------------------------------------

                LoadEquipements(null, null);


                //---------------------------------------
                // Restore filters
                //---------------------------------------

                Eventname.Text = config.PhaseFilter;
                StartDatePicker.SelectedDate = config.StartTime;
                EndDatePicker.SelectedDate = config.EndTime;

                //---------------------------------------
                // Restore selected elements
                //---------------------------------------

                var items = EquipementComboBox0.Items.Cast<MultiSelectItem>().ToList();

                foreach (var item in items)
                {
                    item.IsSelected = config.SelectedElements.Contains(item.Name);
                }

                EquipementComboBox0.Items.Refresh();

                //---------------------------------------
                // Trigger UI logic (template filtering etc.)
                //---------------------------------------

                EquipmentCheckBox_Changed(null, null);


                /*  if (!string.IsNullOrEmpty(config.SelectedTemplate))
                  {
                      AFElementTemplate template = dbItem.ElementTemplates[config.SelectedTemplate];

                      if (template != null)
                          _isLoadingConfiguration = true;
                      getAttributesTemplate(template);
                      _isLoadingConfiguration = false;
                  }*/
                _isLoadingConfiguration = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading configuration:\n" + ex.Message);
            }
        }

        private string GetConfigFolder()
        {
            string exeFolder = AppDomain.CurrentDomain.BaseDirectory;

            string configFolder = System.IO.Path.Combine(exeFolder, "config");

            Directory.CreateDirectory(configFolder);

            return configFolder;
        }

        private void OpenConnectionHelper_Click(object sender, RoutedEventArgs e)
        {
            var win = new ConnectionHelperWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            bool? result = win.ShowDialog();

            if (result == true)
            {
                _piServer = win.PiServer;
                _piSystem = win.PiSystem;
            }
            else
            {
                // User cancelled or connection failed
                // Do nothing (or show message if you want)
            }
        }



        public void LoadAFDatabases()
        {
            try
            {
                AFDatabases dbs = _piSystem.Databases;
                AFDatabaseComboBox.Items.Clear();

                foreach ( var db in dbs )
                {
                    AFDatabaseComboBox.Items.Add( db );
                }

               
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading AF databases: {ex.Message}");
            }
            
        }


        private void LoadEquipements(object sender, SelectionChangedEventArgs e)
        {
            //TO BE FURTHER INVESTIGATED
/*            if (_isLoadingConfiguration)
                return;*/

            AFDatabase aFDatabase = AFDatabaseComboBox.SelectedItem as AFDatabase;

            if (aFDatabase != null)
            {
                AFElementTemplate equipTemplate = aFDatabase.ElementTemplates["Equipment"];

                if (equipTemplate != null)
                {
                    AFNamedCollectionList<AFElement> aFElements = AFElement.FindElementsByTemplate(
                        aFDatabase,
                        null,
                        equipTemplate,
                        true,
                        AFSortField.Name,
                        AFSortOrder.Descending,
                        500);

                    // Clear previous items
                    EquipementComboBox0.Items.Clear();

                    if (aFElements.Count > 0)
                    {
                        foreach (AFElement aFElement in aFElements)
                        {
                            EquipementComboBox0.Items.Add(new MultiSelectItem
                            {
                                Element = aFElement,
                                IsSelected = false
                            });
                        }
                    }

                    // Ensure template is applied
                    EquipementComboBox0.ApplyTemplate();
                    var textBox = GetComboBoxTextBox(EquipementComboBox0);
                    EquipementComboBox0.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (textBox != null)
                        {
                            textBox.Text = string.Empty;
                            textBox.CaretIndex = 0;
                        }

                        EquipementComboBox0.Text = string.Empty;
                        EquipementComboBox0.SelectedItem = null;
                        EquipementComboBox0.IsDropDownOpen = false;

                      
                          /*  _attributeRows.Clear();*/

                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }
        private void EquipementComboBox_DropDownClosed(object sender, EventArgs e)
        {
            var selected = EquipementComboBox0.Items
                .Cast<MultiSelectItem>()
                .Where(i => i.IsSelected)
                .Select(i => i.Name);

            EquipementComboBox0.Text = string.Join(", ", selected);
        }
        private void EquipmentCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var items = EquipementComboBox0.Items.Cast<MultiSelectItem>().ToList();

            var selectedItems = items.Where(i => i.IsSelected).ToList();

            // If nothing selected → enable everything
            if (selectedItems.Count == 0)
            {
                foreach (var item in items)
                    item.IsEnabled = true;

                EquipementComboBox0.Items.Refresh();

                SelectedElementsPane.Visibility = Visibility.Collapsed;
                itemSelectedText.Content = "-";

                return;
            }

            var selectedTemplates = selectedItems
                .Select(i => i.Template)
                .Distinct()
                .ToList();

            var selectedTemplate = GetCommonTemplate(selectedTemplates);
            SelectedTemplateLabel.Content = selectedTemplate?.Name ?? "No common template";

            foreach (var item in items)
            {
                if (!item.IsSelected)
                {
                    item.IsEnabled = IsDerivedFrom(item.Template, selectedTemplate);
                }
            }
          
            SelectedElementsPane.Visibility = Visibility.Visible;
            ItemNumber.Content = selectedItems.Count;
            itemSelectedText.Content = selectedItems.Count == 1 ? "item selected" : "items selected";
            EquipementComboBox0.Items.Refresh();
            getAttributesTemplate(selectedTemplate);

        }
        private AFElementTemplate GetCommonTemplate(List<AFElementTemplate> templates)
        {
            if (templates == null || templates.Count == 0)
                return null;

            // Get ancestor chain for the first template
            List<AFElementTemplate> commonChain = GetTemplateHierarchy(templates[0]);

            foreach (var template in templates.Skip(1))
            {
                var chain = GetTemplateHierarchy(template);
                commonChain = commonChain.Intersect(chain).ToList();
            }

            // Return the closest ancestor (first in hierarchy)
            return commonChain.FirstOrDefault();
        }
        private List<AFElementTemplate> GetTemplateHierarchy(AFElementTemplate template)
        {
            var chain = new List<AFElementTemplate>();

            while (template != null)
            {
                chain.Add(template);
                template = template.BaseTemplate;
            }

            return chain;
        }
        private bool IsDerivedFrom(AFElementTemplate template, AFElementTemplate baseTemplate)
        {
            while (template != null)
            {
                if (template == baseTemplate)
                    return true;

                template = template.BaseTemplate;
            }

            return false;
        }
        private void EquipementComboBox0_Loaded(object sender, RoutedEventArgs e)
        {
            var textBox = GetComboBoxTextBox1(EquipementComboBox0);

            if (textBox != null)
            {
                textBox.TextChanged += EquipementComboBox0_TextChanged;
            }
        }
        private TextBox GetComboBoxTextBox1(ComboBox combo)
        {
            combo.ApplyTemplate();
            return combo.Template.FindName("PART_EditableTextBox", combo) as TextBox;
        }
        private void EquipementComboBox0_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            string text = textBox.Text.Trim();

            var items = EquipementComboBox0.Items.Cast<MultiSelectItem>().ToList();

            if (string.IsNullOrWhiteSpace(text))
            {
                foreach (var item in items)
                    item.IsSelected = false;

                EquipementComboBox0.Items.Refresh();
                EquipmentCheckBox_Changed(null, null);
                return;
            }

            string regexPattern = "^" + Regex.Escape(text)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            Regex regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

            foreach (var item in items)
                item.IsSelected = regex.IsMatch(item.Name);

            EquipementComboBox0.Items.Refresh();

            EquipmentCheckBox_Changed(null, null);
        }
        private void Abort_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }
        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _efAttributeRows.Clear();
            var button = sender as Button;

            string eventName = $"{Eventname.Text}";
            AFDatabase aFDatabase = AFDatabaseComboBox.SelectedItem as AFDatabase;

            List<string> eltNameList = EquipementComboBox0.Items
                .Cast<MultiSelectItem>()
                .Where(i => i.IsSelected)
                .Select(i => i.Name)
                .ToList();

            EFProgressBar.Visibility=Visibility.Visible;
            SearchProgressBar.Value = 0;
            SearchProgressBar.Maximum = eltNameList.Count;

            try
            {
                button.Content = "Loading...";
                button.IsEnabled = false;
                AbortButton.IsEnabled = true;

                var starttime = StartDatePicker.SelectedDate;
                var endtime = EndDatePicker.SelectedDate;

                if (!starttime.HasValue || !endtime.HasValue)
                {
                    MessageBox.Show("Please select both a start date and an end date.",
                                     "Missing Dates",
                                     MessageBoxButton.OK,
                                     MessageBoxImage.Warning);
                    return;
                }

                if (starttime > endtime)
                {
                    MessageBox.Show("The start date cannot be later than the end date.",
                                     "Invalid Date Range",
                                     MessageBoxButton.OK,
                                     MessageBoxImage.Warning);
                    return;
                }

                _eventFrameRows.Clear();

                await Task.Run(() =>
                {
                    if (eltNameList.Count > 0)
                    {
                       
                        int processed = 0;
                        int TotalnumberOfResults = 0;
                        foreach (string elementname in eltNameList)
                        {
                            token.ThrowIfCancellationRequested();

                            IEnumerable<AFEventFrame> eventFrames = Enumerable.Empty<AFEventFrame>();
                            string queryString = $"name:\"{eventName}\" ElementName:\"{elementname}\"";

                            using (AFEventFrameSearch eventFrameSearch =
                                 new AFEventFrameSearch(aFDatabase, "EFSearch", AFSearchMode.Inclusive,
                                                        new AFTime(starttime), new AFTime(endtime), queryString))
                            {
                                eventFrameSearch.CacheTimeout = TimeSpan.FromMinutes(5);

                                var found = eventFrameSearch.FindObjects(fullLoad: false)
                                                            .Cast<AFEventFrame>()
                                                            .ToList();

                                Dispatcher.Invoke(() =>
                                {
                                    TotalnumberOfResults= TotalnumberOfResults+found.Count;
                                    numberOfResults.Content = $"Number of results: {TotalnumberOfResults}";
                                });

                                foreach (var eventFrame in found)
                                {
                                    token.ThrowIfCancellationRequested();

                                    Dispatcher.Invoke(() =>
                                    {
                                        AddEventFrameRecursive(eventFrame, 0);
                                    });
                                }
                            }

                            processed++;

                            Dispatcher.Invoke(() =>
                            {
                                SearchProgressBar.Value = processed;
                            });
                        }
                    }
                    else
                    {
                        token.ThrowIfCancellationRequested();

                        string queryString = $"name:\"{eventName}\"";

                        using (AFEventFrameSearch eventFrameSearch =
                                new AFEventFrameSearch(aFDatabase, "EFSearch", AFSearchMode.Inclusive,
                                new AFTime(starttime), new AFTime(endtime), queryString))
                        {
                            eventFrameSearch.CacheTimeout = TimeSpan.FromMinutes(5);

                            var found = eventFrameSearch
                                .FindObjects(fullLoad: false)
                                .Cast<AFEventFrame>()
                                .ToList();

                            Dispatcher.Invoke(() =>
                            {
                                numberOfResults.Content = $"number of results: {found.Count}";
                            });

                            foreach (var eventFrame in found)
                            {
                                token.ThrowIfCancellationRequested();

                                Dispatcher.Invoke(() =>
                                {
                                    AddEventFrameRecursive(eventFrame, 0);
                                });
                            }
                        }
                    }
                }, token);
            }
            catch (OperationCanceledException)
            {
                numberOfResults.Content = "Search cancelled.";
            }
            finally
            {
                SearchProgressBar.Value = SearchProgressBar.Maximum;
                button.Content = "Search";
                button.IsEnabled = true;
                AbortButton.IsEnabled = false;
            }
        }
        public static IEnumerable<List<T>> Batch<T>(IEnumerable<T> source, int size)
        {
            List<T> bucket = new List<T>(size);

            foreach (var item in source)
            {
                bucket.Add(item);
                if (bucket.Count == size)
                {
                    yield return bucket;
                    bucket = new List<T>(size);
                }
            }

            if (bucket.Count > 0)
                yield return bucket;
        }
        private void AddEventFrameRecursive(AFEventFrame eventFrame, int level)
        {
            bool hasChildren = eventFrame.HasChildEventFrames;

            string primaryElementName = "";
            if (eventFrame.PrimaryReferencedElement != null)
                primaryElementName = eventFrame.PrimaryReferencedElement.Name ?? "";

            bool minimize = AppSettings.Options.MinimizeEFByDefault;

            var row = new EventFrameRow
            {
                Name = eventFrame.Name,
                PrimaryElement = primaryElementName,
                StartTime = eventFrame.StartTime.LocalTime,
                EndTime = eventFrame.EndTime.LocalTime,
                IsSelected = false,
                Level = level,
                HasChildren = hasChildren,
                HasChildrenVisibility = hasChildren ? Visibility.Visible : Visibility.Hidden,
                AFEventFrame = eventFrame,
                Indent = new Thickness(level * 20, 0, 0, 0),

                // Collapse parents by default if option enabled
                IsExpanded = minimize ? false : true,

                // Only show first level if minimized
                Visibility = (minimize && level > 0) ? Visibility.Collapsed : Visibility.Visible
            };

            _eventFrameRows.Add(row);

            if (hasChildren)
            {
                foreach (AFEventFrame child in eventFrame.EventFrames)
                    AddEventFrameRecursive(child, level + 1);
            }
        }

        private void ExpandCollapse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is EventFrameRow row)
            {
                row.IsExpanded = !row.IsExpanded;
                ApplyVisibility(row);
            }
        }

        private void ApplyVisibility(EventFrameRow parent)
        {
            int parentIndex = _eventFrameRows.IndexOf(parent);

            for (int i = parentIndex + 1; i < _eventFrameRows.Count; i++)
            {
                var child = _eventFrameRows[i];

                if (child.Level <= parent.Level)
                    break;

                // Show/hide based on parent
                child.Visibility = parent.IsExpanded ? Visibility.Visible : Visibility.Collapsed;

                // If parent collapsed → collapse all descendants immediately
                if (!parent.IsExpanded)
                    child.IsExpanded = false;
            }
        }
        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return $"\"{value.Replace("\"", "\"\"")}\"";

            return value;
        }
        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            if (!TimeSpan.TryParse(SampleIntervalPicker.Text, out var sampleInterval))
            {
                MessageBox.Show("Invalid interval format. Use hh:mm:ss.");
                return;
            }

            var selectedFrames = _eventFrameRows.Where(r => r.IsSelected).ToList();
            var selectedAttributes = _attributeRows.Where(a => a.IsSelected).ToList();
            var selectedEFAttributes = _efAttributeRows.Where(a => a.IsSelected).ToList();

            var items = EquipementComboBox0.Items.Cast<MultiSelectItem>().ToList();
            var selectedElements = items.Where(i => i.IsSelected).Select(i => i.Element).ToList();

            //Progression Bar
            ExportProgressPanel.Visibility = Visibility.Visible;
            ExportProgressBar.Value = 0;
            ExportProgressBar.Maximum = selectedFrames.Count;
            ExportProgressLabel.Content = "0%";

            IProgress<int> progress = new Progress<int>(value =>
            {
                ExportProgressBar.Value = value;

                if (ExportProgressBar.Maximum > 0)
                {
                    int percent = (int)(100.0 * value / ExportProgressBar.Maximum);
                    ExportProgressLabel.Content = percent + "%";
                }
            });
            // 

            if (!selectedFrames.Any())
            {
                MessageBox.Show("Please select at least one Event Frame.");
                return;
            }

            if (!selectedAttributes.Any())
            {
                MessageBox.Show("Please select at least one attribute.");
                return;
            }

            if (!selectedElements.Any())
            {
                MessageBox.Show("Please select at least one element.");
                return;
            }

            string folder = AppSettings.Options.DefaultExportFolder;


            var now = DateTime.Now.ToString("yyyyMMdd_HHmm");

            string efFilter = string.IsNullOrWhiteSpace(Eventname.Text)
                ? "AllEF"
                : Eventname.Text.Replace(" ", "_");

                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                {
                    efFilter = efFilter.Replace(c, '_');
                }

            string start = StartDatePicker.SelectedDate?.ToString("yyyyMMdd") ?? "Start";
            string end = EndDatePicker.SelectedDate?.ToString("yyyyMMdd") ?? "End";

            int elementCount = selectedElements.Count;
            string fileName = $"Export_{efFilter}_{elementCount}elts_{start}_{end}_{now}.csv";

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV file (*.csv)|*.csv",
                FileName = fileName
            };

            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                saveDialog.InitialDirectory = folder;
            }

            if (saveDialog.ShowDialog() != true)
                return;

            string filePath = saveDialog.FileName;

            try
            {
                if (button != null)
                {
                    button.Content = "Loading...";
                    button.IsEnabled = false;
                    exportExcel.IsEnabled=false;    
                }

                await Task.Run(() =>
                {
                    using (var writer = new StreamWriter(filePath))
                    {
                        /*  // ------------------------------------------------------------
                          // Build attribute/element column keys
                          // ------------------------------------------------------------

                          var columnKeys = new List<string>();
                          var attributeInstances = new Dictionary<string, AFAttribute>();

                          foreach (var element in selectedElements)
                          {
                              foreach (var attrRow in selectedAttributes)
                              {
                                  var attr = element.Attributes[attrRow.Name];

                                  if (attr == null)
                                      continue;

                                  string key = $"{attrRow.Name}_{element.Name}";

                                  columnKeys.Add(key);
                                  attributeInstances[key] = attr;
                              }
                          }*/

                        // ------------------------------------------------------------
                        // HEADER
                        // ------------------------------------------------------------

                        /*  var header = new List<string>
                  {
                      "EventFrame"
                  };

                          foreach (var efAttr in selectedEFAttributes)
                              header.Add(efAttr.Name);

                          header.Add("PrimaryElement");
                          header.Add("Timestamp");

                          foreach (var key in columnKeys)
                              header.Add(key);*/
                        var header = new List<string>()
                        {
                            "EventFrame"
                        };

                        foreach (var efAttr in selectedEFAttributes)
                            header.Add(efAttr.Name);

                        header.Add("PrimaryElement");
                        header.Add("Timestamp");

                        foreach (var attr in selectedAttributes)
                            header.Add(attr.Name);

                        writer.WriteLine(string.Join(",", header.Select(EscapeCsv)));

                        // ------------------------------------------------------------
                        // Process Event Frames
                        // ------------------------------------------------------------
                        //progressBar
                        int progressCount = 0;
                        foreach (var efRow in selectedFrames)
                        {
                            var ef = efRow.AFEventFrame;

                            string efName = efRow.Name ?? ef.Name;
                            string primaryElem = ef.PrimaryReferencedElement?.Name ?? "";

                            var range = new AFTimeRange(
                                new AFTime(efRow.StartTime),
                                new AFTime(efRow.EndTime)
                            );



                            var primaryElement = ef.PrimaryReferencedElement;

                            if (primaryElement == null)
                                continue;

                            var attributeInstances = new Dictionary<string, AFAttribute>();

                            foreach (var attrRow in selectedAttributes)
                            {
                                var attr = primaryElement.Attributes[attrRow.Name];

                                if (attr != null)
                                {
                                    attributeInstances[attrRow.Name] = attr;
                                }
                            }

                            if (!attributeInstances.Any())
                                continue;


                            // --------------------------------------------------------
                            // Master timestamps
                            // --------------------------------------------------------

                            var firstAttr = attributeInstances.Values.First();

                            AFValues masterVals;

                            try
                            {
                                masterVals = firstAttr.Data.InterpolatedValues(
                                    range,
                                    new AFTimeSpan(sampleInterval),
                                    null,
                                    null,
                                    false);
                            }
                            catch
                            {
                                continue;
                            }

                            var timestamps = masterVals
                                .Select(v => v.Timestamp.LocalTime)
                                .ToList();

                            if (timestamps.Count == 0)
                                continue;

                            int N = timestamps.Count;

                            // --------------------------------------------------------
                            // Load attribute values
                            // --------------------------------------------------------

                            var valuesByKey = new Dictionary<string, AFValues>();

                            //WORKS BUT IT IS TOO SLOW
                            foreach (var kvp in attributeInstances)
                            {
                                AFValues vals = null;

                                try
                                {
                                    vals = kvp.Value.Data.InterpolatedValues(
                                        range,
                                        new AFTimeSpan(sampleInterval),
                                        null,
                                        null,
                                        false);
                                }
                                catch
                                {
                                    vals = null;
                                }

                                valuesByKey[kvp.Key] = vals;
                            }


                            /*
                                                        var piPointAttributes = new Dictionary<string, AFAttribute>();
                                                        var formulaAttributes = new Dictionary<string, AFAttribute>();

                                                        // ----------------------------------------------------
                                                        // Separate PI Point attributes from Formula attributes
                                                        // ----------------------------------------------------

                                                        foreach (var kvp in attributeInstances)
                                                        {
                                                            var attr = kvp.Value;

                                                            if (attr.DataReference?.Name == "PI Point" && attr.GetValue().IsGood && attr.PIPoint.IsResolved)
                                                                piPointAttributes[kvp.Key] = attr;
                                                            else
                                                                formulaAttributes[kvp.Key] = attr;
                                                        }

                                                        var afInterval = new AFTimeSpan(sampleInterval);

                                                        // ----------------------------------------------------
                                                        // BULK PI TAG RETRIEVAL
                                                        // ----------------------------------------------------

                                                        if (piPointAttributes.Count > 0)
                                                        {
                                                            var piPoints = new PIPointList(
                                                                piPointAttributes.Values.Select(a => a.PIPoint)
                                                            );

                                                            IEnumerable<AFValues> bulkResults = null;

                                                            try
                                                            {
                                                                bulkResults = piPoints.InterpolatedValues(
                                                                    range,
                                                                    afInterval,
                                                                    null,
                                                                    false,
                                                                    null
                                                                );
                                                            }
                                                            catch
                                                            {
                                                                bulkResults = null;
                                                            }

                                                            if (bulkResults != null)
                                                            {
                                                                var resultList = bulkResults.ToList();
                                                                var keys = piPointAttributes.Keys.ToList();

                                                                for (int i = 0; i < resultList.Count; i++)
                                                                {
                                                                    valuesByKey[keys[i]] = resultList[i];
                                                                }
                                                            }
                                                        }

                                                        // ----------------------------------------------------
                                                        // FORMULA ATTRIBUTES
                                                        // ----------------------------------------------------

                                                        foreach (var kvp in formulaAttributes)
                                                        {
                                                            AFValues vals = null;

                                                            try
                                                            {
                                                                vals = kvp.Value.Data.InterpolatedValues(
                                                                    range,
                                                                    afInterval,
                                                                    null,
                                                                    null,
                                                                    false);
                                                            }
                                                            catch
                                                            {
                                                                vals = null;
                                                            }

                                                            valuesByKey[kvp.Key] = vals;
                                                        }*/


                            // --------------------------------------------------------
                            // EF attribute values
                            // --------------------------------------------------------

                            var staticValues = new Dictionary<string, string>();

                            foreach (var efAttr in selectedEFAttributes)
                            {
                                try
                                {
                                    var v = ef.Attributes[efAttr.Name]?.GetValue()?.Value;
                                    staticValues[efAttr.Name] = v?.ToString() ?? "";
                                }
                                catch
                                {
                                    staticValues[efAttr.Name] = "";
                                }
                            }

                            // --------------------------------------------------------
                            // Write rows
                            // --------------------------------------------------------

                            for (int i = 0; i < N; i++)
                            {
                                var row = new List<string>
                        {
                            efName
                        };

                                foreach (var efAttr in selectedEFAttributes)
                                    row.Add(staticValues[efAttr.Name]);

                                row.Add(primaryElem);

                                row.Add(timestamps[i].ToString("yyyy-MM-dd HH:mm:ss"));

                                /* foreach (var key in columnKeys)
                                 {
                                     valuesByKey.TryGetValue(key, out var series);

                                     if (series != null && i < series.Count)
                                         row.Add(series[i].Value?.ToString());
                                     else
                                         row.Add("");
                                 }*/
                                foreach (var attr in selectedAttributes)
                                {
                                    if (valuesByKey.TryGetValue(attr.Name, out var series) && i < series?.Count)
                                        row.Add(series[i].Value?.ToString());
                                    else
                                        row.Add("");
                                }

                                writer.WriteLine(string.Join(",", row.Select(EscapeCsv)));
                            }
                            //progressBar
                            progressCount++;
                            progress.Report(progressCount);
                        }

                    }
                });

                MessageBox.Show("CSV export completed successfully!", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (button != null)
                {
                    button.Content = "Export to CSV";
                    button.IsEnabled = true;
                    exportExcel.IsEnabled = true;
                }
            }
        }

        /*  private void SelectAllCheckBox_EFAttr_Click(object sender, RoutedEventArgs e)
          {
              bool newVal = (SelectAllCheckBox_EFAttr.IsChecked == true);

              foreach (var row in _efAttributeRows)
              {
                  row.IsSelected = newVal;
              }
          }*/
        private void SelectAllCheckBox_EFAttr_Click(object sender, RoutedEventArgs e)
        {
            bool newVal = (SelectAllCheckBox_EFAttr.IsChecked == true);

            foreach (var item in _efAttrCollectionView)
            {
                if (item is EFAttributeRow row)
                {
                    row.IsSelected = newVal;
                }
            }
        }
        private async void Export_Click_Excel(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            if (!TimeSpan.TryParse(SampleIntervalPicker.Text, out var sampleInterval))
            {
                MessageBox.Show("Invalid interval format. Use hh:mm:ss.");
                return;
            }

            var selectedFrames = _eventFrameRows.Where(r => r.IsSelected).ToList();
            var selectedAttributes = _attributeRows.Where(a => a.IsSelected).ToList();
            var selectedEFAttributes = _efAttributeRows.Where(a => a.IsSelected).ToList();

            var items = EquipementComboBox0.Items.Cast<MultiSelectItem>().ToList();
            var selectedElements = items.Where(i => i.IsSelected).Select(i => i.Element).ToList();


            //Progression Bar
            ExportProgressPanel.Visibility = Visibility.Visible;
            ExportProgressBar.Value = 0;
            ExportProgressBar.Maximum = selectedFrames.Count;
            ExportProgressLabel.Content = "0%";

            IProgress<int> progress = new Progress<int>(value =>
            {
                ExportProgressBar.Value = value;

                if (ExportProgressBar.Maximum > 0)
                {
                    int percent = (int)(100.0 * value / ExportProgressBar.Maximum);
                    ExportProgressLabel.Content = percent + "%";
                }
            });
            // 


            if (!selectedFrames.Any())
            {
                MessageBox.Show("Please select at least one Event Frame.");
                return;
            }

            if (!selectedAttributes.Any())
            {
                MessageBox.Show("Please select at least one attribute.");
                return;
            }

            if (!selectedElements.Any())
            {
                MessageBox.Show("Please select at least one element.");
                return;
            }

            string folder = AppSettings.Options.DefaultExportFolder;

            var now = DateTime.Now.ToString("yyyyMMdd_HHmm");

            string efFilter = string.IsNullOrWhiteSpace(Eventname.Text)
                ? "AllEF"
                : Eventname.Text.Replace(" ", "_");

            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                efFilter = efFilter.Replace(c, '_');
            }

            string start = StartDatePicker.SelectedDate?.ToString("yyyyMMdd") ?? "Start";
            string end = EndDatePicker.SelectedDate?.ToString("yyyyMMdd") ?? "End";

            int elementCount = selectedElements.Count;
            string fileName = $"Export_{efFilter}_{elementCount}elts_{start}_{end}_{now}.xlsx";

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = fileName
            };

            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                saveDialog.InitialDirectory = folder;
            }

            if (saveDialog.ShowDialog() != true)
                return;

            string filePath = saveDialog.FileName;

            try
            {
                if (button != null)
                {
                    button.Content = "Loading...";
                    button.IsEnabled = false;
                    exportCSV.IsEnabled=false;
                }

                ExcelPackage.License.SetNonCommercialPersonal("GSK");

                await Task.Run(() =>
                {
                    using (var package = new ExcelPackage())
                    {
                        var ws = package.Workbook.Worksheets.Add("Data");

                        int row = 1;
                        int col = 1;

                       /* // ------------------------------------------------------------
                        // Build attribute/element column keys
                        // ------------------------------------------------------------

                        var columnKeys = new List<string>();
                        var attributeInstances = new Dictionary<string, AFAttribute>();

                        foreach (var element in selectedElements)
                        {
                            foreach (var attrRow in selectedAttributes)
                            {
                                var attr = element.Attributes[attrRow.Name];

                                if (attr == null)
                                    continue;

                                string key = $"{attrRow.Name}_{element.Name}";

                                columnKeys.Add(key);
                                attributeInstances[key] = attr;
                            }
                        }*/

                        // ------------------------------------------------------------
                        // Header
                        // ------------------------------------------------------------

                        ws.Cells[row, col++].Value = "EventFrame";

                        foreach (var efAttr in selectedEFAttributes)
                            ws.Cells[row, col++].Value = efAttr.Name;

                        ws.Cells[row, col++].Value = "PrimaryElement";
                        ws.Cells[row, col++].Value = "Timestamp";

                        /* foreach (var key in columnKeys)
                             ws.Cells[row, col++].Value = key;*/
                        foreach (var attr in selectedAttributes)
                            ws.Cells[row, col++].Value = attr.Name;

                        using (var rng = ws.Cells[1, 1, 1, col - 1])
                        {
                            rng.Style.Font.Bold = true;
                            rng.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            rng.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(242, 242, 242));
                        }

                        row++;


                        //progressBar
                        int progressCount = 0;


                        // ------------------------------------------------------------
                        // Process Event Frames
                        // ------------------------------------------------------------

                        foreach (var efRow in selectedFrames)
                        {
                            var ef = efRow.AFEventFrame;

                            string efName = efRow.Name ?? ef.Name;
                            string primaryElem = ef.PrimaryReferencedElement?.Name ?? "";

                            var range = new AFTimeRange(
                                new AFTime(efRow.StartTime),
                                new AFTime(efRow.EndTime)
                            );


                            var primaryElement = ef.PrimaryReferencedElement;

                            if (primaryElement == null)
                                continue;

                            // Build attribute instances ONLY for this EF
                            var attributeInstances = new Dictionary<string, AFAttribute>();

                            foreach (var attrRow in selectedAttributes)
                            {
                                var attr = primaryElement.Attributes[attrRow.Name];

                                if (attr != null)
                                {
                                    attributeInstances[attrRow.Name] = attr;
                                }
                            }

                            // --------------------------------------------------------
                            // Master timestamps from first attribute
                            // --------------------------------------------------------

                            var firstAttr = attributeInstances.Values.First();

                            AFValues masterVals;

                            try
                            {
                                /* if (firstAttr.DataReference.Name == "PI Point")
                                 {
                                     masterVals = firstAttr.PIPoint.InterpolatedValues(
                                    range,
                                    new AFTimeSpan(sampleInterval),
                                    null,
                                    false
                                );
                                 }
                                 else //Formula
                                 {
                                     masterVals = firstAttr.Data.InterpolatedValues(
                                           range,
                                            new AFTimeSpan(sampleInterval),
                                            null,
                                            null,
                                            false);
                                 }*/
                                masterVals = firstAttr.Data.InterpolatedValues(
                                          range,
                                           new AFTimeSpan(sampleInterval),
                                           null,
                                           null,
                                           false);

                            }
                            catch
                            {
                                continue;
                            }

                            var timestamps = masterVals
                                .Select(v => v.Timestamp.LocalTime)
                                .ToList();

                            if (timestamps.Count == 0)
                                continue;

                            int N = timestamps.Count;

                            // --------------------------------------------------------
                            // Load attribute values
                            // --------------------------------------------------------

                            //WORKS BUT IT IS SLOW
                            var valuesByKey = new Dictionary<string, AFValues>();

                            foreach (var kvp in attributeInstances)
                            {
                                AFValues vals = null;

                                try
                                {
                                    vals = kvp.Value.Data.InterpolatedValues(
                                      range,
                                      new AFTimeSpan(sampleInterval),
                                      null,
                                      null,
                                      false);
                                }
                                catch
                                {
                                    vals = null;
                                }

                                valuesByKey[kvp.Key] = vals;
                            }
                            //



                            /*    var valuesByKey = new Dictionary<string, AFValues>();

                                var piPointAttributes = new Dictionary<string, AFAttribute>();
                                var formulaAttributes = new Dictionary<string, AFAttribute>();

                                // ----------------------------------------------------
                                // Separate PI Point attributes from Formula attributes
                                // ----------------------------------------------------

                                foreach (var kvp in attributeInstances)
                                {
                                    var attr = kvp.Value;

                                    if (attr.DataReference?.Name == "PI Point" && attr.PIPoint.IsResolved && attr.PIPoint != null)
                                        piPointAttributes[kvp.Key] = attr;
                                    else
                                        formulaAttributes[kvp.Key] = attr;
                                }

                                var afInterval = new AFTimeSpan(sampleInterval);

                                // ----------------------------------------------------
                                // BULK PI TAG RETRIEVAL
                                // ----------------------------------------------------

                                if (piPointAttributes.Count > 0)
                                {
                                    var piPoints = new PIPointList(
                                        piPointAttributes.Values.Select(a => a.PIPoint)
                                    );

                                    IEnumerable<AFValues> bulkResults = null;

                                    try
                                    {
                                        bulkResults = piPoints.InterpolatedValues(
                                            range,
                                            afInterval,
                                            null,
                                            false,
                                            null
                                        );
                                    }
                                    catch
                                    {
                                        bulkResults = null;
                                    }

                                    if (bulkResults != null)
                                    {
                                        var resultList = bulkResults.ToList();
                                        var keys = piPointAttributes.Keys.ToList();

                                        for (int i = 0; i < resultList.Count; i++)
                                        {
                                            valuesByKey[keys[i]] = resultList[i];
                                        }
                                    }
                                }

                                // ----------------------------------------------------
                                // FORMULA ATTRIBUTES
                                // ----------------------------------------------------

                                foreach (var kvp in formulaAttributes)
                                {
                                    AFValues vals = null;

                                    try
                                    {
                                        vals = kvp.Value.Data.InterpolatedValues(
                                            range,
                                            afInterval,
                                            null,
                                            null,
                                            false);
                                    }
                                    catch
                                    {
                                        vals = null;
                                    }

                                    valuesByKey[kvp.Key] = vals;
                                }*/












                            // --------------------------------------------------------
                            // EF attribute values
                            // --------------------------------------------------------

                            var staticValues = new Dictionary<string, string>();

                            foreach (var efAttr in selectedEFAttributes)
                            {
                                try
                                {
                                    var v = ef.Attributes[efAttr.Name]?.GetValue()?.Value;
                                    staticValues[efAttr.Name] = v?.ToString() ?? "";
                                }
                                catch
                                {
                                    staticValues[efAttr.Name] = "";
                                }
                            }

                            // --------------------------------------------------------
                            // Write rows
                            // --------------------------------------------------------

                            for (int i = 0; i < N; i++)
                            {
                                col = 1;

                                ws.Cells[row, col++].Value = efName;

                                foreach (var efAttr in selectedEFAttributes)
                                    ws.Cells[row, col++].Value = staticValues[efAttr.Name];

                                ws.Cells[row, col++].Value = primaryElem;

                                ws.Cells[row, col].Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";
                                ws.Cells[row, col++].Value = timestamps[i];

                                /*foreach (var key in columnKeys)
                                {
                                    valuesByKey.TryGetValue(key, out var series);

                                    if (series != null && i < series.Count)
                                        ws.Cells[row, col++].Value = series[i].Value;
                                    else
                                        ws.Cells[row, col++].Value = null;
                                }*/
                                foreach (var attr in selectedAttributes)
                                {
                                    if (valuesByKey.TryGetValue(attr.Name, out var series) && i < series?.Count)
                                        ws.Cells[row, col++].Value = series[i].Value;
                                    else
                                        ws.Cells[row, col++].Value = null;
                                }

                                row++;
                            }

                            //progressBar
                            progressCount++;
                            progress.Report(progressCount);
                        }

                        ws.Cells[ws.Dimension.Address].AutoFitColumns();

                        if (File.Exists(filePath) && IsFileLocked(filePath))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(
                                    "The file is currently open in Excel.\nPlease close it and try again.",
                                    "File In Use",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning
                                );
                            });
                            return;
                        }

                        package.SaveAs(new FileInfo(filePath));
                        if (AppSettings.Options.AutoOpenExcelAfterExport)
                        {
                            Process.Start(filePath);
                        }

                    }
                });

                MessageBox.Show("Excel export completed successfully!", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (button != null)
                {
                    button.Content = "Export to Excel";
                    button.IsEnabled = true;
                    exportCSV.IsEnabled = true; 
                }
            }
        }
       
        private bool IsFileLocked(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    // If we can open it for R/W with no sharing, file is NOT locked
                }
                return false;
            }
            catch (IOException)
            {
                return true;    // File is locked
            }
            catch
            {
                return true;
            }
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox headerCheckBox)
            {
                bool isChecked = headerCheckBox.IsChecked == true;

                foreach (var item in _collectionView)
                {
                    if (item is EventFrameRow row)
                    {
                        row.IsSelected = isChecked;
                    }
                }
            }
        }
        private void BatchFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {

            string filterText = BatchFilterBox.Text?.ToLower() ?? string.Empty;

            IncludeParentsCheckBox.IsEnabled = filterText.Length > 0;

            _collectionView.Filter = obj =>
            {
                if (obj is EventFrameRow row)
                {
                    return string.IsNullOrEmpty(filterText)
                           || row.PrimaryElement?.ToLower().Contains(filterText) == true
                           || row.Name?.ToLower().Contains(filterText) == true;
                }
                return false;
            };

            _collectionView.Refresh();

            
            IncludeParentsCheckBox_Changed(null, null);

        }

        private void IncludeParentsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // keep same filter text logic as your working method
            string filterText = BatchFilterBox.Text?.ToLower() ?? string.Empty;

            // If no filter text, just keep original behavior (show all)
            if (string.IsNullOrEmpty(filterText))
            {
                // restore simple filter that shows everything (same as your original)
                _collectionView.Filter = obj =>
                {
                    if (obj is EventFrameRow row)
                    {
                        return true; // no filter -> show all
                    }
                    return false;
                };

                _collectionView.Refresh();
                numberOfResults.Content = "";
                return;
            }

            // Build the list of matched rows (same rules as your working code)
            var matches = _eventFrameRows
                .Where(r => (r.PrimaryElement?.ToLower().Contains(filterText) == true)
                         || (r.Name?.ToLower().Contains(filterText) == true))
                .ToList();

            // If checkbox is NOT checked -> use the simple matching filter (no parents)
            if (IncludeParentsCheckBox.IsChecked != true)
            {
                _collectionView.Filter = obj =>
                {
                    if (obj is EventFrameRow row)
                    {
                        // same match test as above to avoid duplication of logic
                        return (row.PrimaryElement?.ToLower().Contains(filterText) == true)
                               || (row.Name?.ToLower().Contains(filterText) == true);
                    }
                    return false;
                };

                numberOfResults.Content = $"{matches.Count} results";
                _collectionView.Refresh();
                return;
            }

            // Checkbox IS checked -> compute matches + parents (single parent chain)
            var rowsToShow = new HashSet<EventFrameRow>(matches);

            foreach (var match in matches)
            {
                int idx = _eventFrameRows.IndexOf(match);
                if (idx <= 0) continue;

                int level = match.Level;

                // walk upwards to find parents: the "closest above with Level == level - 1"
                for (int i = idx - 1; i >= 0; i--)
                {
                    var candidate = _eventFrameRows[i];

                    if (candidate.Level == level - 1)
                    {
                        rowsToShow.Add(candidate);
                        level--; // now look for the parent of this candidate in the next iterations
                        if (level <= 0) break; // reached root
                    }
                    else if (candidate.Level < level - 1)
                    {
                        // jumped above the immediate parent level -> stop searching for this match
                        break;
                    }
                    // else candidate.Level >= level means it's a sibling/descendant — keep scanning upward
                }
            }

            // Set the collection view filter to show only rows in rowsToShow
            _collectionView.Filter = obj =>
            {
                if (obj is EventFrameRow row)
                {
                    return rowsToShow.Contains(row);
                }
                return false;
            };

            numberOfResults.Content = $"{matches.Count} results (+ parents)";
            _collectionView.Refresh();
        }


        private void EFDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Add attributes for newly checked rows
            foreach (var item in e.AddedItems)
            {
                if (item is EventFrameRow row && row.IsSelected)
                {
                    AFEventFrame ef = row.AFEventFrame;
                    if (ef == null) continue;

                    foreach (AFAttribute attr in ef.Attributes)
                    {
                        // Avoid duplicates
                        if (!_efAttributeRows.Any(a => a.EFName == ef.Name && a.Name == attr.Name))
                        {
                            _efAttributeRows.Add(new EFAttributeRow
                            {
                                Name = attr.Name,
                                EFName = ef.Name,
                                Category = string.Join(", ", attr.Categories.Select(c => c.Name)),
                                IsSelected = false,
                                AFEventFrame= ef,
                            });
                        }
                    }
                }
            }

            // Remove attributes for unchecked rows
            foreach (var item in e.RemovedItems)
            {
                if (item is EventFrameRow row && !row.IsSelected)
                {
                    AFEventFrame ef = row.AFEventFrame;
                    if (ef == null) continue;

                    var toRemove = _efAttributeRows
                        .Where(a => a.EFName == ef.Name)
                        .ToList();  // Copy so we can remove safely

                    foreach (var attr in toRemove)
                        _efAttributeRows.Remove(attr);
                }
            }

            _efAttrCollectionView = CollectionViewSource.GetDefaultView(_efAttributeRows);
            EFAttrDataGrid.ItemsSource = _efAttrCollectionView;
        }
        private AFElementTemplate GetCommonEFTemplate(List<AFElementTemplate> templates)
        {
            if (templates == null || templates.Count == 0)
                return null;

            var chains = templates
                .Select(t => GetEFTemplateHierarchy(t))
                .ToList();

            var common = chains
                .Aggregate((prev, next) => prev.Intersect(next).ToList());

            return common.LastOrDefault();
        }

        private List<AFElementTemplate> GetEFTemplateHierarchy(AFElementTemplate template)
        {
            var chain = new List<AFElementTemplate>();

            while (template != null)
            {
                chain.Add(template);
                template = template.BaseTemplate;
            }

            chain.Reverse();

            return chain;
        }
       
        private void UpdateEFAttributeGrid()
        {
            _efAttributeRows.Clear();

            var selectedFrames = _eventFrameRows
                .Where(r => r.IsSelected)
                .Select(r => r.AFEventFrame)
                .Where(ef => ef?.Template != null)
                .ToList();

            if (!selectedFrames.Any())
            {
                SelectedEFTemplateLabel.Content = "-";
                _efAttrCollectionView = CollectionViewSource.GetDefaultView(_efAttributeRows);
                EFAttrDataGrid.ItemsSource = _efAttrCollectionView;
                RefreshEFAttrGrid();
                return;
            }

            // Get templates from selected Event Frames
            var templates = selectedFrames
                .Select(ef => ef.Template)
                .Distinct()
                .ToList();

            // Compute common template
            var commonTemplate = GetCommonEFTemplate(templates);

            if (commonTemplate == null)
            {
                SelectedEFTemplateLabel.Content = "-";
                _efAttrCollectionView = CollectionViewSource.GetDefaultView(_efAttributeRows);
                EFAttrDataGrid.ItemsSource = _efAttrCollectionView;
                RefreshEFAttrGrid();
                return;
            }

            // Update label showing which template is used
            SelectedEFTemplateLabel.Content = commonTemplate.Name;

            // Get hierarchy of that template
            var templateChain = GetEFTemplateHierarchy(commonTemplate);

            foreach (var tpl in templateChain)
            {
                foreach (var attr in tpl.AttributeTemplates)
                {
                    if (attr.IsHidden)
                        continue;

                    _efAttributeRows.Add(new EFAttributeRow
                    {
                        Name = attr.Name,
                        Category = string.Join(", ", attr.Categories.Select(c => c.Name)),
                        IsSelected = false
                    });
                }
            }

            // Setup CollectionView for filtering
            _efAttrCollectionView = CollectionViewSource.GetDefaultView(_efAttributeRows);

            EFAttrDataGrid.ItemsSource = _efAttrCollectionView;

            RefreshEFAttrGrid();
        }
        private void EFAttrFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_efAttrCollectionView == null)
                return;

            string filter = EFAttrFilterBox.Text?.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(filter))
            {
                _efAttrCollectionView.Filter = null;
            }
            else
            {
                _efAttrCollectionView.Filter = obj =>
                {
                    var attr = obj as EFAttributeRow;

                    if (attr == null)
                        return false;

                    return (attr.Name?.ToLower().Contains(filter) ?? false)
                        || (attr.Category?.ToLower().Contains(filter) ?? false);
                };
            }

            _efAttrCollectionView.Refresh();
        }
        private void EFCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateEFAttributeGrid();
        }

        private void EFCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateEFAttributeGrid();
        }

        private void RefreshEFAttrGrid()
        {
            _efAttrCollectionView = CollectionViewSource.GetDefaultView(_efAttributeRows);
            EFAttrDataGrid.ItemsSource = _efAttrCollectionView;
        }

        private TextBox GetComboBoxTextBox(ComboBox combo)
        {
            return combo.Template.FindName("PART_EditableTextBox", combo) as TextBox;
        }

        private void getAttributes(List<AFElement> attrList)
        {
            _attributeRows.Clear();
            if (attrList!=null)
            {
                foreach (AFElement selectedItem in attrList)
                {

                    if (selectedItem == null) return;

                    foreach (AFAttribute attr in selectedItem.Attributes)
                    {
                        if (attr.DataReference != null
                             && attr.DataReference.Name == "PI Point"
                             && !attr.ConfigString.ToLower().Contains("%@")
                             && !string.IsNullOrEmpty(attr.ConfigString)
                             && attr.ConfigString[0] != '|')
                        {

                            string tagName = attr.ConfigString.Split('\\').LastOrDefault()?.Split('?')[0];
                            string category = string.Join(", ", attr.Categories.Select(c => c.Name));

                            _attributeRows.Add(new AttributeRow
                            {
                                Name = attr.Name,
                                ElementName = attr.Element.Name,
                                TagName = tagName,
                                Category = category,
                                IsSelected = false
                            });
                        }
                    }
                }

                _attrCollectionView = CollectionViewSource.GetDefaultView(_attributeRows);
                EltAttrDataGrid.ItemsSource = _attrCollectionView;
            }
        }
        private void getAttributesTemplate(AFElementTemplate aFElementTemplate)
        {
            _attributeRows.Clear();

            if (aFElementTemplate != null)
            {
                // Get ancestor chain for the first template
                List<AFElementTemplate> commonChain = GetTemplateHierarchy(aFElementTemplate);

                foreach (AFElementTemplate template in commonChain)
                {
                    // Get all attributes from all af templates in the commonChain
                    foreach (AFAttributeTemplate attr in template.AttributeTemplates)
                    {
                        if (attr.DataReference != null
                                && (attr.DataReference.Name == "PI Point" || attr.DataReference.Name == "Formula")
                                && !attr.IsHidden)
                        {

                            string ConfigString = attr.ConfigString.Split('\\').LastOrDefault()?.Split('?')[0];
                            string category = string.Join(", ", attr.Categories.Select(c => c.Name));

                            _attributeRows.Add(new AttributeRow
                            {
                                Name = attr.Name,
                                DataReference = attr.DataReference.Name,
                                ConfigString = ConfigString,
                                Category = category,
                                IsSelected = false
                            });
                        }
                    }
                }
            }
            Dispatcher.Invoke(() =>
            {
                _attrCollectionView = CollectionViewSource.GetDefaultView(_attributeRows);
                EltAttrDataGrid.ItemsSource = _attrCollectionView;
                _attrCollectionView.Refresh();
            });
        }

        private void AttrFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filterText = AttrFilterBox.Text.ToLower();

            if (_attrCollectionView == null) return;

            _attrCollectionView.Filter = obj =>
            {
                if (obj is AttributeRow row)
                {
                    return string.IsNullOrEmpty(filterText)
                           || row.Name.ToLower().Contains(filterText)
                           || row.DataReference.ToLower().Contains(filterText)
                           || row.ConfigString.ToLower().Contains(filterText)
                           || row.Category.ToLower().Contains(filterText);
                }
                return false;
            };

            _attrCollectionView.Refresh();
        }

        // Select All toggle
        /*        private void SelectAllCheckBox_attr_Click(object sender, RoutedEventArgs e)
                {
                    bool newVal = (SelectAllCheckBox.IsChecked == true);

                    foreach (var row in _attributeRows)
                    {
                        row.IsSelected = newVal;
                    }
                }*/
        private void SelectAllCheckBox_attr_Click(object sender, RoutedEventArgs e)
        {
            bool newVal = (SelectAllCheckBox.IsChecked == true);

            foreach (var item in _attrCollectionView)
            {
                if (item is AttributeRow row )
                {
                    row.IsSelected = newVal;
                }
            }
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        public void Options_Click(object sender, RoutedEventArgs e)
        {
            var optionsWindow = new OptionsWindow()
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }; 
           optionsWindow.ShowDialog();

          
        }
        private void About_Click(object sender, RoutedEventArgs e)
        {
            var about = new AboutWindow()
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }; 
            about.ShowDialog();
        }
        private void UserGuide_Click(object sender, RoutedEventArgs e)
        {
            string fileName = "user_manual_pi_data_export_tool.md";

            string filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

            try
            {
                if (File.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true // Required to open via system default app
                    });
                }
                else
                {
                    MessageBox.Show("Manual not found at: " + filePath);
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("No application found to open Markdown files.");
            }
        }
       
    }



}
