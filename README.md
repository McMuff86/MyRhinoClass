# RhinoClassOrganizer

## Essential Resources
Before beginning development, familiarize yourself with the following key standards and documentation:
- [Eto.Forms API Documentation](https://pages.picoe.ca/docs/api/html/R_Project_EtoForms.htm): Essential for creating the custom UI panel.
- [Rhino C++ API Documentation](https://mcneel.github.io/rhino-cpp-api-docs/api/cpp/): Provides insight into Rhino's native architecture (helpful for advanced integrations).
- [RhinoCommon API Documentation](https://developer.rhino3d.com/api/rhinocommon/): The primary API for plugin development in C#.

These resources are crucial for developing this plugin, especially for implementing a dockable panel with Eto.Forms and integrating it seamlessly into Rhino.

## Introduction
The RhinoClassOrganizer plugin introduces a hierarchical "Classes" system in Rhino, allowing objects to be grouped into classes and subclasses independently of layers. It provides a user-friendly, dockable panel—similar to Rhino's native Layer Panel—with features like drag-and-drop, filtering, and hierarchy management. Developed for Rhino 8, it uses RhinoCommon and Eto.Forms for a modern, cross-platform user interface.

## Requirements
- **Rhino 8**: Recommended for optimal performance and features.

## Installation
1. Clone or download the repository.
2. Open the solution in Visual Studio and ensure the target framework is set to .NET Framework 4.8.
3. Build the project and load the plugin in Rhino via the `PlugInManager` or by dragging the `.rhp` file into Rhino.

## Usage
1. **Open the Classes Panel**:
   - Run the `Classes` command or access it via the Rhino UI.
2. **Manage Classes**:
   - Use the tree view to add, rename, delete, or reorganize classes and subclasses.
   - Utilize drag-and-drop to adjust the hierarchy.
   - Apply filters using the search bar.
3. **Assign Objects**:
   - Select objects and assign them to one or more classes via the panel.
4. **Select by Class**:
   - Select all objects in a class or subclass directly from the panel.

## Development Notes
- **Framework and Compatibility**:
  - The project must target .NET Framework 4.8 to ensure compatibility with Rhino 7 and 8.
  - In the `.csproj` file, set `<TargetFramework>net48</TargetFramework>`.
  - Avoid multi-targeting (e.g., net7.0 and net48) to prevent debugger issues.
- **Visual Studio Settings**:
  - Set the debugger to "Mixed (.NET Framework and native)" to avoid issues with CoreCLR vs. Desktop CLR.
  - If debugger issues occur, adjust the debugger type in Project Properties → Debug, delete `bin` and `obj` folders, and restart Visual Studio.
- **Rhino Integration**:
  - Ensure RhinoCommon and Eto.Forms versions are compatible and from the same Rhino version (ideally Rhino 7).
  - The plugin class must inherit from `Rhino.PlugIns.PlugIn` and include a GUID attribute for identification.
- **UI Development**:
  - Use Eto.Forms to create a dockable panel registered with `Rhino.UI.Panels.RegisterPanel`.
  - Design the panel to mimic Rhino's native panels:
    - Set padding, size, and background color to match Rhino's UI.
    - Standardize button sizes (e.g., 22x22 pixels) and use `SystemColors` for theming.
    - Implement separators and other UI elements to match Rhino's aesthetic.
- **Data Storage**:
  - Store the class hierarchy in `RhinoDoc.UserDictionary` for persistence. If there is a better solution, use the better solution or develop someting on your own.
- **Performance**:
  - Optimize with caching and efficient data structures (e.g., `HashSet<Guid>` for object assignments).

## Project Structure
- **Models/**: Data models for the class hierarchy (see `readme_models.md`).
- **Views/**: UI components, including the Classes Panel (see `readme_views.md`).
- **Commands/**: Rhino commands (see `readme_commands.md`).
- **Utilities/**: Helper classes for data and event management (see `readme_utilities.md`).
- **Resources/**: Icons and other resources (see `readme_resources.md`).
- **Tests/**: Unit tests (see `readme_tests.md`).

Each subfolder includes a `readme_[subfoldername].md` file with a brief description of its contents.

## Additional Guidelines
- **Panel Integration**:
  - Register the panel with `Rhino.UI.Panels.RegisterPanel` to make it dockable and part of Rhino's standard UI.
- **Native Look and Feel**:
  - Use Rhino's UI conventions for colors, fonts, and icons.
  - Implement native interactions like drag-and-drop for reorganizing the class hierarchy and context menus for quick actions.
- **Best Practices**:
  - Organize files into appropriate folders (e.g., Commands, Models, Views).
  - Use a specific `.gitignore` for Visual Studio/C# projects.
  - Include comments in the code, especially for complex logic and Rhino API references.
