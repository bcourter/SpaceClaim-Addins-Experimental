using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using SpaceClaim.Api.V8;
using SpaceClaim.Api.V8.Extensibility;
using SpaceClaim.Api.V8.Geometry;
using SpaceClaim.Api.V8.Modeler;
using SpaceClaim.Api.V8.Display;
using SpaceClaim.AddInLibrary;
using SpaceClaim.Svg;
using SpaceClaim.AddIn.Tracker.Properties;
using Color = System.Drawing.Color;
using Application = SpaceClaim.Api.V8.Application;

namespace SpaceClaim.AddIn.Tracker {
	public class AddIn : SpaceClaim.Api.V8.Extensibility.AddIn, IExtensibility, IRibbonExtensibility, ICommandExtensibility {

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
			var tab = new RibbonTabCapsule("Tracker", Resources.TrackerText, ribbonRoot);
			RibbonGroupCapsule group;
			RibbonContainerCapsule container;
			RibbonButtonCapsule button;

			group = new RibbonGroupCapsule("Tracker", Resources.TrackerGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
			new TrackerLaunchButtonCapsule (group, RibbonButtonCapsule.ButtonSize.large);
			new TrackerGetEnvironmentButtonCapsule(group, RibbonButtonCapsule.ButtonSize.large);
			new TrackerGetRayButtonCapsule(group, RibbonButtonCapsule.ButtonSize.large);
		}
		
		#endregion
	}
}
