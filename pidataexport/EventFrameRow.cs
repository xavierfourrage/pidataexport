using OSIsoft.AF.EventFrame;
using System;
using System.ComponentModel;
using System.Windows;

public class EventFrameRow : INotifyPropertyChanged
{
    private bool _isSelected;


    public string BatchId { get; set; }
    public string Name { get; set; }
    public string PrimaryElement {  get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }


    public AFEventFrame AFEventFrame { get; set; }  


    // NEW

    public int Level { get; set; } = 0;   // 0 = parent, 1 = child, 2 = grandchild, etc.


    public Thickness Indent { get; set; }



    // Expandable UI values computed directly (NO converter needed)
    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            ExpandSymbol = value ? "▼" : "▶";   // triangle symbols WITHOUT converter
            OnPropertyChanged(nameof(IsExpanded));
            OnPropertyChanged(nameof(ExpandSymbol));
        }
    }

    public string ExpandSymbol { get; private set; } = "▼"; // Default expanded

    public bool HasChildren { get; set; }
    public Visibility HasChildrenVisibility { get; set; }


    private Visibility _visibility = Visibility.Visible;
    public Visibility Visibility
    {
        get => _visibility;
        set { _visibility = value; OnPropertyChanged(nameof(Visibility)); }
    }




    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
