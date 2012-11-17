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
using Discrete.Properties;
using Color = System.Drawing.Color;

namespace SpaceClaim.AddIn.Discrete {
	class DxfFileSaveHandler : FileSaveHandler {
		public DxfFileSaveHandler()
			: base("Simple DXF Files", "dxf") {
		}

		public override void SaveFile(string path) {
			var dxfDoc = new SpaceClaim.Dxf.Document(path);

			Part mainPart = Window.ActiveWindow.Scene as Part;
			if (mainPart == null)
				return;

			foreach (IDesignFace iDesignFace in mainPart.GetDescendants<IDesignFace>()) {
				Face face = iDesignFace.Master.Shape;

				foreach (Fin fin in face.Loops.SelectMany(l => l.Fins))
					dxfDoc.AddCurve(fin.Edge);
			}

			foreach (IDesignCurve iDesignCurve in mainPart.GetDescendants<IDesignCurve>())
				dxfDoc.AddCurve(iDesignCurve.Shape);

			dxfDoc.SaveDxf();
		}
	}
}
