using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.Threading;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using AETools.Properties;

namespace SpaceClaim.AddIn.AETools {
	public partial class AddIn : SpaceClaim.Api.V10.Extensibility.AddIn, IExtensibility, IRibbonExtensibility, ICommandExtensibility {		
		#region IExtensibility Members

		public bool Connect() {
			return true;
		}

		public void Disconnect() {
			Tumble.Disconnect();
			Freeze.Disconnect();
		}

		#endregion

		#region IRibbonExtensibility Members

		public string GetCustomUI() {
			return Resources.Ribbon;
		}

		#endregion

		#region ICommandExtensibility Members

		 public void Initialize() {
			 Options.Initialize();
			 Freeze.Initialize();
			 Wiggle.Initialize();
			 Tumble.Initialize();
			 AnimateHistory.Initialize();
			 ImageSurface.Initialize();
			 Spackle.Initialize();
			 Skeletize.Initialize();
			 Forstnerize.Initialize();
			 ZBitmap.Initialize();
			 Export.Initialize();

			 Rounds.Initialize();
			 Colors.Initialize();

             SpaceClaim.Api.V10.Application.AddFileHandler(new CodeVOpenHandler());
			 SpaceClaim.Api.V10.Application.AddFileHandler(new BezierOpenHandler());
             SpaceClaim.Api.V10.Application.AddFileHandler(new X3dFileSaveHandler());
             SpaceClaim.Api.V10.Application.AddFileHandler(new SSLFileSaveHandler());
		 }

		#endregion

		#region Event handlers
		#endregion
	}
}
