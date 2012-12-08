using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.Api.V10.Display;
using SpaceClaim.AddInLibrary;
using SpaceClaim.Svg;
using CAM.Properties;
using Color = System.Drawing.Color;
using Application = SpaceClaim.Api.V10.Application;

namespace SpaceClaim.AddIn.CAM {
    public class AddIn : SpaceClaim.Api.V10.Extensibility.AddIn, IExtensibility, IRibbonExtensibility, ICommandExtensibility {

        #region IExtensibility Members

        public bool Connect() {
            return true;
        }

        public void Disconnect() {
        }

        #endregion

        #region IRibbonExtensibility Members

        public string GetCustomUI() {
            return ribbonRoot.GetUI();
        }

        #endregion

        #region ICommandExtensibility Members

        RibbonRoot ribbonRoot = new RibbonRoot();
        public void Initialize() {
            var tab = new RibbonTabCapsule("CAM", Resources.TabText, ribbonRoot);
            RibbonGroupCapsule group;

            group = new RibbonGroupCapsule("ToolPath", Resources.ToolPathGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
            new FaceToolPathToolButtonCapsule(group, RibbonButtonCapsule.ButtonSize.large);
            new AnimationToolButtonCapsule(group, RibbonButtonCapsule.ButtonSize.large);

            foreach (PropertyDisplay property in FaceToolPathObject.Properties)
                Application.AddPropertyDisplay(property);
        }

        #endregion
    }
}
