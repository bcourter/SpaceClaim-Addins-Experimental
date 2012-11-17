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

	class SvgFileSaveHandler : FileSaveHandler {
		public SvgFileSaveHandler()
			: base("SVG Files", "svg") {
		}

		public override void SaveFile(string path) {
			var svgDoc = new SpaceClaim.Svg.Document(path);

			Part mainPart = Window.ActiveWindow.Scene as Part;
			if (mainPart == null)
				return;

			Color? strokeColor;
			Color? fillColor = null;

			foreach (IDesignFace iDesignFace in mainPart.GetDescendants<IDesignFace>()) {
				Face face = iDesignFace.Master.Shape;
				strokeColor = iDesignFace.GetAncestor<IDesignBody>().GetVisibleColor();
				fillColor = Color.FromArgb(127, strokeColor.Value);

				foreach (Loop loop in face.Loops)
					svgDoc.AddPath(loop.Fins.Select(f => (ITrimmedCurve) f.Edge).ToList(), true, 1, strokeColor, fillColor);
			}


			Dictionary<Layer, List<CurveSegment>> CurvesOnLayer = mainPart.GetCurvesByLayer();
			AddCurvesByLayer(svgDoc, fillColor, CurvesOnLayer);

			foreach (IComponent iComponent in mainPart.Components) {
				CurvesOnLayer = iComponent.GetCurvesByLayer();
				AddCurvesByLayer(svgDoc, fillColor, CurvesOnLayer);
			}

			svgDoc.SaveXml();
		}

		private static void AddCurvesByLayer(SpaceClaim.Svg.Document svgDoc, Color? fillColor, Dictionary<Layer, List<CurveSegment>> CurvesOnLayer) {
			foreach (Layer layer in CurvesOnLayer.Keys) {
				List<List<ITrimmedCurve>> profiles = CurvesOnLayer[layer].Cast<ITrimmedCurve>().ToList().ExtractChains().Select(c => c.ToList()).ToList();
				foreach (List<ITrimmedCurve> profile in profiles) {
					svgDoc.AddPath(profile, false, GetLineWeight(layer.GetLineWeight(null)), layer.GetColor(null), fillColor);
				}
			}
		}

		private static double GetLineWeight(LineWeight lineWeight) {
			switch (lineWeight.Type) {
				case LineWeightType.Thick:
					return 0.0007;

				case LineWeightType.Thin:
					return 0.0003;

				case LineWeightType.Numeric:
					return lineWeight.Thickness;

				case LineWeightType.ThickMissingThick:
				case LineWeightType.ThickThinThick:
					return 0.0001;

				default:
					throw new NotSupportedException("Unhandled Line Thickness");
			}
		}
	}
}
