using System;
using System.Drawing;
using Rhino;
using Rhino.UI;
using Rhino.PlugIns;
using Eto.Forms;
using MyRhinoClass.Views;

namespace MyRhinoClass
{
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    [System.Runtime.InteropServices.Guid("D5E82E2D-193C-4D7E-B95B-5ABD1B84C0B2")]
    public class MyRhinoClassPlugin : Rhino.PlugIns.PlugIn
    {
        public MyRhinoClassPlugin()
        {
            Instance = this;
        }
        
        ///<summary>Gets the only instance of the MyRhinoClassPlugin plug-in.</summary>
        public static MyRhinoClassPlugin Instance { get; private set; }

        // You can override methods here to change the plug-in behavior on
        // loading and shut down, add options pages to the Rhino _Option command
        // and maintain plug-in wide options in a document.

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            try
            {
                // Register the classes panel
                Panels.RegisterPanel(this, typeof(ClassesPanelHost), "Classes", SystemIcons.Application);
                return LoadReturnCode.Success;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return LoadReturnCode.ErrorShowDialog;
            }
        }
    }

    [System.Runtime.InteropServices.GuidAttribute("DC72583E-4FFB-48EF-B991-DDFF232B0276")]
    public class ClassesPanelHost : Panel, IPanel
    {
        private readonly ClassesPanel _panel;

        public ClassesPanelHost()
        {
            _panel = new ClassesPanel();
            Content = _panel;
        }

        public static Guid PanelId => typeof(ClassesPanelHost).GUID;

        void IPanel.PanelShown(uint documentSerialNumber, ShowPanelReason reason)
        {
            // Panel is being shown
        }

        void IPanel.PanelHidden(uint documentSerialNumber, ShowPanelReason reason)
        {
            // Panel is being hidden
        }

        void IPanel.PanelClosing(uint documentSerialNumber, bool onCloseDocument)
        {
            // Panel is closing
        }

        public object PanelContent => this;
    }
}