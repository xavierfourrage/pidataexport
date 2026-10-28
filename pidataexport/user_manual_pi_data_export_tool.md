# User Manual – PI AF / PI Data Export Tool

## 1. Overview

This application allows users to extract time-series data and contextual information from PI AF and PI Data Archive. The tool is designed to:

- Search and retrieve Event Frames (EF)
- Select attributes from both Event Frames and Elements
- Export data to Excel (.xlsx) or CSV (.csv)

The workflow is structured and must be followed in sequence to ensure valid exports.

---

## 2. Main Interface Description

### 2.1 Top Menu

**File**
- Save Configuration: Save current setup to a JSON file
- Load Configuration: Load a previously saved configuration
- Recent Configurations: Quickly reload recent files
- Connection Manager: Open server connection window
- Exit: Close the application

**Options**
- Configure default behavior (export folder, UI settings, EF behavior)

**Help**
- About: Displays information about the application

---

## 3. Connection Setup

Use the **Connection Manager** to:

- Select an AF Server
- Select a PI Data Archive
- Optionally specify credentials

Once validated, the selected servers are displayed in the main UI.

---

## 4. Configuration Panel

### 4.1 AF Database

Select the AF Database from the dropdown. This will populate the list of available Elements.

---

### 4.2 Element Selection (Element Name Filter)

- The dropdown allows multi-selection of Elements
- You can type a filter (e.g., `XT*`) to narrow results
- Checkboxes allow selecting multiple elements

#### Important Behavior
- When multiple elements are selected, the application determines their **common ancestor template**
- Only elements compatible with this common template remain selectable

---

### 4.3 Selected Elements Display

A label shows how many elements are selected.

---

### 4.4 Event Frame Name Filter (Phase)

- Free text input
- Used to filter Event Frames by name

Example:
- `Batch*`
- `PhaseA`

---

### 4.5 Time Frame Selection

- Start Date
- End Date

Defines the search window for Event Frames

---

## 5. Event Frame Search

Click **Search** to retrieve Event Frames.

The search is based on:
- Selected Elements
- EF Name Filter
- Time Range

### Results Grid (EFDataGrid)

Each row represents an Event Frame with:
- Name
- Primary Element
- Start / End Time

#### Features:
- Filtering (by name or element)
- Expand / collapse hierarchy
- Select individual or multiple Event Frames
- "Select All" applies only to filtered results

---

## 6. Event Frame Attribute Selection

After selecting Event Frames:

- The EF Attribute grid displays attributes from the **common Event Frame template**
- Only attributes shared across all selected Event Frames are shown

### Key Concept
The application computes the **common ancestor EF template** of all selected Event Frames.

Only attributes from this template hierarchy are displayed.

---

## 7. Element Attribute Selection

After selecting Elements:

- The Element Attribute grid displays attributes from the **common Element template**

### Supported Data References
- PI Point
- Formula

### Important Behavior
- Attributes come from the full template hierarchy
- Only attributes common to all selected elements are shown

---

## 8. Export Workflow (Mandatory Steps)

To perform a valid export:

1. Select one or more Elements
2. Click **Search** to retrieve Event Frames
3. Select one or more Event Frames
4. (Optional) Select Event Frame attributes
5. Select at least one Element attribute
6. Click **Export to Excel** or **Export to CSV**

---

## 9. Export Logic

### Data Structure

Each row in the output corresponds to:
- One timestamp within an Event Frame time range

### Columns

- EventFrame
- EF Attributes (if selected)
- PrimaryElement
- Timestamp
- Selected Element Attributes

### Important Change

- Attributes are retrieved **only from the Primary Element of each Event Frame**
- There is no duplication of columns per element

---

## 10. Attribute Data Handling

### PI Point Attributes
- Data is retrieved using interpolated values

### Formula Attributes
- Evaluated over the Event Frame time range

### Sampling
- Controlled via sampling interval input

---

## 11. Export Options

### Excel Export
- Outputs .xlsx file
- Automatically formatted columns

### CSV Export
- Outputs .csv file
- Lightweight and faster

### File Naming
- Includes timestamp and filters for traceability

---

## 12. Progress Monitoring

During export:
- Progress bar shows completion percentage
- Updates based on processing steps

---

## 13. Configuration Management

### Save Configuration
Stores:
- AF Server
- PI Server
- Database
- Selected Elements
- Filters
- Time range

### Load Configuration
- Restores full UI state
- Reconnects to servers
- Reloads elements and attributes

---

## 14. Options Panel

### General
- Default export folder
- Auto-open Excel after export

### EF Search
- Minimize EF hierarchy by default

### UI
- Default window width/height

---

## 15. Performance Notes

- Formula attributes are evaluated individually
- Large selections may increase processing time

---

## 16. Best Practices

- Limit number of selected Event Frames for faster exports
- Use filters to narrow results
- Prefer CSV for large datasets
- Ensure selected attributes exist across all templates

---

## 17. Troubleshooting

### No attributes displayed
- Ensure selected items share a common template

### Export is empty
- Verify at least one EF and one attribute is selected

### Slow performance
- Reduce number of attributes or Event Frames

---

## 18. Summary

This tool relies heavily on template consistency and structured workflow:

- Templates determine available attributes
- Event Frames define time ranges
- Primary Elements provide attribute data

Following the correct sequence ensures accurate and efficient exports.
