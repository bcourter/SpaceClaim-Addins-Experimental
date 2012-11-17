using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.IO;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;
using SpaceClaim.AddIn.Discrete;
using Discrete.Properties;

namespace SpaceClaim.AddIn.Discrete {
	public abstract class ExcelPropertiesButtonCapsule : RibbonButtonCapsule {
		protected int row = 1;
		protected ExcelWorksheet excelWorksheet = null;

		public ExcelPropertiesButtonCapsule(string name, string text, System.Drawing.Image image, string hint, RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base(name, text, image, hint, parent, buttonSize) {
		}
	}

	public class ExcelResetButtonCapsule : ExcelPropertiesButtonCapsule {
		public ExcelResetButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("Reset", Resources.ExcelResetText, null, Resources.ExcelResetHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			row = 1;
		}
	}

	public class ExcelLengthButtonCapsule : ExcelPropertiesButtonCapsule {
		public ExcelLengthButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("Length", Resources.ExcelLengthText, null, Resources.ExcelLengthHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			if (excelWorksheet == null)
				excelWorksheet = new ExcelWorksheet();

			Window activeWindow = Window.ActiveWindow;

			double length = 0;
			foreach (ITrimmedCurve iTrimmedCurve in activeWindow.GetAllSelectedITrimmedCurves()) {
				length += iTrimmedCurve.Length;
			}

			excelWorksheet.SetCell(row++, 1, length);
		}
	}

	public class ExcelAngleButtonCapsule : ExcelPropertiesButtonCapsule {
		public ExcelAngleButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("Angle", Resources.ExcelAngleText, null, Resources.ExcelAngleHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			if (excelWorksheet == null)
				excelWorksheet = new ExcelWorksheet();

			Window activeWindow = Window.ActiveWindow;

			List<ITrimmedCurve> iTrimmedCurves = new List<ITrimmedCurve>(activeWindow.GetAllSelectedITrimmedCurves());
			if (iTrimmedCurves.Count != 2)
				return;

			ITrimmedCurve curveA = iTrimmedCurves[0];
			ITrimmedCurve curveB = iTrimmedCurves[1];

			var intersections = new List<IntPoint<CurveEvaluation, CurveEvaluation>>(curveA.IntersectCurve(curveB));

			CurveEvaluation evalA = curveA.ProjectPoint(intersections[0].Point);
			CurveEvaluation evalB = curveB.ProjectPoint(intersections[0].Point);

			double angle = Math.Acos(Vector.Dot(evalA.Tangent.UnitVector, evalB.Tangent.UnitVector));

			excelWorksheet.SetCell(row++, 1, angle * 180 / Math.PI);
		}
	}



}