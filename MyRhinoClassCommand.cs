using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;

namespace MyRhinoClass
{
    [System.Runtime.InteropServices.Guid("A5E82E2D-193C-4D7E-B95B-5ABD1B84C0B1")]
    public class MyRhinoClassCommand : Command
    {
        public MyRhinoClassCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a reference in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static MyRhinoClassCommand Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "ShowClassesPanel";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                // Show the classes panel
                Panels.OpenPanel(typeof(Views.ClassesPanelHost).GUID);
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
