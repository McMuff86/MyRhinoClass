# Guide to Creating a Rhino Panel

This guide describes the necessary steps to create a dockable panel in Rhino 8 using C#.

## 1. Project Structure

Create the following folder structure:
```
YourProject/
├── Views/
│   ├── YourPanel.cs          # The actual UI panel
│   └── YourPanelHost.cs      # The panel host for Rhino integration
├── YourProjectPlugin.cs      # The plugin main class
└── YourProjectCommand.cs     # The command to open the panel
```

## 2. Required References

Ensure the following references are present in your `.csproj` file:

```xml
<ItemGroup>
    <PackageReference Include="RhinoCommon" Version="8.0.23304.9001" ExcludeAssets="runtime" />
    <PackageReference Include="System.Drawing.Common" Version="7.0.0" />
</ItemGroup>

<ItemGroup>
    <Reference Include="Eto">
        <HintPath>..\..\..\..\..\Program Files\Rhino 8\System\Eto.dll</HintPath>
    </Reference>
    <Reference Include="RhinoCommon">
        <HintPath>..\..\..\..\..\Program Files\Rhino 8\System\RhinoCommon.dll</HintPath>
    </Reference>
    <Reference Include="Rhino.UI">
        <HintPath>..\..\..\..\..\Program Files\Rhino 8\System\Rhino.UI.dll</HintPath>
    </Reference>
</ItemGroup>
```

## 3. Panel Implementation

### 3.1 Advanced TreeGridView Panel Example

Here's an example of a panel with a sophisticated TreeGridView implementation that includes:
- Multiple columns with different data types
- Editable cells with validation
- Search functionality
- Real-time updates
- Object selection synchronization with Rhino

```csharp
using Eto.Forms;
using Eto.Drawing;
using System;
using System.Linq;
using System.Collections.Generic;

public class AdvancedPanel : Panel
{
    // Core components
    private readonly TreeGridView _treeView;
    private readonly TextBox _searchBox;
    private readonly Button _refreshButton;
    private readonly Label _infoLabel;
    private readonly TreeGridItemCollection _dataStore;
    
    // Tracking for double-click editing
    private TreeGridItem _lastClickedItem = null;
    private DateTime _lastClickTime = DateTime.MinValue;
    private const double DOUBLE_CLICK_TIME = 0.5; // seconds

    public AdvancedPanel()
    {
        // Initialize data store
        _dataStore = new TreeGridItemCollection();

        // Create TreeGridView
        _treeView = new TreeGridView
        {
            Style = "bordered",
            Size = new Size(600, 400),
            AllowMultipleSelection = false,
            ShowHeader = true
        };

        // Add main type column with editing support
        var typeColumn = new GridColumn
        {
            HeaderText = "Type",
            DataCell = new TextBoxCell
            {
                Binding = new DelegateBinding<TreeGridItem, string>(
                    r => r.Tag switch
                    {
                        // Customize this based on your data types
                        YourClass yc => $"{yc.Name} ({yc.Items.Count} items)",
                        YourObject yo => yo.GetType().Name,
                        _ => "Unknown Item"
                    },
                    (r, value) => HandleValueEdit(r, value)
                )
            },
            Editable = true,
            Width = 200
        };
        _treeView.Columns.Add(typeColumn);

        // Add details column
        var detailsColumn = new GridColumn
        {
            HeaderText = "Details",
            DataCell = new TextBoxCell
            {
                Binding = new DelegateBinding<TreeGridItem, string>(
                    r => GetDetailsForItem(r)
                )
            },
            Width = 250
        };
        _treeView.Columns.Add(detailsColumn);

        // Create search box
        _searchBox = new TextBox { PlaceholderText = "Search..." };
        _searchBox.TextChanged += (s, e) => RefreshList();

        // Create refresh button
        _refreshButton = new Button
        {
            Text = "↻ Refresh",
            ToolTip = "Refresh all values and view"
        };
        _refreshButton.Click += (s, e) => RefreshList();

        // Create info label
        _infoLabel = new Label
        {
            Width = 200,
            Wrap = WrapMode.Word
        };

        // Layout setup
        var layout = new DynamicLayout
        {
            DefaultSpacing = new Size(5, 5),
            Padding = new Padding(10)
        };

        // Add controls to layout
        var topRow = new DynamicLayout();
        topRow.AddRow(_searchBox, _refreshButton);
        layout.AddRow(topRow);

        var mainRow = new DynamicLayout();
        mainRow.AddRow(_treeView, _infoLabel);
        layout.AddRow(mainRow);

        Content = layout;

        // Event handlers
        _treeView.SelectionChanged += HandleSelectionChanged;
        _treeView.CellClick += HandleCellClick;
    }

    private void HandleSelectionChanged(object sender, EventArgs e)
    {
        var selectedItem = _treeView.SelectedItem as TreeGridItem;
        if (selectedItem?.Tag == null) return;

        // Update selection in your 3D view
        UpdateSelection(selectedItem);
        
        // Update info label
        UpdateInfoLabel(selectedItem);
    }

    private void HandleCellClick(object sender, GridCellMouseEventArgs e)
    {
        var now = DateTime.Now;
        var item = e.Item as TreeGridItem;

        // Handle double-click editing
        if (e.Column == 0 && item?.Tag != null)
        {
            if (item == _lastClickedItem && 
                (now - _lastClickTime).TotalSeconds < DOUBLE_CLICK_TIME)
            {
                if (IsItemEditable(item))
                {
                    _treeView.BeginEdit(e.Row, e.Column);
                }
            }
        }

        _lastClickedItem = item;
        _lastClickTime = now;
    }

    private void RefreshList(string searchTerm = null)
    {
        try
        {
            _dataStore.Clear();

            // Get your data
            var items = GetYourData();
            
            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                items = items.Where(i => MatchesSearch(i, searchTerm));
            }

            // Build tree structure
            foreach (var item in items)
            {
                var treeItem = CreateTreeItem(item);
                _dataStore.Add(treeItem);
            }

            _treeView.DataStore = _dataStore;
        }
        catch (Exception ex)
        {
            HandleError("Error refreshing list", ex);
        }
    }

    protected override void OnUnLoad(EventArgs e)
    {
        base.OnUnLoad(e);
        
        // Unsubscribe from events
        UnsubscribeFromEvents();
    }
}
```

### 3.2 Key Features to Implement

1. **Data Management**
   - Implement a data store class to manage your items
   - Create methods for CRUD operations
   - Handle data persistence if needed

2. **TreeGridView Setup**
   - Configure columns with appropriate bindings
   - Set up cell editing with validation
   - Implement double-click editing behavior
   - Handle selection changes

3. **Real-time Updates**
   - Subscribe to relevant document events
   - Update UI when data changes
   - Handle attribute modifications
   - Implement refresh mechanism

4. **Search and Filter**
   - Implement search functionality
   - Update tree items based on search terms
   - Maintain selection state during updates

5. **Error Handling**
   - Implement comprehensive error handling
   - Show user-friendly error messages
   - Log errors for debugging

6. **Performance Optimization**
   - Use async operations where appropriate
   - Implement efficient refresh mechanisms
   - Cache data when possible

### 3.3 Best Practices

1. **UI Design**
   - Use consistent spacing and padding
   - Implement clear visual hierarchy
   - Provide feedback for user actions
   - Use tooltips for additional information

2. **Event Handling**
   - Always unsubscribe from events in OnUnLoad
   - Use debouncing for frequent events
   - Handle UI updates on main thread

3. **Data Binding**
   - Use proper binding patterns
   - Implement validation
   - Handle null cases
   - Update UI efficiently

4. **Error Management**
   - Implement proper exception handling
   - Show user-friendly error messages
   - Log errors for debugging
   - Maintain application stability

## 4. Testing

1. **Functionality Testing**
   - Test all CRUD operations
   - Verify search functionality
   - Check real-time updates
   - Test selection synchronization

2. **Performance Testing**
   - Test with large data sets
   - Verify refresh performance
   - Check memory usage
   - Monitor event handling

3. **Error Handling**
   - Test error scenarios
   - Verify error messages
   - Check recovery behavior
   - Test boundary conditions

## 5. Troubleshooting

1. **Common Issues**
   - UI not updating: Check event handlers and bindings
   - Selection issues: Verify selection logic
   - Performance problems: Check data handling and updates
   - Memory leaks: Verify event unsubscription

2. **Debugging Tips**
   - Use RhinoApp.WriteLine for logging
   - Implement detailed error messages
   - Check event sequences
   - Monitor memory usage

## 6. Maintenance

1. **Code Organization**
   - Keep related functionality together
   - Use clear naming conventions
   - Document complex logic
   - Maintain separation of concerns

2. **Updates**
   - Keep dependencies updated
   - Test after Rhino updates
   - Maintain compatibility
   - Document changes

This extended guide provides a foundation for creating sophisticated panels in Rhino with advanced features like editable TreeGridViews, real-time updates, and proper error handling.

## 4. Plugin Implementation

### 4.1 The Plugin Class (YourProjectPlugin.cs)
```csharp
using System;
using System.Drawing;
using Rhino;
using Rhino.UI;
using Rhino.PlugIns;
using YourProject.Views;

namespace YourProject
{
    [System.Runtime.InteropServices.Guid("YOUR-PLUGIN-GUID-HERE-MAKE-IT-UNIQUE")]
    public class YourProjectPlugin : Rhino.PlugIns.PlugIn
    {
        public YourProjectPlugin()
        {
            Instance = this;
        }
        
        public static YourProjectPlugin Instance { get; private set; }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            try
            {
                // Register the panel
                Panels.RegisterPanel(this, typeof(YourPanelHost), "Your Panel Name", SystemIcons.Application);
                return LoadReturnCode.Success;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return LoadReturnCode.ErrorShowDialog;
            }
        }
    }
}
```

### 4.2 The Panel Opening Command (YourProjectCommand.cs)
```csharp
using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;

namespace YourProject
{
    [System.Runtime.InteropServices.Guid("YOUR-COMMAND-GUID-HERE-MAKE-IT-UNIQUE")]
    public class YourProjectCommand : Command
    {
        public YourProjectCommand()
        {
            Instance = this;
        }

        public static YourProjectCommand Instance { get; private set; }

        public override string EnglishName => "ShowYourPanel";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                Panels.OpenPanel(typeof(Views.YourPanelHost).GUID);
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error showing panel: {ex.Message}");
                return Result.Failure;
            }
        }
    }
}
```

## 5. Important Notes

1. **GUIDs**: 
   - Each GUID must be unique
   - Use tools like Visual Studio's "Create GUID" or online GUID generators
   - Save the GUIDs for future reference

2. **Namespaces**: 
   - Adjust all namespaces to match your project
   - Ensure all using directives are present

3. **Build Configuration**:
   - Target framework should be .NET Framework 4.8 or .NET 7.0
   - Output type should be a .rhp file

4. **Testing**:
   1. Compile the project
   2. Load the plugin in Rhino using `PlugInManager`
   3. Test the `ShowYourPanel` command
   4. The panel should also be available via the `Panels` command

## 6. Troubleshooting

1. **If the command is not found**:
   - Check if the plugin is loaded
   - Ensure GUIDs are correct
   - Check the Rhino command line for error messages

2. **If the panel doesn't display**:
   - Verify the panel registration in the plugin
   - Ensure all UI components are properly initialized

3. **For build errors**:
   - Check the references
   - Ensure all namespaces are correct
   - Verify interface implementations

## 7. Best Practices

1. **UI Design**:
   - Use consistent spacing (Padding, Spacing)
   - Follow Rhino's UI guidelines
   - Use modern controls (e.g., TreeGridView instead of TreeView)

2. **Code Organization**:
   - Separate UI logic from business logic
   - Use meaningful names
   - Document complex functionality

3. **Maintenance**:
   - Store all GUIDs in a separate file
   - Comment important code sections
   - Maintain a changelog file 