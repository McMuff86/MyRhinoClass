using System;
using System.Collections.Generic;
using Rhino;
using Eto.Forms;

namespace MyRhinoClass.Views
{
    public class RhinoClass : ITreeGridItem
    {
        private readonly List<ITreeGridItem> _children = new List<ITreeGridItem>();
        
        public string Name { get; set; }
        public Guid Id { get; } = Guid.NewGuid();
        public HashSet<Guid> ObjectIds { get; } = new HashSet<Guid>();
        public Guid? ParentId { get; set; }

        private bool _expanded;
        private ITreeGridItem? _parent;

        public string Text => $"{Name} ({ObjectIds.Count} objects)";
        
        public ITreeGridItem? Parent
        {
            get => _parent;
            set => _parent = value;
        }
        
        public bool Expanded
        {
            get => _expanded;
            set
            {
                if (_expanded != value)
                {
                    _expanded = value;
                    if (_expanded)
                    {
                        UpdateChildren();
                    }
                }
            }
        }
        
        public bool Expandable => _children.Count > 0;
        
        public IEnumerable<ITreeGridItem> Children => _children;

        public RhinoClass(string name)
        {
            Name = name;
            ObjectIds = new HashSet<Guid>();
            ParentId = null;
        }

        public void AddObject(Guid objectId)
        {
            RhinoApp.WriteLine("\nRhinoClass: Attempting to add object " + objectId + " to class " + Name);
            RhinoApp.WriteLine("RhinoClass: Current object count before adding: " + ObjectIds.Count);
            
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null)
            {
                RhinoApp.WriteLine("RhinoClass: Error - No active document");
                return;
            }

            var obj = doc.Objects.Find(objectId);
            if (obj == null)
            {
                RhinoApp.WriteLine("RhinoClass: Error - Could not find object in document");
                return;
            }

            RhinoApp.WriteLine("RhinoClass: Found object in document. Type: " + obj.GetType().Name);
            RhinoApp.WriteLine("RhinoClass: Object geometry type: " + obj.Geometry?.GetType().Name);
            RhinoApp.WriteLine("RhinoClass: Object valid: " + obj.IsValid);
            RhinoApp.WriteLine("RhinoClass: Object deleted: " + obj.IsDeleted);

            bool wasAdded = ObjectIds.Add(objectId);
            RhinoApp.WriteLine("RhinoClass: Object " + (wasAdded ? "was" : "was not") + " added to ObjectIds collection");
            RhinoApp.WriteLine("RhinoClass: Current object count after attempt: " + ObjectIds.Count);

            if (wasAdded)
            {
                try
                {
                    // Store the class ID in the object's attributes
                    string currentClassId = obj.Attributes.GetUserString("RhinoClass");
                    RhinoApp.WriteLine("RhinoClass: Current class ID in object attributes: " + (currentClassId ?? "none"));
                    
                    obj.Attributes.SetUserString("RhinoClass", Id.ToString());
                    obj.CommitChanges();
                    RhinoApp.WriteLine("RhinoClass: Set new class ID in object attributes: " + Id.ToString());
                    
                    // Verify the attribute was set
                    string verifyClassId = obj.Attributes.GetUserString("RhinoClass");
                    RhinoApp.WriteLine("RhinoClass: Verified class ID in object attributes: " + verifyClassId);
                    
                    // Add a child item for the object
                    var description = obj.ShortDescription(false);
                    var childItem = new TreeGridItem { 
                        Values = new object[] { description }
                    };
                    _children.Add(childItem);
                    
                    RhinoApp.WriteLine("RhinoClass: Added child item to tree: " + description);
                    RhinoApp.WriteLine("RhinoClass: Current children count: " + _children.Count);
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine("RhinoClass: Error while updating object - " + ex.Message);
                    RhinoApp.WriteLine("RhinoClass: Stack trace - " + ex.StackTrace);
                    // Rollback the addition if we couldn't update the object
                    ObjectIds.Remove(objectId);
                    RhinoApp.WriteLine("RhinoClass: Rolled back object addition");
                    return;
                }
            }
            else
            {
                RhinoApp.WriteLine("RhinoClass: Object " + objectId + " is already in class " + Name);
            }

            // Final verification
            if (ObjectIds.Contains(objectId))
            {
                RhinoApp.WriteLine("RhinoClass: Verification - Object is in the collection");
                RhinoApp.WriteLine("RhinoClass: Final object count: " + ObjectIds.Count);
            }
            else
            {
                RhinoApp.WriteLine("RhinoClass: Error - Object is not in the collection after attempted addition");
            }
        }

        public void RemoveObject(Guid objectId)
        {
            if (ObjectIds.Remove(objectId))
            {
                var obj = RhinoDoc.ActiveDoc?.Objects.Find(objectId);
                if (obj != null)
                {
                    obj.Attributes.SetUserString("RhinoClass", null);
                    obj.CommitChanges();
                }
                
                // Refresh children list
                UpdateChildren();
            }
        }

        private void UpdateChildren()
        {
            _children.Clear();
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null)
            {
                foreach (var objectId in ObjectIds)
                {
                    var obj = doc.Objects.Find(objectId);
                    if (obj != null)
                    {
                        var childItem = new TreeGridItem { 
                            Values = new object[] { obj.ShortDescription(false) }
                        };
                        _children.Add(childItem);
                    }
                }
            }
        }

        public override string ToString()
        {
            return $"{Name} ({ObjectIds.Count} objects)";
        }
    }
} 