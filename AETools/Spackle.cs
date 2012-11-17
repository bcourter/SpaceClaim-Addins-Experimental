using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.AETools {
	static class Spackle {
		const string spackleToolCommandName = "AESpackleTool";
		static bool isSpackling = false;
		//static SpackleMode spackleMode = SpackleMode.New;
		static double height = 0.001;
		static double radius = 0.005;
		static double scatterRadius = .05;
		static int blastCount = 24;
		static Part spacklePart = null;

		public static void Initialize() {
			Command command;

			command = Command.Create(spackleToolCommandName);
			command.Text = "Spackle";
			command.Hint = "Blast your model with additive or erosive material";
			command.Executing += spackleTool_Executing;
			command.Updating += spackleTool_Updating;
		}

		static void spackleTool_Executing(object sender, EventArgs e) {
			isSpackling = !isSpackling;
			if (isSpackling)
				Window.ActiveWindow.SelectionChanged += Spackler;
			else
				Window.ActiveWindow.SelectionChanged -= Spackler;
		}

		static void spackleTool_Updating(object sender, EventArgs e) {
			Command command = (Command) sender;
			command.IsChecked = isSpackling;
			command.IsEnabled = true;
		}

		static void Spackler(object sender, EventArgs e) {
			WriteBlock.ExecuteTask("Spackle", delegate {
				SpackleSome();
			});
		}

		static void SpackleSome() {
			Window activeWindow = Window.ActiveWindow;
			spacklePart = Part.Create(activeWindow.Document, "Spackle");

			if (activeWindow.ActiveContext.SingleSelection == null)
				return;

			Point? selectionPoint = activeWindow.ActiveContext.GetSelectionPoint(activeWindow.ActiveContext.SingleSelection);
			if (selectionPoint == null)
				return;

			Random random = new Random();

			Point point = selectionPoint.Value;

			for (int i = 0; i < blastCount; i++) {
				Vector delta = Vector.Create(random.NextDouble() * scatterRadius, 0, 0);
				delta = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirY), random.NextDouble() * Math.PI * 2) * delta;
				delta = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), random.NextDouble() * Math.PI * 2) * delta;
				CreateShotNearPoint(point + delta, height, radius);
			}

			activeWindow.ActiveContext.Selection = null;
		}

		static void CreateShotNearPoint(Point point, double height, double radius) {
			Window activeWindow = Window.ActiveWindow;
			Line rayLine = Line.Create(point, activeWindow.Projection.Inverse * Direction.DirZ);
			ITrimmedCurve rayCurve = CurveSegment.Create(rayLine, Interval.Create(1000, -1000));

			//DesignCurve.Create(activeWindow.Scene as Part, rayCurve);	// draws the ray as a design object

			var intersectionList = new List<IntPoint<SurfaceEvaluation, CurveEvaluation>>();
			foreach (IDesignBody designBody in (activeWindow.Scene as Part).Bodies) {
				foreach (IDesignFace designFace in designBody.Faces) {
					intersectionList.AddRange(designFace.Shape.IntersectCurve(rayCurve));
				}
			}

			Point? closePoint = null;
			double closeParam = Double.MinValue;
			foreach (IntPoint<SurfaceEvaluation, CurveEvaluation> intersection in intersectionList) {
				if (intersection.EvaluationB.Param > closeParam) {
					closeParam = intersection.EvaluationB.Param;
					closePoint = intersection.Point;
				}
			}

			if (closePoint == null)
				return;

			DesignBody toolBody = ShapeHelper.CreateSphere(closePoint.Value, radius, activeWindow.Scene as Part);  //TBD why doesn't this work on spacklePart?
		
			//Frame? frame = GetFrameFromPoint(designFace, point);
			//if (frame == null)
			//    return;

			//Body body = CreateCylinder(point, frame.Value.DirX, frame.Value.DirY, radius, height);
			//DesignBody toolBody = AddInHelper.CreateSphere(frame.Value.Origin, radius, spacklePart);

			// TBD XXX Fix for v5
			//if (spackleMode == SpackleMode.Add)
			//    targetBody.Unite(new IDesignBody[] { toolBody });
			//else if (spackleMode == SpackleMode.Cut)
			//    targetBody.Subtract(new IDesignBody[] { toolBody });
		}

		static Frame? GetFrameFromPoint(IDesignFace designFace, Point startPoint) {
			SurfaceEvaluation evaluation = designFace.Shape.Geometry.ProjectPoint(startPoint);
			Point point = evaluation.Point;
			PointUV pointUV = evaluation.Param;
			Direction dirU = (designFace.Shape.Geometry.Evaluate(PointUV.Create(pointUV.U + 0.0001, pointUV.V)).Point.Vector
							- designFace.Shape.Geometry.Evaluate(PointUV.Create(pointUV.U - 0.0001, pointUV.V)).Point.Vector).Direction;
			Direction dirV = Direction.Cross(evaluation.Normal, dirU);

			Frame frame = Frame.Create(point, dirU, dirV);

			if (designFace.Shape.IsReversed) 
				return Frame.Create(frame.Origin, -frame.DirX, frame.DirY);
			
			return frame;
		}

		enum SpackleMode {
			New = 0,
			Add = 1,
			Cut = 2
		}

	}
}
