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

namespace pidataexport
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /*        private ObservableCollection<EventFrameRow> _eventFrameRows;*/
        private ObservableCollection<EventFrameRow> _eventFrameRows = new ObservableCollection<EventFrameRow>();

        private ICollectionView _collectionView;

        private ObservableCollection<AttributeRow> _attributeRows = new ObservableCollection<AttributeRow>();
        private ICollectionView _attrCollectionView;

        private readonly ObservableCollection<EFAttributeRow> _efAttributeRows = new ObservableCollection<EFAttributeRow>();
        private ICollectionView _efAttrCollectionView;


        private PIServer _piServer;
        public PISystem _piSystem;
        public MainWindow()
        {
            InitializeComponent();
            
            AFDatabaseComboBox.Items.Clear();

            /*if (LoadRememberedSettings())
            {
                ConnectToPI();
                LoadAFDatabases();             
            };*/


            _eventFrameRows = new ObservableCollection<EventFrameRow>();
            _collectionView = CollectionViewSource.GetDefaultView(_eventFrameRows);
            /*EFDataGrid.ItemsSource = _collectionView;*/

            EFDataGrid.ItemsSource = _eventFrameRows;


            this.DataContext = this;
        }
        private bool LoadRememberedSettings()
        {
            try
            {
                string folder = GetConfigFolder();
                string file = System.IO.Path.Combine(folder, "connection_settings.json");

                if (!File.Exists(file))
                    return false;

                // Read + parse JSON
                var json = File.ReadAllText(file);
                var settings = JsonConvert.DeserializeObject<ConnectionSettings>(json);

                if (settings == null)
                    return false;

                // PI Server lookup
                _piServer = PIServer.FindPIServer(settings.PIServer);

                // AF Server lookup
                var piSystems = new PISystems();
                _piSystem = piSystems[settings.AFServer];
            
                return _piSystem != null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading connection_settings.json: {ex.Message}");
                return false;
            }
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

        private void OpenConnectionHelper_Click(object sender, RoutedEventArgs e)
        {
            var win = new ConnectionHelperWindow 
            { 
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            win.Owner = this;
            win.ShowDialog();   // modal, use .Show() if you want non-modal
        }

        /* private void ConnectToPI()
         {
             try
             {
                 _piSystem = new PISystems().DefaultPISystem;
                 _piSystem.Connect();


                 _piServer = new PIServers().DefaultPIServer;
                 _piServer.Connect();

                 AFServer.Content = _piSystem.Name;
                 PIDAServer.Content = _piServer.Name;
             }
             catch (Exception ex)
             {
                 MessageBox.Show($"Error connecting to PI system: {ex.Message}");
             }
         }*/
        private async void ConnectToPI()
        {
            var win = new LoadingBox
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            try
            {
                // Prepare UI BEFORE background work
                win.ConnectionStatusText.Text = "Connecting to AF Server...";
                win.Show();


                await Task.Run(() => _piSystem.Connect());
               

                win.ConnectionStatusText.Text = "Connecting to PI DA Server...";
                

                await Task.Run(() => _piServer.Connect());
                win.Close();

                AFServer.Content = _piSystem.Name;
                PIDAServer.Content = _piServer.Name;

                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to PI system: {ex.Message}");
            }
            finally
            {
                win.Close();
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

                    // Clear old items before adding new ones
                   /* EquipementComboBox.Items.Clear();*/
                    EquipementComboBox0.Items.Clear();

                    if (aFElements.Count > 0)
                    {
                        foreach (AFElement aFElement in aFElements)
                        {
                          /*  EquipementComboBox.Items.Add(aFElement);*/
                            EquipementComboBox0.Items.Add(aFElement);
                        }
                        
                    }
                    // Ensure template is applied so PART_EditableTextBox exists (if editable)
                    EquipementComboBox0.ApplyTemplate();
                    var textBox = GetComboBoxTextBox(EquipementComboBox0); // your helper that does ApplyTemplate+FindName

                    // Defer clearing so ComboBox internal logic finishes first
                    EquipementComboBox0.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // clear displayed text
                        if (textBox != null)
                        {
                            textBox.Text = string.Empty;
                            textBox.CaretIndex = 0;
                        }

                        EquipementComboBox0.Text = string.Empty;

                        // clear selection to avoid the control resetting the text later
                        EquipementComboBox0.SelectedItem = null;

                        // ensure dropdown is closed
                        EquipementComboBox0.IsDropDownOpen = false;

                        // optional: clear your selection list(s) if you want a full reset
                        // afElementList.Clear();
                        // itemsList.Clear();
                        _attributeRows.Clear();

                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }
     /*   private CancellationTokenSource _cts;*/
        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            _efAttributeRows.Clear();
            var button = sender as Button;

            string eventName = $"{Eventname.Text}";
            string eltname = EquipementComboBox0.Text; 
            /*string batchID = BatchID.Text.ToLower();*/

            AFDatabase aFDatabase = AFDatabaseComboBox.SelectedItem as AFDatabase;


            List<string> eltNameList = eltname
                                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .ToList();
           
            try
            {
                button.Content = "Loading...";
                button.IsEnabled = false;
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);


                var starttime = StartDatePicker.SelectedDate;
                var endtime = EndDatePicker.SelectedDate;

                // -----------------------------
                //    VALIDATE DATE RANGE
                // -----------------------------
                if (!starttime.HasValue || !endtime.HasValue)
                {
                    MessageBox.Show("Please select both a start date and an end date.",
                                     "Missing Dates",
                                     MessageBoxButton.OK,
                                     MessageBoxImage.Warning);
                    return;   // Exit early ⇒ finally block restores the button UI
                }

                if (starttime > endtime)
                {
                    MessageBox.Show("The start date cannot be later than the end date.",
                                     "Invalid Date Range",
                                     MessageBoxButton.OK,
                                     MessageBoxImage.Warning);
                    return;   // Exit early
                }


                _eventFrameRows.Clear();
                int count = 0;
                if (eltNameList.Count > 0)
                {
                    foreach (string elementname in eltNameList)
                    {
                        /*------------Method 2 (prefered)------------*/
                        IEnumerable<AFEventFrame> eventFrames = Enumerable.Empty<AFEventFrame>();
                        string queryString = $"name:\"{eventName}\" ElementName:\"{elementname}\"";
                        using (AFEventFrameSearch eventFrameSearch =
                             new AFEventFrameSearch(aFDatabase, "EFSearch", AFSearchMode.Inclusive, new AFTime(starttime), new AFTime(endtime), queryString))
                        {
                            eventFrameSearch.CacheTimeout = TimeSpan.FromMinutes(5);

                            // Materialize result set once
                            var found = eventFrameSearch.FindObjects(fullLoad: false).Cast<AFEventFrame>().ToList();

                            numberOfResults.Content = $"number of results: {found.Count}";

                            foreach (var eventFrame in found)
                            {
                                // Recursively add event frame + all its hierarchy
                                AddEventFrameRecursive(eventFrame, 0);
                            }
                        }
                    }
                }
                else
                {
                    /*------------Method 2 (prefered)------------*/
                    IEnumerable<AFEventFrame> eventFrames = Enumerable.Empty<AFEventFrame>();
                    string queryString = $"name:\"{eventName}\"";
                    using (AFEventFrameSearch eventFrameSearch =
                            new AFEventFrameSearch(aFDatabase, "EFSearch", AFSearchMode.Inclusive, new AFTime(starttime), new AFTime(endtime), queryString))
                    {
                        eventFrameSearch.CacheTimeout = TimeSpan.FromMinutes(5);

                        // Materialize result set once
                        var found = eventFrameSearch.FindObjects(fullLoad: false).Cast<AFEventFrame>().ToList();

                        numberOfResults.Content = $"number of results: {found.Count}";

                        foreach (var eventFrame in found)
                        {
                            // Recursively add event frame + all its hierarchy
                            AddEventFrameRecursive(eventFrame, 0);
                        }
                    }

                }



                /*------------------------*/
                /*                numberOfResults.Content = "Results exceed the search limit (1000)";*/

                /* _eventFrameRows.Clear();*/

                /* int count = 0;*/

                /*   await Task.Run(async () =>
                   {
                       foreach (var eventFrame in eventFrames)
                       {
                           AFAttribute batchAttr = null;
                           foreach (AFAttribute a in eventFrame.Attributes)
                           {
                               if (a.Name.IndexOf("batchid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   a.Name.IndexOf("batch id", StringComparison.OrdinalIgnoreCase) >= 0)
                               {
                                   batchAttr = a;
                                   break;
                               }
                           }

                           if (batchAttr != null)
                           {
                               string attVal = batchAttr.GetValue()?.ToString();

                               if (string.IsNullOrEmpty(batchID) || attVal.ToLower().Contains(batchID))
                               {
                                   count++;
                                   // marshal back to UI thread
                                   await Dispatcher.InvokeAsync(() =>
                                   {
                                       _eventFrameRows.Add(new EventFrameRow
                                       {
                                           BatchId = attVal,
                                           Name = eventFrame.Name,
                                           StartTime = eventFrame.StartTime,
                                           EndTime = eventFrame.EndTime,
                                           IsSelected = false
                                       });
                                       
                                           if (eventFrames.ToList().Count == 1000)
                                               numberOfResults.Content = "Results exceed the search limit (1000)";
                                           else
                                               numberOfResults.Content = $"number of results: {count}"; ;
                                   });
                               }
                           }
                       }

                   });*/

            }
            catch (OperationCanceledException)
            {
                numberOfResults.Content = "Search cancelled.";
            }
            finally
            {
                button.Content = "Search";
                button.IsEnabled = true;
            }
        }
        private void AddEventFrameRecursive(AFEventFrame eventFrame, int level)
        {
            bool hasChildren = eventFrame.HasChildEventFrames;

            // Safe Primary Element (avoid null reference)
            string primaryElementName = "";
            if (eventFrame.PrimaryReferencedElement != null)
                primaryElementName = eventFrame.PrimaryReferencedElement.Name ?? "";

            var row = new EventFrameRow
            {
                Name = eventFrame.Name,
                PrimaryElement = primaryElementName,  // ← safe assignment
                StartTime = eventFrame.StartTime.LocalTime,
                EndTime = eventFrame.EndTime.LocalTime,
                IsSelected = false,
                Level = level,
                HasChildren = hasChildren,
                HasChildrenVisibility = hasChildren ? Visibility.Visible : Visibility.Hidden,
                AFEventFrame = eventFrame,
                Indent = new Thickness(level * 20, 0, 0, 0)
            };

            _eventFrameRows.Add(row);

            // Children
            if (hasChildren)
            {
                foreach (AFEventFrame child in eventFrame.EventFrames)
                    AddEventFrameRecursive(child, level + 1);
            }
        }



        /*        private void AbortSearch_Click(object sender, RoutedEventArgs e)
                {
                    _cts?.Cancel();
                }*/
        /*  private void AddEventFrameRecursive(AFEventFrame eventFrame, string batchId)
          {
              // Add the current event frame
              _eventFrameRows.Add(new EventFrameRow
              {
                  BatchId = batchId,
                  Name = eventFrame.Name,
                  StartTime = eventFrame.StartTime,
                  EndTime = eventFrame.EndTime,
                  IsSelected = false
              });

              // If it has children, recurse into them
              if (eventFrame.HasChildEventFrames)
              {
                  foreach (AFEventFrame child in eventFrame.EventFrames)
                  {
                      AddEventFrameRecursive(child, "child");
                  }
              }
          }*/
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

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            // --- Validate sample interval ---
            if (!TimeSpan.TryParse(SampleIntervalPicker.Text, out var sampleInterval))
            {
                MessageBox.Show("Invalid interval format. Use hh:mm:ss.");
                return;
            }

            // --- Selected rows ---
            var selectedFrames = _eventFrameRows.Where(r => r.IsSelected).ToList();
            var selectedAttributes = _attributeRows.Where(a => a.IsSelected).ToList();
            var selectedEFAttributes = _efAttributeRows.Where(a => a.IsSelected).ToList();

            if (!selectedFrames.Any())
            {
                MessageBox.Show("Please select at least one Event Frame.");
                return;
            }
            if (!selectedAttributes.Any())
            {
                MessageBox.Show("Please select at least one tag attribute.");
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = "Export.csv"
            };
            if (saveDialog.ShowDialog() != true) return;

            string filePath = saveDialog.FileName;
            try
            {
                if (button != null)
                {
                    button.Content = "Loading...";
                    button.IsEnabled = false;
                }
                await Task.Run(() =>
                {
                    // ============================================================
                    // PREVENT CRASH IF FILE IS OPEN (locked by Excel or another app)
                    // ============================================================

                    if (File.Exists(filePath) && IsFileLocked(filePath))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                "The selected file is currently open.\nPlease close it and try again.",
                                "File In Use",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning
                            );
                        });
                        return; // abort cleanly
                    }

                    using (var writer = new StreamWriter(filePath, false, Encoding.UTF8, 65536))
                    {

                        // ============================================================
                        // 1. BUILD HEADER
                        // ============================================================

                        var header = new List<string>();

                        header.Add("EventFrame");                     // Column A
                        foreach (var efAttr in selectedEFAttributes)  // Columns B..?
                            header.Add(efAttr.Name);

                        header.Add("PrimaryElement");                 // Next column
                        header.Add("Timestamp");                      // Next column

                        // Then one column per selected tag attribute
                        foreach (var attr in selectedAttributes)
                            header.Add(attr.Name);

                        writer.WriteLine(string.Join(",", header));

                        // ============================================================
                        // 2. PREPARE PIPOINT CACHE (avoid repeated lookups)
                        // ============================================================

                        var invalidAttributes = new List<string>();

                        var piPointCache = selectedAttributes
                            .GroupBy(a => a.TagName)
                            .ToDictionary(
                                g => g.Key,
                                g =>
                                {
                                    try
                                    {
                                        // Attempt to resolve the tag
                                        return PIPoint.FindPIPoint(_piServer, g.Key);
                                    }
                                    catch
                                    {
                                        // Tag is invalid — add to invalid list and return null
                                        invalidAttributes.Add(g.Key);
                                        return null;
                                    }
                                }
                            );

                        // Remove invalid (null) entries from the dictionary
                        piPointCache = piPointCache
                            .Where(kvp => kvp.Value != null)
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                        // Show popup if needed
                        if (invalidAttributes.Any())
                        {
                            MessageBox.Show(
                                "The following attribute tagnames could not be resolved:\n\n" +
                                string.Join("\n", invalidAttributes),
                                "Invalid PI Points",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning
                            );
                        }


                        // ============================================================
                        // 3. EXPORT EACH EVENT FRAME
                        // ============================================================

                        var sb = new StringBuilder(1 << 20);
                        const int FLUSH_EVERY = 50000;
                        int written = 0;

                        foreach (var efRow in selectedFrames)
                        {
                            var ef = efRow.AFEventFrame;
                            string efName = efRow.Name ?? ef.Name;
                            string primaryElem = efRow.PrimaryElement ?? "";

                            // --- EF Time Range ---
                            var range = new AFTimeRange(
                                new AFTime(efRow.StartTime),
                                new AFTime(efRow.EndTime)
                            );

                            // ========================================================
                            // 3.a MASTER TIMESTAMP SERIES (using first attribute)
                            // ========================================================

                            var firstAttr = selectedAttributes[0];
                            var firstPt = piPointCache[firstAttr.TagName];

                            AFValues masterVals;
                            try
                            {
                                masterVals = firstPt.InterpolatedValues(
                                    range,
                                    new AFTimeSpan(sampleInterval),
                                    null,
                                    false
                                );
                            }
                            catch
                            {
                                continue; // skip this EF
                            }

                            var timestamps = masterVals
                                .Select(v => v.Timestamp.LocalTime)
                                .ToList();

                            if (timestamps.Count == 0)
                                continue;

                            int N = timestamps.Count;

                            // ========================================================
                            // 3.b FETCH VALUES FOR ALL ATTRIBUTES (EACH CALLED ONCE)
                            // ========================================================

                            /* var valuesByAttr = new Dictionary<string, AFValues>(
                                 StringComparer.OrdinalIgnoreCase
                             );

                             foreach (var attr in selectedAttributes)
                             {
                                 AFValues vals = null;

                                 if (piPointCache.TryGetValue(attr.TagName, out PIPoint pt))
                                 {
                                     try
                                     {
                                         vals = pt.InterpolatedValues(
                                             range,
                                             new AFTimeSpan(sampleInterval),
                                             null,
                                             false
                                         );
                                     }
                                     catch
                                     {
                                         vals = null;
                                     }
                                 }

                                 valuesByAttr[attr.Name] = vals;
                             }*/
                            // NEW: handle duplicate attribute names
                            // Step 1: count duplicates
                            var attrNameCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            foreach (var attr in selectedAttributes)
                            {
                                if (!attrNameCount.ContainsKey(attr.Name))
                                    attrNameCount[attr.Name] = 0;
                                attrNameCount[attr.Name]++;
                            }

                            // Step 2: build unique export names
                            var exportNames = new Dictionary<AttributeRow, string>();
                            foreach (var attr in selectedAttributes)
                            {
                                if (attrNameCount[attr.Name] > 1)
                                    exportNames[attr] = $"{attr.Name}_{attr.ElementName}";
                                else
                                    exportNames[attr] = attr.Name;
                            }

                            // Step 3: fetch PI values using unique keys
                            var valuesByUniqueName = new Dictionary<string, AFValues>(StringComparer.OrdinalIgnoreCase);

                            foreach (var attr in selectedAttributes)
                            {
                                string key = exportNames[attr];
                                AFValues vals = null;

                                if (piPointCache.TryGetValue(attr.TagName, out PIPoint pt))
                                {
                                    try
                                    {
                                        vals = pt.InterpolatedValues(
                                            range,
                                            new AFTimeSpan(sampleInterval),
                                            null,
                                            false
                                        );
                                    }
                                    catch
                                    {
                                        vals = null;
                                    }
                                }

                                valuesByUniqueName[key] = vals;
                            }


                            // ========================================================
                            // 3.c GET STATIC EF ATTRIBUTE VALUES (ONE-TIME)
                            // ========================================================

                            var staticValues = new Dictionary<string, string>();

                            foreach (var efAttr in selectedEFAttributes)
                            {
                                try
                                {
                                    var val = ef.Attributes[efAttr.Name]?.GetValue()?.Value;
                                    staticValues[efAttr.Name] = val?.ToString() ?? "";
                                }
                                catch
                                {
                                    staticValues[efAttr.Name] = "";
                                }
                            }

                            // ========================================================
                            // 3.d WRITE DATA ROWS
                            // ========================================================

                            for (int i = 0; i < N; i++)
                            {
                                var row = new List<string>();

                                // EventFrame name
                                row.Add(CsvSafe(efName));

                                // Static EF attributes
                                foreach (var efAttr in selectedEFAttributes)
                                    row.Add(CsvSafe(staticValues[efAttr.Name]));

                                // Primary element
                                row.Add(CsvSafe(primaryElem));

                                // Timestamp
                                row.Add(timestamps[i].ToString("yyyy-MM-dd HH:mm:ss"));

                                // Time-series values
                                /*                                foreach (var attr in selectedAttributes)
                                                                {
                                                                    var vals = valuesByAttr[attr.Name];
                                                                    if (vals != null && i < vals.Count)
                                                                        row.Add(CsvSafe(vals[i].Value?.ToString() ?? ""));
                                                                    else
                                                                        row.Add("");
                                                                }*/
                                // NEW
                                foreach (var attr in selectedAttributes)
                                {
                                    string key = exportNames[attr];
                                    var vals = valuesByUniqueName[key];

                                    if (vals != null && i < vals.Count)
                                        row.Add(CsvSafe(vals[i].Value?.ToString() ?? ""));
                                    else
                                        row.Add("");
                                }


                                sb.AppendLine(string.Join(",", row));

                                if (++written % FLUSH_EVERY == 0)
                                {
                                    writer.Write(sb.ToString());
                                    sb.Clear();
                                }
                            }
                        }

                        // Flush remainder
                        if (sb.Length > 0)
                            writer.Write(sb.ToString());
                    }
                });

                MessageBox.Show("Export completed successfully!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Restore button state
                if (button != null)
                {
                    button.Content = "Export to csv";
                    button.IsEnabled = true;
                }
            }
        }


        // Escapes commas and quotes if needed
        private string CsvSafe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(",") || s.Contains("\""))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }



        private void SelectAllCheckBox_EFAttr_Click(object sender, RoutedEventArgs e)
        {
            bool newVal = (SelectAllCheckBox_EFAttr.IsChecked == true);

            foreach (var row in _efAttributeRows)
            {
                row.IsSelected = newVal;
            }
        }

        private async void Export_Click_Excel(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            // ============================================================
            // 0. Validate sampling interval
            // ============================================================
            if (!TimeSpan.TryParse(SampleIntervalPicker.Text, out var sampleInterval))
            {
                MessageBox.Show("Invalid interval format. Use hh:mm:ss.");
                return;
            }

            var selectedFrames = _eventFrameRows.Where(r => r.IsSelected).ToList();
            var selectedAttributes = _attributeRows.Where(a => a.IsSelected).ToList();
            var selectedEFAttributes = _efAttributeRows.Where(a => a.IsSelected).ToList();

            if (!selectedFrames.Any())
            {
                MessageBox.Show("Please select at least one Event Frame.");
                return;
            }
            if (!selectedAttributes.Any())
            {
                MessageBox.Show("Please select at least one tag attribute.");
                return;
            }

            // ============================================================
            // 1. Ask user for XLSX output file
            // ============================================================
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "Export.xlsx"
            };
            if (saveDialog.ShowDialog() != true) return;

            string filePath = saveDialog.FileName;

            try
            {
                if (button != null)
                {
                    button.Content = "Loading...";
                    button.IsEnabled = false;
                }

                ExcelPackage.License.SetNonCommercialPersonal("GSK");

                await Task.Run(() =>
                {
                    using (var package = new ExcelPackage())
                    {
                        var ws = package.Workbook.Worksheets.Add("Data");

                        int row = 1;
                        int col = 1;

                        // ============================================================
                        // (A) BUILD UNIQUE COLUMN KEYS FOR ATTRIBUTES
                        // ============================================================
                        var nameGroups = selectedAttributes
                            .GroupBy(a => a.Name)
                            .ToDictionary(g => g.Key, g => g.Count());

                        var attributeKeyMap = new Dictionary<object, string>();

                        foreach (var attr in selectedAttributes)
                        {
                            string key = attr.Name ?? attr.TagName ?? "Attribute";

                            if (nameGroups.TryGetValue(attr.Name, out int cnt) && cnt > 1)
                            {
                                string elementName;
                                try { elementName = attr.ElementName; }
                                catch { elementName = null; }

                                if (string.IsNullOrWhiteSpace(elementName))
                                    elementName = attr.TagName ?? "unknown";

                                elementName = elementName.Replace(",", "_").Replace(" ", "_");

                                key = $"{attr.Name}_{elementName}";
                            }

                            // Prevent extremely rare collisions
                            string baseKey = key;
                            int suffix = 1;
                            while (attributeKeyMap.Values.Contains(key, StringComparer.OrdinalIgnoreCase))
                                key = $"{baseKey}_{suffix++}";

                            attributeKeyMap[attr] = key;
                        }

                        // ============================================================
                        // 2. HEADER ROW
                        // ============================================================
                        ws.Cells[row, col++].Value = "EventFrame";

                        foreach (var efAttr in selectedEFAttributes)
                            ws.Cells[row, col++].Value = efAttr.Name;

                        ws.Cells[row, col++].Value = "PrimaryElement";
                        ws.Cells[row, col++].Value = "Timestamp";

                        foreach (var attr in selectedAttributes)
                            ws.Cells[row, col++].Value = attributeKeyMap[attr];

                        // Style the header
                        using (var rng = ws.Cells[1, 1, 1, col - 1])
                        {
                            rng.Style.Font.Bold = true;
                            rng.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            rng.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(242, 242, 242));
                        }

                        row++;

                        // ============================================================
                        // 3. Prepare PI Point Cache
                        // ============================================================
                        var invalidAttributes = new List<string>();

                        var piPointCache = selectedAttributes
                            .GroupBy(a => a.TagName)
                            .ToDictionary(
                                g => g.Key,
                                g =>
                                {
                                    try { return PIPoint.FindPIPoint(_piServer, g.Key); }
                                    catch
                                    {
                                        invalidAttributes.Add(g.Key);
                                        return null;
                                    }
                                }
                            );

                        piPointCache = piPointCache
                            .Where(kvp => kvp.Value != null)
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                        if (invalidAttributes.Any())
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(
                                    "The following attribute tagnames could not be resolved:\n\n" +
                                    string.Join("\n", invalidAttributes),
                                    "Invalid PI Points",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning
                                );
                            });
                        }

                        // ============================================================
                        // 4. Process each Event Frame
                        // ============================================================
                        foreach (var efRow in selectedFrames)
                        {
                            var ef = efRow.AFEventFrame;
                            string efName = efRow.Name ?? ef.Name;
                            string primaryElem = efRow.PrimaryElement ?? "";

                            var range = new AFTimeRange(
                                new AFTime(efRow.StartTime),
                                new AFTime(efRow.EndTime)
                            );

                            // --- Master timestamps from first selected attribute ---
                            var firstAttr = selectedAttributes[0];
                            var firstPt = piPointCache[firstAttr.TagName];

                            AFValues masterVals;
                            try
                            {
                                masterVals = firstPt.InterpolatedValues(
                                    range,
                                    new AFTimeSpan(sampleInterval),
                                    null,
                                    false
                                );
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

                            // --- Load values for all attributes using unique KEY ---
                            var valuesByKey = new Dictionary<string, AFValues>(StringComparer.OrdinalIgnoreCase);

                            foreach (var attr in selectedAttributes)
                            {
                                AFValues vals = null;

                                if (piPointCache.TryGetValue(attr.TagName, out PIPoint pt))
                                {
                                    try
                                    {
                                        vals = pt.InterpolatedValues(
                                            range,
                                            new AFTimeSpan(sampleInterval),
                                            null,
                                            false
                                        );
                                    }
                                    catch { vals = null; }
                                }

                                string key = attributeKeyMap[attr];
                                valuesByKey[key] = vals;
                            }

                            // --- Load static EF attributes ---
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

                            // ============================================================
                            // 5. Write rows
                            // ============================================================
                            for (int i = 0; i < N; i++)
                            {
                                col = 1;

                                ws.Cells[row, col++].Value = efName;

                                foreach (var efAttr in selectedEFAttributes)
                                    ws.Cells[row, col++].Value = staticValues[efAttr.Name];

                                ws.Cells[row, col++].Value = primaryElem;

                                // Timestamp
                                ws.Cells[row, col].Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";
                                ws.Cells[row, col++].Value = timestamps[i];

                                // Attribute values using unique keys
                                foreach (var attr in selectedAttributes)
                                {
                                    string key = attributeKeyMap[attr];

                                    valuesByKey.TryGetValue(key, out var series);

                                    if (series != null && i < series.Count)
                                        ws.Cells[row, col++].Value = series[i].Value;
                                    else
                                        ws.Cells[row, col++].Value = null;
                                }

                                row++;
                            }
                        }

                        ws.Cells[ws.Dimension.Address].AutoFitColumns();

                        // Check for locked file
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
                    }
                });

                MessageBox.Show("Excel export completed successfully!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (button != null)
                {
                    button.Content = "Export to Excel";
                    button.IsEnabled = true;
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

                foreach (var row in _eventFrameRows)
                {
                    row.IsSelected = isChecked;
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


        private void EFCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            var cb = sender as CheckBox;
            var row = cb.DataContext as EventFrameRow;
            if (row == null) return;

            AFEventFrame ef = row.AFEventFrame;
            if (ef == null) return;

            foreach (AFAttribute attr in ef.Attributes)
            {
                if (!_efAttributeRows.Any(a => a.Name == attr.Name && a.EFName == ef.Name))
                {
                    _efAttributeRows.Add(new EFAttributeRow
                    {
                        Name = attr.Name,
                        EFName = ef.Name,
                        Category = string.Join(", ", attr.Categories.Select(c => c.Name)),
                        IsSelected = false,
                        AFEventFrame=ef
                    });
                }
            }

            RefreshEFAttrGrid();
        }

        private void EFCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            var cb = sender as CheckBox;
            var row = cb.DataContext as EventFrameRow;
            if (row == null) return;

            AFEventFrame ef = row.AFEventFrame;
            if (ef == null) return;

            var toRemove = _efAttributeRows
                .Where(a => a.EFName == ef.Name)
                .ToList();

            foreach (var attr in toRemove)
                _efAttributeRows.Remove(attr);

            RefreshEFAttrGrid();
        }

        private void RefreshEFAttrGrid()
        {
            _efAttrCollectionView = CollectionViewSource.GetDefaultView(_efAttributeRows);
            EFAttrDataGrid.ItemsSource = _efAttrCollectionView;
        }
        private readonly List<AFElement> afElementList = new List<AFElement>();

        private void EquipementComboBox0_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (EquipementComboBox0.SelectedItem == null)
                return;

            // Get the AFElement selected
            AFElement selectedElement = EquipementComboBox0.SelectedItem as AFElement;
            if (selectedElement == null)
                return;

            string selectedName = selectedElement.Name;

            // Get editable TextBox
            TextBox textBox = GetComboBoxTextBox(EquipementComboBox0);

            string currentText = (textBox?.Text) ?? (EquipementComboBox0.Text ?? string.Empty);

            var items = currentText
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            // Update displayed items (string list)
            if (!items.Contains(selectedName))
                items.Add(selectedName);

            // Update AFElement list (actual object list)
            if (!afElementList.Contains(selectedElement))
                afElementList.Add(selectedElement);

            string newText = string.Join("; ", items);

            // Defer UI update
            EquipementComboBox0.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (textBox != null)
                    {
                        textBox.Text = newText;
                        textBox.CaretIndex = newText.Length;
                    }

                    EquipementComboBox0.Text = newText;

                    // Optional: reset selection so user can pick again
                    // EquipementComboBox0.SelectedItem = null;

                    EquipementComboBox0.IsDropDownOpen = false;
                   
                }),
                System.Windows.Threading.DispatcherPriority.Background);

            afElementList.RemoveAll(el => !items.Contains(el.Name));
            getAttributes(afElementList);

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

      /*  private void EquipementComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _attributeRows.Clear();

            var selectedItem = EquipementComboBox.SelectedItem as AFElement;
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
                        TagName = tagName,
                        Category = category,
                        IsSelected = false
                    });
                }
            }

            _attrCollectionView = CollectionViewSource.GetDefaultView(_attributeRows);
            EltAttrDataGrid.ItemsSource = _attrCollectionView;


        }*/
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
                           || row.ElementName.ToLower().Contains(filterText)
                           || row.TagName.ToLower().Contains(filterText)
                           || row.Category.ToLower().Contains(filterText);
                }
                return false;
            };

            _attrCollectionView.Refresh();
        }

        // Select All toggle
        private void SelectAllCheckBox_attr_Click(object sender, RoutedEventArgs e)
        {
            bool newVal = (SelectAllCheckBox.IsChecked == true);

            foreach (var row in _attributeRows)
            {
                row.IsSelected = newVal;
            }
        }

        private void EFattrListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
           /* pipointListBox.Items.Clear();
            // Get selected item
            if (EFAttrListBox.SelectedItems.Count==1)
            {
                var selectedItems = EFAttrListBox.SelectedItem as AFAttribute;
                pipointListBox.Items.Add(selectedItems.ConfigString.Split('\\')
                                                                         .LastOrDefault().Split('?')[0]);
            }

            else
            {
                var selectedItems = EFAttrListBox.SelectedItems;
                foreach (AFAttribute attr in selectedItems)
                {
                    if (attr != null)
                    {
                        
                        pipointListBox.Items.Add(attr.ConfigString.Split('\\')
                                                                          .LastOrDefault().Split('?')[0]);
                    }
                }
            }*/
            
            
           
        }

        private void StartSpinner(Button button)
        {
            Dispatcher.Invoke(() =>
            {
                var spinner = (Ellipse)button.Template.FindName("spinner", button);
                var rotate = (RotateTransform)spinner.RenderTransform;

                spinner.Visibility = Visibility.Visible;
                var content = (ContentPresenter)button.Template.FindName("contentPresenter", button);
                if (content != null) content.Opacity = 0.3;

                var anim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1)))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };
                rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
            });
        }

        private void StopSpinner(Button button)
        {
            Dispatcher.Invoke(() =>
            {
                var spinner = (Ellipse)button.Template.FindName("spinner", button);
                var rotate = (RotateTransform)spinner.RenderTransform;

                spinner.Visibility = Visibility.Collapsed;
                var content = (ContentPresenter)button.Template.FindName("contentPresenter", button);
                if (content != null) content.Opacity = 1;

                rotate.BeginAnimation(RotateTransform.AngleProperty, null);
            });
        }

        private void SampleIntervalBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow digits and colon only
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(
                e.Text, @"[0-9:]");
        }

        private void SampleIntervalBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string text = SampleIntervalPicker.Text;
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    text, @"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]$"))
            {
                MessageBox.Show("Please enter a valid time in hh:mm:ss format.");
                SampleIntervalPicker.Text = ""; // clear instead of forcing focus
            }
        }

     


    }



}
