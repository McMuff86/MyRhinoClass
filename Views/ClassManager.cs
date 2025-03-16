using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;

namespace MyRhinoClass.Views
{
    public class ClassManager
    {
        private static ClassManager _instance;
        private readonly Dictionary<Guid, RhinoClass> _classes = new Dictionary<Guid, RhinoClass>();

        public static ClassManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ClassManager();
                return _instance;
            }
        }

        private ClassManager() { }

        public RhinoClass CreateClass(string name)
        {
            var rhinoClass = new RhinoClass(name);
            _classes[rhinoClass.Id] = rhinoClass;
            return rhinoClass;
        }

        public void DeleteClass(Guid classId)
        {
            var classToDelete = GetClassById(classId);
            if (classToDelete != null)
            {
                // Remove all child references to this class
                foreach (var rhinoClass in _classes.Values)
                {
                    if (rhinoClass.ParentId == classId)
                    {
                        rhinoClass.ParentId = null;
                    }
                }

                // Remove the class
                _classes.Remove(classId);
            }
        }

        public void AssignObjectToClass(Guid objectId, Guid classId)
        {
            RhinoApp.WriteLine($"\nClassManager: Attempting to assign object {objectId} to class {classId}");
            
            if (!_classes.TryGetValue(classId, out var rhinoClass))
            {
                RhinoApp.WriteLine("ClassManager: Error - Class not found with ID: " + classId);
                return;
            }

            RhinoApp.WriteLine("ClassManager: Found target class: " + rhinoClass.Name);
            RhinoApp.WriteLine("ClassManager: Class current object count: " + rhinoClass.ObjectIds.Count);
            
            // Remove object from its current class if it exists
            foreach (var existingClass in _classes.Values)
            {
                if (existingClass.ObjectIds.Contains(objectId))
                {
                    RhinoApp.WriteLine("ClassManager: Removing object from existing class: " + existingClass.Name);
                    existingClass.RemoveObject(objectId);
                    break;
                }
            }
            
            RhinoApp.WriteLine("ClassManager: Adding object to class: " + rhinoClass.Name);
            rhinoClass.AddObject(objectId);
            
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null)
            {
                var obj = doc.Objects.Find(objectId);
                if (obj != null)
                {
                    RhinoApp.WriteLine("ClassManager: Found object in document, type: " + obj.GetType().Name);
                    RhinoApp.WriteLine("ClassManager: Setting user string on object");
                    obj.Attributes.SetUserString("RhinoClass", classId.ToString());
                    obj.CommitChanges();
                    RhinoApp.WriteLine("ClassManager: Object attributes updated");
                }
                else
                {
                    RhinoApp.WriteLine("ClassManager: Error - Could not find object in document");
                }
                doc.Views.Redraw();
            }
            else
            {
                RhinoApp.WriteLine("ClassManager: Error - No active document");
            }
            
            // Verify the assignment
            if (rhinoClass.ObjectIds.Contains(objectId))
            {
                RhinoApp.WriteLine("ClassManager: Verification - Object successfully added to class");
                RhinoApp.WriteLine("ClassManager: Class now has " + rhinoClass.ObjectIds.Count + " objects");
            }
            else
            {
                RhinoApp.WriteLine("ClassManager: Error - Object was not added to class");
            }
        }

        public IEnumerable<RhinoClass> GetAllClasses()
        {
            return _classes.Values;
        }

        public RhinoClass GetClassById(Guid classId)
        {
            return _classes.TryGetValue(classId, out var rhinoClass) ? rhinoClass : null;
        }

        public RhinoClass GetClassByObjectId(Guid objectId)
        {
            return _classes.Values.FirstOrDefault(c => c.ObjectIds.Contains(objectId));
        }

        public void UpdateClass(RhinoClass rhinoClass)
        {
            RhinoApp.WriteLine("ClassManager: Updating class " + rhinoClass.Name);
            RhinoApp.WriteLine("ClassManager: Class has " + rhinoClass.ObjectIds.Count + " objects");
            
            if (_classes.ContainsKey(rhinoClass.Id))
            {
                _classes[rhinoClass.Id] = rhinoClass;
                RhinoApp.WriteLine("ClassManager: Class updated successfully");
            }
            else
            {
                RhinoApp.WriteLine("ClassManager: Error - Class not found in manager");
            }
        }
    }
} 