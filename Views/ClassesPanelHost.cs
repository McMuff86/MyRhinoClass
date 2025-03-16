using System;
using Rhino.UI;
using Eto.Forms;

namespace MyRhinoClass.Views
{
    public class ClassesPanelHost : Panel, IPanel
    {
        private readonly ClassesPanel _panel;

        public ClassesPanelHost()
        {
            _panel = new ClassesPanel();
            Content = _panel;
        }

        /// <summary>
        /// Returns the ID of this panel.
        /// </summary>
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