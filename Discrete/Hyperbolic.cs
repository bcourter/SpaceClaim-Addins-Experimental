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
using Application = SpaceClaim.Api.V10.Application;

namespace SpaceClaim.AddIn.Discrete {
	class CreatePoincareDiskButtonCapsule : RibbonButtonCapsule {
		public CreatePoincareDiskButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("PoincareDisk", Resources.CreatePoincareDiskCommandText, null, Resources.CreatePoincareDiskCommandHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			Window activeWindow = Window.ActiveWindow;
			Part part = activeWindow.Scene as Part;

			int steps = 16;
			double step = (double) 1 / steps;

			for (int i = -steps; i <= steps; i++) {
				double u = (double) i * step;

				List<List<Body>> bands = new List<List<Body>>();

				for (int j = -steps; j <= steps; j++) {
					double v = (double) j * step;

					PointUV uv00 = PointUV.Create(u, v);
					PointUV uv01 = PointUV.Create(u, v + step);
					PointUV uv11 = PointUV.Create(u + step, v + step);
					PointUV uv10 = PointUV.Create(u + step, v);

					if (
						uv00.MagnitudeSquared() > 1 ||
						uv01.MagnitudeSquared() > 1 ||
						uv11.MagnitudeSquared() > 1 ||
						uv10.MagnitudeSquared() > 1
					)
						continue;

					Point p00 = ToPoincare(uv00);
					Point p01 = ToPoincare(uv01);
					Point p11 = ToPoincare(uv11);
					Point p10 = ToPoincare(uv10);

					DesignCurve.Create(part, CurveSegment.Create(p00, p01));
					DesignCurve.Create(part, CurveSegment.Create(p00, p10));
				}
			}

			activeWindow.ZoomExtents();
		}

		static Point ToPoincare(PointUV uv) {
			double u = uv.U;
			double v = uv.V;

			double sumSquares = 1 - u * u - v * v;
			if (sumSquares == 0)
				return Point.Origin;

			return Point.Create(
				2 * u / sumSquares,
				2 * v / sumSquares,
				0
			);
		}
	}
}