# MyRhinoClass Plugin - Wiederherstellungsanleitung (Rhino 8)

## 1. Projekt Setup
1. Visual Studio öffnen
2. Neues Projekt erstellen:
   - Projekttyp: "Class Library (.NET Framework)"
   - Name: "MyRhinoClass"
   - Speicherort: Wählen Sie einen geeigneten Pfad

## 2. NuGet-Pakete
Folgende NuGet-Pakete installieren:
- RhinoCommon (Version 8.0.23304.14305)
- Eto.Forms (Version 2.7.5)
- Newtonsoft.Json (Version 13.0.3)

## 3. Projektstruktur
Erstellen Sie folgende Ordnerstruktur:
```
MyRhinoClass/
├── Commands/
├── Models/
└── Views/
```

## 4. Hauptklassen

### RhinoClass.cs (in Models/)
```csharp
using System;
using System.Collections.Generic;

namespace MyRhinoClass
{
    public class RhinoClass
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public HashSet<Guid> ObjectIds { get; set; }
        public Guid? ParentId { get; set; }

        public RhinoClass()
        {
            Id = Guid.NewGuid();
            Name = string.Empty;
            ObjectIds = new HashSet<Guid>();
            ParentId = null;
        }

        public void AddObject(Guid objectId)
        {
            ObjectIds.Add(objectId);
        }

        public void RemoveObject(Guid objectId)
        {
            ObjectIds.Remove(objectId);
        }
    }
}
```

### ClassManager.cs (in Models/)
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Rhino;

namespace MyRhinoClass
{
    public class ClassManager
    {
        private static ClassManager _instance;
        private readonly string _dataFilePath;
        private readonly List<RhinoClass> _classes;

        public static ClassManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ClassManager();
                return _instance;
            }
        }

        private ClassManager()
        {
            _dataFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RhinoClasses",
                "classes.json"
            );
            _classes = LoadClasses();
        }

        private List<RhinoClass> LoadClasses()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    var json = File.ReadAllText(_dataFilePath);
                    return JsonConvert.DeserializeObject<List<RhinoClass>>(json) ?? new List<RhinoClass>();
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error loading classes: {ex.Message}");
            }
            return new List<RhinoClass>();
        }

        private void SaveClasses()
        {
            try
            {
                var directory = Path.GetDirectoryName(_dataFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonConvert.SerializeObject(_classes, Formatting.Indented);
                File.WriteAllText(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error saving classes: {ex.Message}");
            }
        }

        public RhinoClass CreateClass(string name)
        {
            var newClass = new RhinoClass { Name = name };
            _classes.Add(newClass);
            SaveClasses();
            return newClass;
        }

        public void UpdateClass(RhinoClass rhinoClass)
        {
            var existingClass = _classes.FirstOrDefault(c => c.Id == rhinoClass.Id);
            if (existingClass != null)
            {
                var index = _classes.IndexOf(existingClass);
                _classes[index] = rhinoClass;
                SaveClasses();
            }
        }

        public void DeleteClass(Guid classId)
        {
            var classToDelete = _classes.FirstOrDefault(c => c.Id == classId);
            if (classToDelete != null)
            {
                _classes.Remove(classToDelete);
                SaveClasses();
            }
        }

        public IEnumerable<RhinoClass> GetAllClasses()
        {
            return _classes.ToList();
        }

        public RhinoClass GetClassById(Guid id)
        {
            return _classes.FirstOrDefault(c => c.Id == id);
        }

        public RhinoClass GetClassByObjectId(Guid objectId)
        {
            return _classes.FirstOrDefault(c => c.ObjectIds.Contains(objectId));
        }
    }
}
```

### MyRhinoClassCommand.cs (in Commands/)
```csharp
using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using System;

namespace MyRhinoClass.Commands
{
    public class MyRhinoClassCommand : Command
    {
        public MyRhinoClassCommand()
        {
            Instance = this;
        }

        public static MyRhinoClassCommand Instance { get; private set; }

        public override string EnglishName => "MyRhinoClass";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var panel = Rhino.UI.Panels.GetPanel("MyRhinoClass");
            if (panel == null)
            {
                var classesPanel = new Views.ClassesPanel();
                panel = new Rhino.UI.Panels.Panel("MyRhinoClass", classesPanel);
                panel.Visible = true;
            }
            else
            {
                panel.Visible = !panel.Visible;
            }

            return Result.Success;
        }
    }
}
```

### MyRhinoClassPlugin.cs (im Hauptverzeichnis)
```csharp
using Rhino;
using System;

namespace MyRhinoClass
{
    public class MyRhinoClassPlugin : Rhino.PlugIns.PlugIn
    {
        public MyRhinoClassPlugin()
        {
            Instance = this;
        }

        public static MyRhinoClassPlugin Instance { get; private set; }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            return LoadReturnCode.Success;
        }
    }
}
```

## 5. Assembly Info
Öffnen Sie die Datei `Properties/AssemblyInfo.cs` und fügen Sie diese Attribute hinzu:
```csharp
[assembly: Guid("YOUR-GUID-HERE")] // Ersetzen Sie dies durch einen neuen GUID
[assembly: AssemblyTitle("MyRhinoClass")]
[assembly: AssemblyDescription("Class organization plugin for Rhino 8")]
[assembly: AssemblyCompany("Your Company")]
[assembly: AssemblyProduct("MyRhinoClass")]
[assembly: AssemblyCopyright("Copyright © 2024")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
```

## 6. ClassesPanel.cs
Die ClassesPanel.cs-Datei ist sehr umfangreich. Ich werde sie in einem separaten Abschnitt bereitstellen, da sie die komplexeste Komponente ist.

## 7. Build und Installation
1. Projekt in Release-Konfiguration erstellen
2. Die erstellte DLL in das Rhino 8 Plugins-Verzeichnis kopieren:
   - Typischerweise: `C:\Users\[Username]\AppData\Roaming\McNeel\Rhinoceros\8.0\Plug-ins`

## 8. Testen
1. Rhino 8 starten
2. Befehl "MyRhinoClass" eingeben
3. Panel sollte erscheinen mit allen implementierten Funktionen

Soll ich nun die ClassesPanel.cs-Datei mit allen aktuellen Funktionen bereitstellen? 