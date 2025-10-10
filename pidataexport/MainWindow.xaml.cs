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

        private PIServer _piServer;
        public PISystem _piSystem;
        public MainWindow()
        {
            InitializeComponent();

            AFDatabaseComboBox.Items.Clear();

            ConnectToPI();
            LoadAFDatabases();


            _eventFrameRows = new ObservableCollection<EventFrameRow>();
            _collectionView = CollectionViewSource.GetDefaultView(_eventFrameRows);
            /*EFDataGrid.ItemsSource = _collectionView;*/

            EFDataGrid.ItemsSource = _eventFrameRows;


            this.DataContext = this;
        }
        private void OpenConnectionHelper_Click(object sender, RoutedEventArgs e)
        {
            var win = new ConnectionHelperWindow();
            win.Owner = this;
            win.ShowDialog();   // modal, use .Show() if you want non-modal
        }

        private void ConnectToPI()
        {
            try
            {
                _piSystem = new PISystems().DefaultPISystem;
                _piSystem.Connect();

                _piServer = new PIServers().DefaultPIServer;
                _piServer.Connect();

                AFServer.Content = _piSystem.Name;
                PIDAServer.Content=_piServer.Name;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to PI system: {ex.Message}");
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
                    EquipementComboBox.Items.Clear();

                    if (aFElements.Count > 0)
                    {
                        foreach (AFElement aFElement in aFElements)
                        {
                            EquipementComboBox.Items.Add(aFElement);
                        }

                        // Optionally select the first element
                        EquipementComboBox.SelectedIndex = 0;
                    }
                }
            }
        }
     /*   private CancellationTokenSource _cts;*/
        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            string eventName = $"*{Eventname.Text}*";
            string elementname = Elementname.Text;
            string batchID = BatchID.Text.ToLower();

            AFDatabase aFDatabase = AFDatabaseComboBox.SelectedItem as AFDatabase;



/*            _cts = new CancellationTokenSource();
            var token = _cts.Token;*/

            try
            {
                button.Content = "Loading...";
                button.IsEnabled = false;
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);


                var starttime = StartDatePicker.SelectedDate;
                var endtime = EndDatePicker.SelectedDate;

                var eventFrames =AFEventFrame.FindEventFrames(
                        database: aFDatabase,
                        searchRoot: null,
                        searchMode: AFSearchMode.Overlapped,
                        startTime: new AFTime(starttime),
                        endTime: new AFTime(endtime),
                        nameFilter: eventName,
                        referencedElementNameFilter: elementname,
                        eventFrameCategory: null,
                        eventFrameTemplate: null,
                        referencedElementTemplate: null,
                        durationQuery: null,
                        searchFullHierarchy: true,
                        sortField: AFSortField.Name,
                        sortOrder: AFSortOrder.Ascending,
                        startIndex: 0,
                        maxCount: 1000
                    );
                

                /*                numberOfResults.Content = "Results exceed the search limit (1000)";*/

                _eventFrameRows.Clear();

                int count = 0;

                await Task.Run(async () => {
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
                                   
                                    if (eventFrames.Count == 1000)
                                        numberOfResults.Content = "Results exceed the search limit (1000)";
                                    else
                                        numberOfResults.Content = $"number of results: {count}"; ;
                                });
                            }
                        }
                    }
                   
                });

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
        /*        private void AbortSearch_Click(object sender, RoutedEventArgs e)
                {
                    _cts?.Cancel();
                }*/
        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = "Export.csv"
            };
            if (saveDialog.ShowDialog() != true) return;

            string filePath = saveDialog.FileName;

            bool isCompressed = false;
            TimeSpan? sampleInterval = null;
            if (!isCompressed)
            {
                if (!TimeSpan.TryParse(SampleIntervalBox.Text, out var parsed))
                {
                    MessageBox.Show("Invalid interval format. Use hh:mm:ss.");
                    return;
                }
                sampleInterval = parsed;
            }

            var selectedFrames = _eventFrameRows.Where(r => r.IsSelected).ToList();
            var selectedAttributes = _attributeRows.Where(a => a.IsSelected).ToList();

            if (!selectedFrames.Any() || !selectedAttributes.Any())
            {
                MessageBox.Show("Please select at least one event frame and one attribute.");
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    using (var writer = new StreamWriter(filePath, false, Encoding.UTF8, 65536))
                    {
                        // ---------- Header ----------
                        var header = new List<string>(selectedFrames.Count * (isCompressed ? selectedAttributes.Count * 2 : (selectedAttributes.Count + 1)));
                        foreach (var frame in selectedFrames)
                        {
                            string batchId = frame.BatchId ?? frame.Name;

                            if (isCompressed)
                            {
                                // One timestamp per attribute
                                foreach (var attr in selectedAttributes)
                                {
                                    header.Add($"{batchId}_{attr.Name}_Timestamp");
                                    header.Add($"{batchId}_{attr.Name}");
                                }
                            }
                            else
                            {
                                // One shared timestamp per frame
                                header.Add($"{batchId}_Timestamp");
                                foreach (var attr in selectedAttributes)
                                    header.Add($"{batchId}_{attr.Name}");
                            }
                        }
                        writer.WriteLine(string.Join(",", header));

                        // Cache PIPoints per tag (reuse across frames)
                        var piPointByTag = selectedAttributes
                            .GroupBy(a => a.TagName)
                            .ToDictionary(g => g.Key, g => PIPoint.FindPIPoint(_piServer, g.Key));

                        if (isCompressed)
                        {
                            // frame -> attrName -> AFValues (recorded)
                            var valuesByFrameAttr = new Dictionary<string, Dictionary<string, AFValues>>();
                            int globalMaxRows = 0;

                            foreach (var frame in selectedFrames)
                            {
                                var range = new AFTimeRange(new AFTime(frame.StartTime), new AFTime(frame.EndTime));
                                string fkey = frame.BatchId ?? frame.Name;
                                var dict = new Dictionary<string, AFValues>();
                                foreach (var attr in selectedAttributes)
                                {
                                    var pt = piPointByTag[attr.TagName];
                                    var vals = pt.RecordedValues(range, AFBoundaryType.Inside, null, false);
                                    dict[attr.Name] = vals;
                                    if (vals.Count > globalMaxRows) globalMaxRows = vals.Count;
                                }
                                valuesByFrameAttr[fkey] = dict;
                            }

                            // Write rows: each row index i pulls the i-th item of EACH (frame, attr) series
                            var sb = new StringBuilder(1 << 20);
                            const int FLUSH_EVERY = 50000;
                            int written = 0;

                            for (int i = 0; i < globalMaxRows; i++)
                            {
                                var row = new List<string>(selectedFrames.Count * selectedAttributes.Count * 2);

                                foreach (var frame in selectedFrames)
                                {
                                    string fkey = frame.BatchId ?? frame.Name;
                                    var dict = valuesByFrameAttr[fkey];

                                    foreach (var attr in selectedAttributes)
                                    {
                                        var series = dict[attr.Name];
                                        if (i < series.Count)
                                        {
                                            var v = series[i];
                                            row.Add(v.Timestamp.LocalTime.ToString("yyyy-MM-dd HH:mm:ss"));
                                            row.Add(v.Value?.ToString() ?? "");
                                        }
                                        else
                                        {
                                            row.Add("");
                                            row.Add("");
                                        }
                                    }
                                }

                                sb.AppendLine(string.Join(",", row));
                                if (++written % FLUSH_EVERY == 0)
                                {
                                    writer.Write(sb.ToString());
                                    sb.Clear();
                                }
                            }
                            if (sb.Length > 0) writer.Write(sb.ToString());
                        }
                        else
                        {
                            // SAMPLED: frame -> timestamps (from first attr) and frame -> attrName -> AFValues (interpolated)
                            var timesByFrame = new Dictionary<string, List<AFTime>>();
                            var valuesByFrameAttr = new Dictionary<string, Dictionary<string, AFValues>>();
                            int globalMaxRows = 0;

                            foreach (var frame in selectedFrames)
                            {
                                var range = new AFTimeRange(new AFTime(frame.StartTime), new AFTime(frame.EndTime));
                                string fkey = frame.BatchId ?? frame.Name;

                                // Master timestamps for this frame using the first selected attribute
                                var firstPt = piPointByTag[selectedAttributes[0].TagName];
                                var master = firstPt.InterpolatedValues(range, new AFTimeSpan(sampleInterval.Value), null, false);
                                var tsList = master.Select(v => v.Timestamp).ToList();
                                timesByFrame[fkey] = tsList;
                                if (tsList.Count > globalMaxRows) globalMaxRows = tsList.Count;

                                var dict = new Dictionary<string, AFValues>();
                                foreach (var attr in selectedAttributes)
                                {
                                    var pt = piPointByTag[attr.TagName];
                                    var vals = pt.InterpolatedValues(range, new AFTimeSpan(sampleInterval.Value), null, false);
                                    dict[attr.Name] = vals; // should match length of master
                                }
                                valuesByFrameAttr[fkey] = dict;
                            }

                            // Write rows: row i contains, for each frame: timestamp[i], then value[i] for each attr
                            var sb = new StringBuilder(1 << 20);
                            const int FLUSH_EVERY = 50000;
                            int written = 0;

                            for (int i = 0; i < globalMaxRows; i++)
                            {
                                var row = new List<string>(selectedFrames.Count * (1 + selectedAttributes.Count));

                                foreach (var frame in selectedFrames)
                                {
                                    string fkey = frame.BatchId ?? frame.Name;
                                    var tsList = timesByFrame[fkey];

                                    if (i < tsList.Count)
                                    {
                                        var tsStr = tsList[i].LocalTime.ToString("yyyy-MM-dd HH:mm:ss");
                                        row.Add(tsStr);

                                        foreach (var attr in selectedAttributes)
                                        {
                                            var series = valuesByFrameAttr[fkey][attr.Name];
                                            if (i < series.Count)
                                                row.Add(series[i].Value?.ToString() ?? "");
                                            else
                                                row.Add("");
                                        }
                                    }
                                    else
                                    {
                                        // pad empty block for this frame
                                        row.Add(""); // timestamp
                                        for (int c = 0; c < selectedAttributes.Count; c++) row.Add("");
                                    }
                                }

                                sb.AppendLine(string.Join(",", row));
                                if (++written % FLUSH_EVERY == 0)
                                {
                                    writer.Write(sb.ToString());
                                    sb.Clear();
                                }
                            }
                            if (sb.Length > 0) writer.Write(sb.ToString());
                        }
                    }
                });

                MessageBox.Show("Export completed successfully!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            _collectionView.Filter = obj =>
            {
                if (obj is EventFrameRow row)
                {
                    return string.IsNullOrEmpty(filterText)
                           || row.BatchId?.ToLower().Contains(filterText) == true
                           || row.Name?.ToLower().Contains(filterText) == true;
                }
                return false;
            };

            _collectionView.Refresh();
        }


        private void EquipementComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
            EFAttrDataGrid.ItemsSource = _attrCollectionView;
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
            string text = SampleIntervalBox.Text;
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    text, @"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]$"))
            {
                MessageBox.Show("Please enter a valid time in hh:mm:ss format.");
                SampleIntervalBox.Text = ""; // clear instead of forcing focus
            }
        }

      

    }
}
