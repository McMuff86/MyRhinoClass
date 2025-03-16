using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
using Rhino;
using MyRhinoClass;
using Rhino.DocObjects;
using Font = Eto.Drawing.Font;

namespace MyRhinoClass.Views
{
    public class ClassesPanel : Panel
    {
        private readonly TreeGridView _treeView;
        private readonly TextBox _searchBox;
        private readonly TextBox _newClassName;
        private readonly Button _createClassButton;
        private readonly Button _assignToClassButton;
        private readonly Button _refreshButton;
        private readonly Button _moveUpButton;
        private readonly Button _moveDownButton;
        private readonly Button _moveRightButton;
        private readonly Button _moveLeftButton;
        private readonly Button _deleteClassButton;
        private readonly TreeGridItemCollection _dataStore;
        private readonly Label _objectInfoLabel;
        private readonly Dictionary<Guid, TreeGridItem> _objectItemMap = new Dictionary<Guid, TreeGridItem>();
        private readonly ContextMenu _contextMenu;
        private TreeGridItem _lastClickedItem = null;
        private DateTime _lastClickTime = DateTime.MinValue;
        private const double DOUBLE_CLICK_TIME = 0.5; // seconds
        private TreeGridItem _draggedItem = null;
        private Point? _dragStartPosition = null;
        private bool _isDragging = false;
        private const int DRAG_THRESHOLD = 5;
        private readonly Dictionary<Guid, string> _lastLoggedLayer = new Dictionary<Guid, string>();

        private class LayerItem
        {
            public string FullPath { get; set; }
            public int Index { get; set; }
            public Guid Id { get; set; }

            public override string ToString()
            {
                return FullPath;
            }
        }

        public ClassesPanel()
        {
            // Initialize the data store
            _dataStore = new TreeGridItemCollection();

            // Create context menu
            _contextMenu = new ContextMenu();
            var selectObjectsItem = new ButtonMenuItem { Text = "Select Objects" };
            selectObjectsItem.Click += SelectObjects_Click;
            _contextMenu.Items.Add(selectObjectsItem);

            // Create TreeGridView with editing capability
            _treeView = new TreeGridView
            {
                Style = "bordered",
                Size = new Size(600, 400),
                AllowMultipleSelection = false,
                ShowHeader = true,
                AllowColumnReordering = true
            };

            // Add drag and drop support
            _treeView.MouseDown += TreeView_MouseDown;
            _treeView.MouseMove += TreeView_MouseMove;
            _treeView.MouseUp += TreeView_MouseUp;

            _treeView.SelectionChanged += TreeView_SelectionChanged;
            _treeView.CellClick += TreeView_CellClick;

            // Add column for tree structure and type with editing capability
            var typeColumn = new GridColumn
            {
                HeaderText = "Type",
                DataCell = new TextBoxCell
                {
                    Binding = new DelegateBinding<TreeGridItem, string>(
                        r => r.Tag switch
                        {
                            RhinoClass rc => $"{rc.Name} ({rc.ObjectIds.Count} objects)",
                            Rhino.DocObjects.RhinoObject ro => ro.Geometry?.GetType().Name ?? "Unknown",
                            _ => "Unknown Item"
                        },
                        (r, value) =>
                        {
                            if (r.Tag is RhinoClass rc && !string.IsNullOrWhiteSpace(value))
                            {
                                try
                                {
                                    string newName = value;
                                    int bracketIndex = newName.IndexOf(" (");
                                    if (bracketIndex > 0)
                                    {
                                        newName = newName.Substring(0, bracketIndex);
                                    }

                                    if (newName != rc.Name)
                                    {
                                        rc.Name = newName;
                                        ClassManager.Instance.UpdateClass(rc);
                                        RhinoApp.WriteLine($"Renamed class from {rc.Name} to {newName}");
                                        RefreshClassList();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    RhinoApp.WriteLine($"Error renaming class: {ex.Message}");
                                    MessageBox.Show($"Error renaming class: {ex.Message}", "Error", MessageBoxType.Error);
                                }
                            }
                        }
                    )
                },
                Editable = true,
                Width = 200,
                Resizable = true
            };

            _treeView.Columns.Add(typeColumn);

            // Add column for object name
            var nameColumn = new GridColumn
            {
                HeaderText = "Name",
                DataCell = new TextBoxCell
                {
                    Binding = new DelegateBinding<TreeGridItem, string>(
                        r => r.Tag switch
                        {
                            RhinoClass _ => string.Empty,
                            Rhino.DocObjects.RhinoObject ro => GetObjectName(ro),
                            _ => string.Empty
                        },
                        (r, value) =>
                        {
                            if (r.Tag is Rhino.DocObjects.RhinoObject ro && !string.IsNullOrWhiteSpace(value))
                            {
                                try
                                {
                                    // Update the object's name attribute
                                    var attr = ro.Attributes;
                                    attr.Name = value;
                                    RhinoDoc.ActiveDoc.Objects.ModifyAttributes(ro, attr, true);
                                    RhinoApp.WriteLine($"Renamed object to: {value}");
                                    RefreshClassList(); // Refresh to show the updated name
                                }
                                catch (Exception ex)
                                {
                                    RhinoApp.WriteLine($"Error renaming object: {ex.Message}");
                                    MessageBox.Show($"Error renaming object: {ex.Message}", "Error", MessageBoxType.Error);
                                }
                            }
                        }
                    )
                },
                Editable = true,
                Width = 150,
                Resizable = true
            };
            _treeView.Columns.Add(nameColumn);

            // Add column for layer with layer picker
            var layerColumn = new GridColumn
            {
                HeaderText = "Layer",
                DataCell = new ComboBoxCell
                {
                    DataStore = GetAllLayerNames().Select(l => l.FullPath).ToList(),
                    Binding = new DelegateBinding<TreeGridItem, object>(
                        item =>
                        {
                            var doc = RhinoDoc.ActiveDoc;
                            if (doc != null && item.Tag is RhinoObject ro)
                            {
                                var currentLayer = doc.Layers[ro.Attributes.LayerIndex];
                                RhinoApp.WriteLine($"[GETTER] Aktueller Layer für {ro.Id}: '{currentLayer.FullPath}' (Index: {ro.Attributes.LayerIndex})");

                                // Gib direkt den Layer-Pfad zurück
                                return currentLayer.FullPath;
                            }
                            return null;
                        },
                        (item, value) =>
                        {
                            RhinoApp.WriteLine($"[SETTER] Empfangener Wert: '{value}' (Typ: {value?.GetType().Name ?? "null"})");
                            
                            if (item?.Tag is RhinoObject ro)
                            {
                                try
                                {
                                    var doc = RhinoDoc.ActiveDoc;
                                    if (doc == null) return;

                                    // Konvertiere den Wert in einen String
                                    string targetLayerName = value?.ToString();
                                    if (string.IsNullOrEmpty(targetLayerName))
                                    {
                                        RhinoApp.WriteLine("[SETTER] Ungültiger Layer-Name (null oder leer)");
                                        return;
                                    }

                                    // Finde den Layer-Index anhand des Namens
                                    int targetLayerIndex = -1;
                                    for (int i = 0; i < doc.Layers.Count; i++)
                                    {
                                        if (string.Equals(doc.Layers[i].FullPath, targetLayerName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            targetLayerIndex = i;
                                            break;
                                        }
                                    }

                                    if (targetLayerIndex == -1)
                                    {
                                        RhinoApp.WriteLine($"[SETTER] Layer '{targetLayerName}' nicht gefunden");
                                        return;
                                    }

                                    // Prüfe ob es wirklich eine Änderung ist
                                    var currentLayer = doc.Layers[ro.Attributes.LayerIndex];
                                    if (currentLayer.Index == targetLayerIndex)
                                    {
                                        RhinoApp.WriteLine($"[SETTER] Layer ist bereits '{targetLayerName}'");
                                        return;
                                    }

                                    RhinoApp.WriteLine($"[SETTER] Ändere Layer für Objekt {ro.Id}:");
                                    RhinoApp.WriteLine($"  Von: '{currentLayer.FullPath}' (Index: {ro.Attributes.LayerIndex})");
                                    RhinoApp.WriteLine($"  Zu:  '{targetLayerName}' (Index: {targetLayerIndex})");

                                    // Setze den neuen Layer
                                    ro.Attributes.LayerIndex = targetLayerIndex;
                                    
                                    if (doc.Objects.ModifyAttributes(ro, ro.Attributes, true))
                                    {
                                        RhinoApp.WriteLine($"[SETTER] Layer-Änderung erfolgreich");
                                        doc.Views.Redraw();
                                        Application.Instance.AsyncInvoke(() => RefreshClassList());
                                    }
                                    else
                                    {
                                        RhinoApp.WriteLine("[SETTER] Fehler beim Ändern des Layers");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    RhinoApp.WriteLine($"[SETTER] Fehler: {ex.Message}\n{ex.StackTrace}");
                                    MessageBox.Show($"Fehler beim Ändern des Layers: {ex.Message}", "Fehler", MessageBoxType.Error);
                                }
                            }
                        }
                    )
                },
                Width = 150,
                Resizable = true,
                Editable = true
            };
            _treeView.Columns.Add(layerColumn);

            // Add column for details
            var detailsColumn = new GridColumn
            {
                HeaderText = "Details",
                DataCell = new TextBoxCell 
                { 
                    Binding = new DelegateBinding<TreeGridItem, string>(
                        r => r.Tag switch
                        {
                            RhinoClass _ => string.Empty,
                            Rhino.DocObjects.RhinoObject ro => GetObjectDetails(ro),
                            _ => string.Empty
                        }
                    )
                },
                Width = 250,
                Resizable = true
            };
            _treeView.Columns.Add(detailsColumn);

            // Create object info label
            _objectInfoLabel = new Label
            {
                Width = 200,
                Wrap = WrapMode.Word
            };

            // Create search box
            _searchBox = new TextBox { PlaceholderText = "Search classes..." };
            _searchBox.TextChanged += SearchBox_TextChanged;

            // Create new class controls
            _newClassName = new TextBox { PlaceholderText = "New class name..." };
            _createClassButton = new Button { Text = "Create Class" };
            _createClassButton.Click += CreateClass_Click;

            // Create assign button
            _assignToClassButton = new Button { Text = "Assign Selected" };
            _assignToClassButton.Click += AssignToClass_Click;

            // Create refresh button
            _refreshButton = new Button 
            { 
                Text = "↻ Refresh",
                ToolTip = "Aktualisiere alle Werte und die Ansicht"
            };
            _refreshButton.Click += RefreshButton_Click;

            // Create class management buttons with small size
            var buttonSize = new Size(24, 24);
            
            _moveUpButton = new Button
            {
                Text = "↑",
                Size = buttonSize,
                ToolTip = "Move Class Up"
            };
            _moveUpButton.Click += MoveUp_Click;

            _moveDownButton = new Button
            {
                Text = "↓",
                Size = buttonSize,
                ToolTip = "Move Class Down"
            };
            _moveDownButton.Click += MoveDown_Click;

            _moveRightButton = new Button
            {
                Text = "→",
                Size = buttonSize,
                ToolTip = "Make Child Class"
            };
            _moveRightButton.Click += MoveRight_Click;

            _moveLeftButton = new Button
            {
                Text = "←",
                Size = buttonSize,
                ToolTip = "Remove from Hierarchy"
            };
            _moveLeftButton.Click += MoveLeft_Click;

            _deleteClassButton = new Button
            {
                Text = "×",
                Size = buttonSize,
                ToolTip = "Delete Class",
                Font = new Font(SystemFont.Default, 9)
            };
            _deleteClassButton.Click += DeleteClass_Click;

            // Layout
            var layout = new DynamicLayout { DefaultSpacing = new Size(5, 5), Padding = new Padding(10) };
            
            // Top row with search and refresh
            var topRow = new DynamicLayout { DefaultSpacing = new Size(5, 5) };
            topRow.AddRow(_searchBox, _refreshButton);
            layout.AddRow(topRow);
            
            // Class creation controls
            var newClassLayout = new DynamicLayout { DefaultSpacing = new Size(5, 5) };
            newClassLayout.Add(_newClassName);
            newClassLayout.Add(_createClassButton);
            layout.AddRow(newClassLayout);
            
            // Button row with assign and management buttons
            var buttonRow = new TableLayout
            {
                Spacing = new Size(2, 2),
                Rows = {
                    new TableRow(
                        _assignToClassButton,
                        _moveUpButton,
                        _moveDownButton,
                        _moveRightButton,
                        _moveLeftButton,
                        _deleteClassButton
                    )
                }
            };
            layout.AddRow(buttonRow);

            // Tree and info layout
            var horizontalLayout = new DynamicLayout { DefaultSpacing = new Size(10, 5) };
            horizontalLayout.AddRow(_treeView, _objectInfoLabel);
            
            layout.AddRow(horizontalLayout);
            Content = layout;

            RefreshClassList();

            // Subscribe to Rhino document events
            RhinoDoc.ModifyObjectAttributes += RhinoDoc_ModifyObjectAttributes;
        }

        private string GetObjectName(Rhino.DocObjects.RhinoObject obj)
        {
            if (obj == null) return string.Empty;

            // Versuche zuerst, den Objektnamen zu bekommen
            if (!string.IsNullOrEmpty(obj.Name))
                return obj.Name;

            // Falls kein Name vorhanden, verwende eine aussagekräftige Beschreibung
            return $"Object {obj.RuntimeSerialNumber}";
        }

        private string GetObjectDetails(Rhino.DocObjects.RhinoObject obj)
        {
            if (obj == null) return string.Empty;

            var details = new System.Text.StringBuilder();
            
            // Add geometry-specific details
            if (obj.Geometry != null)
            {
                switch (obj.Geometry)
                {
                    case Rhino.Geometry.Curve curve:
                        details.Append($"Length: {curve.GetLength():F2}");
                        break;
                    case Rhino.Geometry.Brep brep:
                        details.Append($"Faces: {brep.Faces.Count}");
                        if (brep.IsSolid)
                            details.Append($" | Volume: {brep.GetVolume():F2}");
                        break;
                    case Rhino.Geometry.Point point:
                        details.Append($"Pos: ({point.Location.X:F1}, {point.Location.Y:F1}, {point.Location.Z:F1})");
                        break;
                }
            }

            return details.ToString();
        }

        private void TreeView_SelectionChanged(object sender, EventArgs e)
        {
            var selectedItem = _treeView.SelectedItem as TreeGridItem;
            if (selectedItem?.Tag == null) return;

            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;

            try
            {
                if (selectedItem.Tag is RhinoClass rhinoClass)
                {
                    // Update info label for the class without changing selection
                    UpdateClassInfoLabel(rhinoClass);
                }
                else if (selectedItem.Tag is Rhino.DocObjects.RhinoObject rhinoObject)
                {
                    // For individual objects, add to selection without clearing existing selection
                    rhinoObject.Select(true);
                    UpdateObjectInfoLabel(rhinoObject);
                    doc.Views.Redraw();
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error in selection change: {ex.Message}");
            }
        }

        private void SelectSingleObject(Rhino.DocObjects.RhinoObject rhinoObject)
        {
            RhinoApp.WriteLine($"Selecting single object: {rhinoObject.Id}");
            rhinoObject.Select(true);
        }

        private void UpdateClassInfoLabel(RhinoClass rhinoClass)
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"Class: {rhinoClass.Name}");
            
            // Get all subclasses recursively
            var allSubClasses = GetAllSubClasses(rhinoClass);
            var totalObjects = rhinoClass.ObjectIds.Count + allSubClasses.Sum(c => c.ObjectIds.Count);
            
            info.AppendLine($"Direkte Objekte: {rhinoClass.ObjectIds.Count}");
            info.AppendLine($"Gesamte Objekte (inkl. Subklassen): {totalObjects}");
            info.AppendLine($"ID: {rhinoClass.Id}");

            // Add information about subclasses
            if (allSubClasses.Any())
            {
                info.AppendLine("\nSubklassen:");
                foreach (var subClass in allSubClasses)
                {
                    info.AppendLine($"- {subClass.Name} ({subClass.ObjectIds.Count} Objekte)");
                }
            }

            // Add statistics about object types in this class and subclasses
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null)
            {
                var typeStats = new Dictionary<string, int>();
                
                // Add objects from this class
                AddObjectTypesToStats(rhinoClass.ObjectIds, doc, typeStats);
                
                // Add objects from subclasses
                foreach (var subClass in allSubClasses)
                {
                    AddObjectTypesToStats(subClass.ObjectIds, doc, typeStats);
                }

                if (typeStats.Any())
                {
                    info.AppendLine("\nObjekt Typen (Gesamt):");
                    foreach (var stat in typeStats.OrderByDescending(x => x.Value))
                    {
                        info.AppendLine($"- {stat.Key}: {stat.Value}");
                    }
                }
            }

            _objectInfoLabel.Text = info.ToString();
        }

        private List<RhinoClass> GetAllSubClasses(RhinoClass parentClass)
        {
            var result = new List<RhinoClass>();
            var directChildren = ClassManager.Instance.GetAllClasses()
                .Where(c => c.ParentId == parentClass.Id)
                .ToList();

            foreach (var child in directChildren)
            {
                result.Add(child);
                result.AddRange(GetAllSubClasses(child));
            }

            return result;
        }

        private void AddObjectTypesToStats(IEnumerable<Guid> objectIds, RhinoDoc doc, Dictionary<string, int> typeStats)
        {
            foreach (var objectId in objectIds)
            {
                var obj = doc.Objects.Find(objectId);
                if (obj?.Geometry != null)
                {
                    var typeName = obj.Geometry.GetType().Name;
                    if (!typeStats.ContainsKey(typeName))
                        typeStats[typeName] = 0;
                    typeStats[typeName]++;
                }
            }
        }

        private void UpdateObjectInfoLabel(Rhino.DocObjects.RhinoObject rhinoObject)
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"Type: {rhinoObject.Geometry?.GetType().Name ?? "Unknown"}");
            
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null)
            {
                info.AppendLine($"Layer: {doc.Layers[rhinoObject.Attributes.LayerIndex].Name}");
            }
            
            info.AppendLine($"Color: {rhinoObject.Attributes.ObjectColor}");
            
            if (rhinoObject.Geometry != null)
            {
                switch (rhinoObject.Geometry)
                {
                    case Rhino.Geometry.Curve curve:
                        info.AppendLine($"Length: {curve.GetLength():F2}");
                        info.AppendLine($"Closed: {curve.IsClosed}");
                        break;
                    case Rhino.Geometry.Brep brep:
                        info.AppendLine($"Faces: {brep.Faces.Count}");
                        info.AppendLine($"Volume: {brep.GetVolume():F2}");
                        break;
                    case Rhino.Geometry.Point point:
                        info.AppendLine($"Location: ({point.Location.X:F2}, {point.Location.Y:F2}, {point.Location.Z:F2})");
                        break;
                }
            }

            _objectInfoLabel.Text = info.ToString();
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            RefreshClassList();
        }

        private void RefreshClassList(string searchTerm = null)
        {
            RhinoApp.WriteLine("\nStarting RefreshClassList...");
            
            try
            {
                _dataStore.Clear();
                _objectItemMap.Clear();

                var doc = RhinoDoc.ActiveDoc;
                if (doc == null) return;

                var allClasses = ClassManager.Instance.GetAllClasses().ToList();
                
                // Filter by search term if provided
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    allClasses = allClasses.Where(c => c.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }

                // First, add root classes (classes without parents)
                var rootClasses = allClasses.Where(c => !c.ParentId.HasValue).ToList();
                foreach (var rootClass in rootClasses)
                {
                    var rootItem = CreateClassTreeItem(rootClass, allClasses, doc);
                    _dataStore.Add(rootItem);
                }

                _treeView.DataStore = _dataStore;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error in RefreshClassList: {ex.Message}");
            }
        }

        private TreeGridItem CreateClassTreeItem(RhinoClass rhinoClass, List<RhinoClass> allClasses, RhinoDoc doc)
        {
            var classItem = new TreeGridItem { Tag = rhinoClass, Expanded = true };

            // Add child classes
            var childClasses = allClasses.Where(c => c.ParentId == rhinoClass.Id).ToList();
            foreach (var childClass in childClasses)
            {
                var childItem = CreateClassTreeItem(childClass, allClasses, doc);
                classItem.Children.Add(childItem);
            }

            // Add objects
            foreach (var objectId in rhinoClass.ObjectIds)
            {
                var obj = doc.Objects.Find(objectId);
                if (obj != null)
                {
                    var objectItem = new TreeGridItem { Tag = obj };
                    classItem.Children.Add(objectItem);
                    _objectItemMap[obj.Id] = objectItem;
                }
            }

            return classItem;
        }

        private void CreateClass_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_newClassName.Text))
            {
                MessageBox.Show("Please enter a class name.", "Error", MessageBoxType.Error);
                return;
            }

            var newClass = ClassManager.Instance.CreateClass(_newClassName.Text);
            RefreshClassList();

            // Find and select the newly created class
            foreach (TreeGridItem item in _dataStore)
            {
                if (item.Tag is RhinoClass rhinoClass && rhinoClass.Id == newClass.Id)
                {
                    _treeView.SelectedItem = item;
                    break;
                }
            }

            _newClassName.Text = string.Empty;
        }

        private void AssignToClass_Click(object sender, EventArgs e)
        {
            var selectedItem = _treeView.SelectedItem as TreeGridItem;
            if (selectedItem?.Tag is RhinoClass selectedClass)
            {
                RhinoApp.WriteLine("\nStarting object assignment process...");
                
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null)
                {
                    RhinoApp.WriteLine("Error: No active document");
                    return;
                }

                RhinoApp.WriteLine("Getting selected objects...");
                var selectedObjects = doc.Objects.GetSelectedObjects(true, false);
                if (selectedObjects == null)
                {
                    RhinoApp.WriteLine("Error: Selected objects is null");
                    return;
                }

                var objectsArray = selectedObjects.ToArray();
                var selectedCount = objectsArray.Length;
                
                RhinoApp.WriteLine("Total objects in doc: " + doc.Objects.Count);
                RhinoApp.WriteLine("Selected class: " + selectedClass.Name + " (ID: " + selectedClass.Id + ")");
                RhinoApp.WriteLine("Selected objects count: " + selectedCount);

                if (selectedCount > 0)
                {
                    bool anyObjectsAdded = false;
                    
                    foreach (var obj in objectsArray)
                    {
                        if (obj != null)
                        {
                            try
                            {
                                var existingClass = ClassManager.Instance.GetClassByObjectId(obj.Id);
                                if (existingClass != null)
                                {
                                    existingClass.RemoveObject(obj.Id);
                                }

                                selectedClass.AddObject(obj.Id);
                                
                                if (selectedClass.ObjectIds.Contains(obj.Id))
                                {
                                    anyObjectsAdded = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                RhinoApp.WriteLine("Error assigning object: " + ex.Message);
                            }
                        }
                    }

                    if (anyObjectsAdded)
                    {
                        ClassManager.Instance.UpdateClass(selectedClass);
                        RefreshClassList();
                        doc.Views.Redraw();
                    }
                    else
                    {
                        RhinoApp.WriteLine("Warning: No objects were successfully added to the class");
                    }
                }
                else
                {
                    MessageBox.Show("No objects selected. Please select objects in Rhino first.", "Warning", MessageBoxType.Warning);
                }
            }
            else
            {
                MessageBox.Show("Please select a class first.", "Error", MessageBoxType.Error);
            }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            RhinoApp.WriteLine("Manual refresh requested...");
            
            try
            {
                // Store currently selected item
                var selectedItem = _treeView.SelectedItem as TreeGridItem;
                Guid? selectedId = null;
                
                if (selectedItem?.Tag is RhinoClass rc)
                    selectedId = rc.Id;
                else if (selectedItem?.Tag is Rhino.DocObjects.RhinoObject ro)
                    selectedId = ro.Id;

                // Refresh the list
                RefreshClassList();

                // Restore selection if possible
                if (selectedId.HasValue)
                {
                    foreach (TreeGridItem item in _dataStore)
                    {
                        // Check class items
                        if (item.Tag is RhinoClass itemClass && itemClass.Id == selectedId)
                        {
                            _treeView.SelectedItem = item;
                            break;
                        }

                        // Check object items
                        foreach (TreeGridItem childItem in item.Children)
                        {
                            if (childItem.Tag is Rhino.DocObjects.RhinoObject itemObject && 
                                itemObject.Id == selectedId)
                            {
                                _treeView.SelectedItem = childItem;
                                break;
                            }
                        }
                    }
                }

                RhinoApp.WriteLine("Manual refresh completed successfully");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error during manual refresh: {ex.Message}");
                MessageBox.Show("Fehler beim Aktualisieren: " + ex.Message, "Fehler", MessageBoxType.Error);
            }
        }

        private void RhinoDoc_ModifyObjectAttributes(object sender, Rhino.DocObjects.RhinoModifyObjectAttributesEventArgs e)
        {
            try
            {
                if (e.NewAttributes.Name != e.OldAttributes.Name)
                {
                    if (_objectItemMap.TryGetValue(e.RhinoObject.Id, out var item))
                    {
                        Application.Instance.AsyncInvoke(() =>
                        {
                            try
                            {
                                // Update the item and its parent
                                _treeView.ReloadItem(item);
                                if (item.Parent != null)
                                {
                                    _treeView.ReloadItem(item.Parent);
                                }

                                // Update info label if this item is selected
                                if (_treeView.SelectedItem == item)
                                {
                                    UpdateObjectInfoLabel(e.RhinoObject);
                                }

                                RhinoApp.WriteLine($"Updated name for object {e.RhinoObject.Id}: {e.OldAttributes.Name} -> {e.NewAttributes.Name}");
                            }
                            catch (Exception ex)
                            {
                                RhinoApp.WriteLine($"Error updating tree item: {ex.Message}");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error handling attribute modification: {ex.Message}");
            }
        }

        private void TreeView_CellClick(object sender, GridCellMouseEventArgs e)
        {
            var now = DateTime.Now;
            var item = e.Item as TreeGridItem;

            // Only handle clicks in the first (type) or second (name) column
            if ((e.Column != 0 && e.Column != 1) || item?.Tag == null) return;

            // Check if this is a second click on the same item within the time window
            if (item == _lastClickedItem && (now - _lastClickTime).TotalSeconds < DOUBLE_CLICK_TIME)
            {
                if ((e.Column == 0 && item.Tag is RhinoClass) ||
                    (e.Column == 1 && item.Tag is Rhino.DocObjects.RhinoObject))
                {
                    // Start editing
                    _treeView.BeginEdit(e.Row, e.Column);
                }
            }

            _lastClickedItem = item;
            _lastClickTime = now;
        }

        private void TreeView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Buttons == MouseButtons.Primary)
            {
                var cell = _treeView.GetCellAt(new Point((int)e.Location.X, (int)e.Location.Y));
                var item = cell?.Item as TreeGridItem;
                if (item?.Tag is RhinoClass)
                {
                    _dragStartPosition = new Point((int)e.Location.X, (int)e.Location.Y);
                    _draggedItem = item;
                }
            }
        }

        private void TreeView_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging && _dragStartPosition.HasValue && _draggedItem != null)
            {
                var currentPoint = new Point((int)e.Location.X, (int)e.Location.Y);
                var dx = Math.Abs(currentPoint.X - _dragStartPosition.Value.X);
                var dy = Math.Abs(currentPoint.Y - _dragStartPosition.Value.Y);

                if (dx > DRAG_THRESHOLD || dy > DRAG_THRESHOLD)
                {
                    _isDragging = true;
                    _treeView.Cursor = Cursors.Move;
                }
            }
        }

        private void TreeView_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isDragging && _draggedItem != null)
            {
                var cell = _treeView.GetCellAt(new Point((int)e.Location.X, (int)e.Location.Y));
                var targetItem = cell?.Item as TreeGridItem;

                if (targetItem != null && IsValidDropTarget(targetItem))
                {
                    try
                    {
                        MoveClass(_draggedItem, targetItem);
                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine($"Error moving class: {ex.Message}");
                        MessageBox.Show($"Error moving class: {ex.Message}", "Error", MessageBoxType.Error);
                    }
                }
            }
            else if (e.Buttons == MouseButtons.Alternate) // Right click
            {
                var cell = _treeView.GetCellAt(new Point((int)e.Location.X, (int)e.Location.Y));
                var item = cell?.Item as TreeGridItem;
                
                if (item?.Tag is RhinoClass)
                {
                    _treeView.SelectedItem = item; // Select the item that was right-clicked
                    _contextMenu.Show(_treeView, new Point((int)e.Location.X, (int)e.Location.Y));
                }
            }

            // Reset drag state
            _isDragging = false;
            _draggedItem = null;
            _dragStartPosition = null;
            _treeView.Cursor = Cursors.Default;
        }

        private bool IsValidDropTarget(TreeGridItem targetItem)
        {
            if (_draggedItem == null || targetItem == null) return false;
            if (_draggedItem == targetItem) return false;

            // Check if target is a descendant of dragged item
            var current = targetItem;
            while (current != null)
            {
                if (current == _draggedItem) return false;
                current = current.Parent as TreeGridItem;
            }

            // Only allow dropping on RhinoClass items
            return targetItem.Tag is RhinoClass;
        }

        private void MoveClass(TreeGridItem sourceItem, TreeGridItem targetItem)
        {
            var sourceClass = sourceItem.Tag as RhinoClass;
            var targetClass = targetItem.Tag as RhinoClass;

            if (sourceClass == null || targetClass == null) return;

            RhinoApp.WriteLine($"Moving class {sourceClass.Name} to {targetClass.Name}");

            // Remove from old parent
            if (sourceItem.Parent != null)
            {
                ((TreeGridItem)sourceItem.Parent).Children.Remove(sourceItem);
            }
            else
            {
                _dataStore.Remove(sourceItem);
            }

            // Add to new parent
            targetItem.Children.Add(sourceItem);
            targetItem.Expanded = true;

            // Update the class hierarchy in the ClassManager
            sourceClass.ParentId = targetClass.Id;
            ClassManager.Instance.UpdateClass(sourceClass);

            // Refresh the view
            RefreshClassList();
        }

        private void MoveUp_Click(object sender, EventArgs e)
        {
            var selectedItem = _treeView.SelectedItem as TreeGridItem;
            if (selectedItem?.Tag is RhinoClass selectedClass)
            {
                var parent = selectedItem.Parent as TreeGridItem;
                var collection = parent?.Children ?? _dataStore;
                var index = collection.IndexOf(selectedItem);

                if (index > 0)
                {
                    // Get the class we're swapping with
                    var targetItem = collection[index - 1] as TreeGridItem;
                    if (targetItem?.Tag is RhinoClass targetClass)
                    {
                        // Swap the classes in the UI
                        collection.RemoveAt(index);
                        collection.Insert(index - 1, selectedItem);
                        _treeView.SelectedItem = selectedItem;

                        // Update the tree view without full refresh
                        _treeView.ReloadData();
                    }
                }
            }
        }

        private void MoveDown_Click(object sender, EventArgs e)
        {
            var selectedItem = _treeView.SelectedItem as TreeGridItem;
            if (selectedItem?.Tag is RhinoClass selectedClass)
            {
                var parent = selectedItem.Parent as TreeGridItem;
                var collection = parent?.Children ?? _dataStore;
                var index = collection.IndexOf(selectedItem);

                if (index < collection.Count - 1)
                {
                    // Get the class we're swapping with
                    var targetItem = collection[index + 1] as TreeGridItem;
                    if (targetItem?.Tag is RhinoClass targetClass)
                    {
                        // Swap the classes in the UI
                        collection.RemoveAt(index);
                        collection.Insert(index + 1, selectedItem);
                        _treeView.SelectedItem = selectedItem;

                        // Update the tree view without full refresh
                        _treeView.ReloadData();
                    }
                }
            }
        }

        private void MoveRight_Click(object sender, EventArgs e)
        {
            var selectedItem = _treeView.SelectedItem as TreeGridItem;
            if (selectedItem?.Tag is RhinoClass selectedClass)
            {
                var parent = selectedItem.Parent as TreeGridItem;
                
                // Move right (make child of previous sibling)
                if (parent != null)
                {
                    var siblings = parent.Children.Cast<TreeGridItem>().ToList();
                    var currentIndex = siblings.IndexOf(selectedItem);
                    if (currentIndex > 0)
                    {
                        var newParentItem = siblings[currentIndex - 1];
                        if (newParentItem.Tag is RhinoClass newParentClass)
                        {
                            // Update the parent relationship
                            selectedClass.ParentId = newParentClass.Id;
                            ClassManager.Instance.UpdateClass(selectedClass);
                            
                            // Move the item in the UI
                            parent.Children.Remove(selectedItem);
                            newParentItem.Children.Add(selectedItem);
                            newParentItem.Expanded = true;
                            _treeView.SelectedItem = selectedItem;

                            // Update the tree view without full refresh
                            _treeView.ReloadData();
                        }
                    }
                }
            }
        }

        private void MoveLeft_Click(object sender, EventArgs e)
        {
            var selectedItem = _treeView.SelectedItem as TreeGridItem;
            if (selectedItem?.Tag is RhinoClass selectedClass)
            {
                var parent = selectedItem.Parent as TreeGridItem;
                var grandparent = parent?.Parent as TreeGridItem;

                // Move left (make sibling of parent)
                if (parent?.Tag is RhinoClass parentClass)
                {
                    // Update the parent relationship to null if we're at root level
                    selectedClass.ParentId = grandparent?.Tag is RhinoClass grandParentClass ? grandParentClass.Id : null;
                    ClassManager.Instance.UpdateClass(selectedClass);
                    
                    // Move the item in the UI
                    parent.Children.Remove(selectedItem);
                    
                    if (grandparent != null)
                    {
                        // Insert after the parent if we have a grandparent
                        var parentIndex = grandparent.Children.IndexOf(parent);
                        grandparent.Children.Insert(parentIndex + 1, selectedItem);
                    }
                    else
                    {
                        // Add to root level if no grandparent
                        _dataStore.Add(selectedItem);
                    }
                    
                    _treeView.SelectedItem = selectedItem;

                    // Update the tree view without full refresh
                    _treeView.ReloadData();
                }
            }
        }

        private void DeleteClass_Click(object sender, EventArgs e)
        {
            var selectedItem = _treeView.SelectedItem as TreeGridItem;
            if (selectedItem?.Tag is RhinoClass selectedClass)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the class '{selectedClass.Name}'?\nThis will remove all object assignments but won't delete the objects.",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxType.Warning
                );

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        // Remove all object assignments
                        var objectIds = selectedClass.ObjectIds.ToList();
                        foreach (var objectId in objectIds)
                        {
                            selectedClass.RemoveObject(objectId);
                        }

                        // Remove the class
                        ClassManager.Instance.DeleteClass(selectedClass.Id);
                        RefreshClassList();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting class: {ex.Message}", "Error", MessageBoxType.Error);
                    }
                }
            }
        }

        private void SelectObjects_Click(object sender, EventArgs e)
        {
            var selectedItem = _treeView.SelectedItem as TreeGridItem;
            if (selectedItem?.Tag is RhinoClass selectedClass)
            {
                // Ask user if they want to keep existing selection
                var result = MessageBox.Show(
                    "Möchten Sie die bestehende Selektion beibehalten?\nJa = Zur Selektion hinzufügen\nNein = Neue Selektion",
                    "Selektion",
                    MessageBoxButtons.YesNo,
                    MessageBoxType.Question
                );

                if (result == DialogResult.No)
                {
                    // Clear existing selection only if user chooses to
                    RhinoDoc.ActiveDoc?.Objects.UnselectAll();
                }

                SelectClassAndChildrenObjects(selectedClass);
                RhinoDoc.ActiveDoc?.Views.Redraw();
            }
        }

        private void SelectClassAndChildrenObjects(RhinoClass rhinoClass)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;

            // Select objects in this class
            foreach (var objectId in rhinoClass.ObjectIds)
            {
                var obj = doc.Objects.Find(objectId);
                if (obj != null)
                {
                    obj.Select(true);
                }
            }

            // Recursively select objects in child classes
            var childClasses = ClassManager.Instance.GetAllClasses()
                .Where(c => c.ParentId == rhinoClass.Id);

            foreach (var childClass in childClasses)
            {
                SelectClassAndChildrenObjects(childClass);
            }
        }

        protected override void OnUnLoad(EventArgs e)
        {
            base.OnUnLoad(e);
            
            // Unsubscribe from events
            RhinoDoc.ModifyObjectAttributes -= RhinoDoc_ModifyObjectAttributes;
        }

        private IEnumerable<LayerItem> GetAllLayerNames()
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
                return Enumerable.Empty<LayerItem>();

            var layers = new List<LayerItem>();
            for (int i = 0; i < doc.Layers.Count; i++)
            {
                var layer = doc.Layers[i];
                var layerItem = new LayerItem 
                { 
                    FullPath = layer.FullPath,
                    Index = i,
                    Id = layer.Id
                };
                layers.Add(layerItem);
                RhinoApp.WriteLine($"[LAYERS] Layer {i}: Name='{layer.FullPath}', Index={i}, ID={layer.Id}");
            }

            return layers.OrderBy(l => l.FullPath);
        }
    }
} 