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
	abstract class TabPropertiesButtonCapsule : RibbonButtonCapsule {
		protected const double inches = 25.4 / 1000;

		protected bool areTabsFlipped = false;
		protected bool isTabStartSlot = false;
		protected double edgeOffset = 0.004;

		protected Window activeWindow;
		protected Layer dashLayer;
		protected Part part;

		public TabPropertiesButtonCapsule(string name, string text, System.Drawing.Image image, string hint, RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base(name, text, image, hint, parent, buttonSize) {

			Booleans[Resources.FlipTabsText] = new RibbonCommandBoolean(areTabsFlipped);
			Booleans[Resources.StartSlotText] = new RibbonCommandBoolean(isTabStartSlot);
			Values[Resources.EdgeOffsetFieldText] = new RibbonCommandValue(edgeOffset);
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			areTabsFlipped = Booleans[Resources.FlipTabsText].Value;
			isTabStartSlot = Booleans[Resources.StartSlotText].Value;
			edgeOffset = Values[Resources.EdgeOffsetFieldText].Value;

			activeWindow = Window.ActiveWindow;
			part = activeWindow.ActiveContext.Context as Part;
			dashLayer = NoteHelper.CreateOrGetLayer(activeWindow.ActiveContext.Context.Document, "Dashes", System.Drawing.Color.AliceBlue);
		}
	}

	class EdgeTabButtonCapsule : TabPropertiesButtonCapsule {
		public EdgeTabButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("EdgeTab", Resources.EdgeTabText, null, Resources.EdgeTabHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			base.OnExecute(command, context, buttonRect);

			Window activeWindow = Window.ActiveWindow;
			Part activePart = (activeWindow.Scene as Part);

			Layer tabLayer = NoteHelper.CreateOrGetLayer(activeWindow.ActiveContext.Context.Document, "Tabs", System.Drawing.Color.Fuchsia);

			IDesignEdge iDesignEdge = activeWindow.ActiveContext.SingleSelection as IDesignEdge;
			if (iDesignEdge == null)
				return;

			if (iDesignEdge.Faces.Count != 1)
				return;

			IDesignFace iDesignFace = null;
			foreach (IDesignFace testFace in iDesignEdge.Faces)
				iDesignFace = testFace;

			Debug.Assert(iDesignFace != null);

			Point startPoint = iDesignEdge.Shape.StartPoint;
			Point endPoint = iDesignEdge.Shape.EndPoint;

			if (areTabsFlipped) {
				Point tempPoint = startPoint;
				startPoint = endPoint;
				endPoint = tempPoint;
			}

			SurfaceEvaluation surfEval = iDesignFace.Shape.ProjectPoint(startPoint);
			Direction faceNormal = surfEval.Normal;
			Point midpoint = startPoint + (endPoint - startPoint) / 2;
			Double edgeLength = iDesignEdge.Shape.Length;
			Direction xDir = (endPoint - startPoint).Direction;

			List<Window> tabWindows = null;
			string tabFile = string.Empty;
			if (!isTabStartSlot)
				tabFile = @"C:\Users\bcr.SPACECLAIM\Documents\Models\Dodecahedron Foldcrease\Tab-Circle-Male.scdoc";
			else
				tabFile = @"C:\Users\bcr.SPACECLAIM\Documents\Models\Dodecahedron Foldcrease\Tab-Circle-Female.scdoc";

			try {
				tabWindows = new List<Window>(Document.Open(tabFile, ImportOptions.Create()));
			}
			catch (Exception exception) {
				System.Windows.Forms.MessageBox.Show(SpaceClaim.Api.V10.Application.MainWindow, exception.Message);
			}

			DesignBody tabDesignBody = null;
			foreach (DesignBody testBody in (tabWindows[0].Scene as Part).Bodies)
				tabDesignBody = testBody;

			Debug.Assert(tabDesignBody != null);

			tabDesignBody = DesignBody.Create(activePart, "tab", tabDesignBody.Shape.Body.Copy());

			foreach (Window window in tabWindows)
				window.Delete();

			Matrix scale = Matrix.CreateScale(edgeLength / 0.02, Point.Origin);
			Matrix trans = Matrix.CreateMapping(Frame.Create(midpoint, xDir, Direction.Cross(faceNormal, xDir)));

			tabDesignBody.Transform(trans * scale);
			tabDesignBody.Layer = tabLayer;
		}

#if false // used with old-style auto tab chaining ala pod tent
		// hardwired to place on about inch-spaced points
		// Edge-only for now -- See EO comments
		static void MakeTabs_Executing(object sender, EventArgs e) {
			Window activeWindow = Window.ActiveWindow;

			Layer tabLayer = NoteHelper.CreateOrGetLayer(activeWindow.ActiveContext.Context.Document, "Tabs", System.Drawing.Color.Fuchsia);

			ICollection<ITrimmedCurve> trimmedCurves = AddInHelper.GetITrimmedCurvesOfSelectedTopology(activeWindow);

			// quantize with lines to points separated by approx targetDistance;
			TrimmedCurveChain curveChain = new TrimmedCurveChain(trimmedCurves);
			Point lastPoint = curveChain.Curves[0].StartPoint;
			double tolerance = 0.2 * inches;
			double targetDistance = 1 * inches;
			trimmedCurves = new List<ITrimmedCurve>();
			Dictionary<ITrimmedCurve, Direction> OriginalNormals = new Dictionary<ITrimmedCurve, Direction>();

			double extraLength = 0;
			foreach (OrientedTrimmedCurve curve in curveChain.Curves) {
				Point point = curve.EndPoint;

				if (Math.Abs((lastPoint - point).Magnitude - targetDistance) > tolerance) {
					extraLength += (lastPoint - point).Magnitude;
					continue;
				}

				CurveSegment curveSegment = null;
				//		if (extraLength == 0)
				curveSegment = CurveSegment.Create(lastPoint, point);
				//		else
				//			curveSegment = CurveSegment.Create(point - (lastPoint - point).Direction * ((lastPoint - point).Magnitude + extraLength), point);

				trimmedCurves.Add(curveSegment);

				Edge edge = curve.TrimmedCurve as Edge;
				if (edge != null) {
					Face face = null;
					foreach (Face testFace in edge.Faces) {
						face = testFace;
						break;
					}

					SurfaceEvaluation surfEval = face.ProjectPoint(curve.StartPoint);
					OriginalNormals[curveSegment] = surfEval.Normal;
				}

				lastPoint = point;
				//		extraLength = 0;
			}

			curveChain = new TrimmedCurveChain(trimmedCurves);
			if (AreTabsFlipped)
				curveChain.Reverse();


			List<Window> curveWindows = new List<Window>(Document.Open(@"C:\Users\bcr.SPACECLAIM\Documents\Models\Pod Tent\TabCurve.scdoc", false));
			bool adjustEnds = true;
#if false
			List<Window> curveWindows = new List<Window>(Document.Open(@"C:\Users\bcr.SPACECLAIM\Documents\Models\Pod Tent\TabStrapEdgeCurve.scdoc", false));
			bool adjustEnds = true;
#endif
			NurbsCurve middle = GetFirstNurbsCurveFromPart(curveWindows[0].Scene as Part);
			Debug.Assert(middle != null);

			NurbsCurve endTab = null;
			NurbsCurve endSlot = null;
			foreach (Component component in (curveWindows[0].Scene as Part).Components) {
				if (component.Template.Name == "EndTab")
					endTab = GetFirstNurbsCurveFromPart(component.Template);

				if (component.Template.Name == "EndSlot")
					endSlot = GetFirstNurbsCurveFromPart(component.Template);
			}

			Debug.Assert(endTab != null);
			Debug.Assert(endSlot != null);

			Point startPoint = curveChain.Curves[0].StartPoint;
			Point endPoint = curveChain.Curves[0].EndPoint;
			Point startTail = startPoint + (endPoint - startPoint);
			bool mirror = false;

			if (IsTabStartSlot) {
				NurbsCurve tmp = endTab;
				endTab = endSlot;
				endSlot = tmp;

				mirror = !mirror;
			}

			for (int i = 0; i < curveChain.Curves.Count; i++) {
				Point endTail;
				if (i == curveChain.Curves.Count - 1)
					endTail = curveChain.Curves[i].EndPoint + (curveChain.Curves[i].EndPoint - curveChain.Curves[i].StartPoint);
				else
					endTail = curveChain.Curves[i + 1].EndPoint;

				Point mid = Point.Origin + (startPoint.Vector + endPoint.Vector) / 2;
				Direction startDirection = (startPoint - startTail).Direction;
				Direction lineDirection = (endPoint - startPoint).Direction;
				Direction endDirection = (endTail - endPoint).Direction;

				Direction upDirection = Direction.DirZ;
				//if (upDirection.IsParallelTo(lineDirection))
				//    upDirection = Direction.DirY;

				if (OriginalNormals.ContainsKey(curveChain.Curves[i].TrimmedCurve))
					upDirection = OriginalNormals[curveChain.Curves[i].TrimmedCurve];

				Direction normalDirection = Direction.Cross(lineDirection, upDirection);

				Line startMidLine;
				if (startDirection.UnitVector == lineDirection.UnitVector)
					startMidLine = Line.Create(startPoint, Direction.Cross(lineDirection, upDirection));
				else
					startMidLine = Line.Create(startPoint, (startDirection.UnitVector - lineDirection.UnitVector).Direction);

				Line endMidLine;
				if (lineDirection.UnitVector == endDirection.UnitVector)
					endMidLine = Line.Create(endPoint, Direction.Cross(lineDirection, upDirection));
				else
					endMidLine = Line.Create(endPoint, (lineDirection.UnitVector - endDirection.UnitVector).Direction);

				NurbsCurve template = middle;

				if (mirror) {
					lineDirection = -lineDirection;
					Line tmp = startMidLine;
					startMidLine = endMidLine;
					endMidLine = tmp;
				}

				if (i == 0)
					template = endSlot;

				if (i == curveChain.Curves.Count - 1) {
					if (mirror ^ IsTabStartSlot)
						template = endSlot;
					else
						template = endTab;
				}

				Frame frame = Frame.Create(mid, lineDirection, normalDirection);
				Matrix transform = Matrix.CreateMapping(frame);

				ControlPoint[] controlPoints = new ControlPoint[template.ControlPoints.Length];
				int j = 0;
				foreach (ControlPoint controlPoint in template.ControlPoints) {
					controlPoints[j] = new ControlPoint(transform * controlPoint.Position, controlPoint.Weight);
					j++;
				}

				//CurveEvaluation curveEval = null;
				//curveEval = startMidLine.Evaluate(1);
				//DesignCurve.Create(activeWindow.SubjectMatter as Part, CurveSegment.Create(startPoint, curveEval.Point));
				//curveEval = endMidLine.Evaluate(1);
				//DesignCurve.Create(activeWindow.SubjectMatter as Part, CurveSegment.Create(endPoint, curveEval.Point));

				MakeNurbsEndTangent(endMidLine, controlPoints, 0, 1);
				if (adjustEnds)
					MakeNurbsEndTangent(startMidLine, controlPoints, controlPoints.Length - 1, controlPoints.Length - 2);

				Curve curve = NurbsCurve.Create(template.Data, controlPoints);
				CurveSegment curveSegment = CurveSegment.Create(curve, template.Parameterization.Range.Value);

				DesignCurve tab = DesignCurve.Create(activeWindow.ActiveContext.Context as Part, curveSegment);
				tab.Layer = tabLayer;

				startTail = startPoint;
				startPoint = endPoint;
				endPoint = endTail;
				mirror = !mirror;

			}

			foreach (Window window in curveWindows)
				window.Close();

		}
#endif


		private static NurbsCurve GetFirstNurbsCurveFromPart(Part part) {
			List<DesignCurve> curves = new List<DesignCurve>(part.Curves);
			ITrimmedCurve testCurve = curves[0].Shape;
			Debug.Assert(testCurve.Geometry is NurbsCurve);
			return testCurve.Geometry as NurbsCurve;
		}

		private static void MakeNurbsEndTangent(Line cleaveLine, ControlPoint[] controlPoints, int endIndex, int tangentIndex) {
			Point initialPoint = controlPoints[endIndex].Position;
			CurveEvaluation curveEvaluation = cleaveLine.ProjectPoint(initialPoint);
			if (initialPoint == curveEvaluation.Point)
				return;

			controlPoints[endIndex] = new ControlPoint(curveEvaluation.Point, controlPoints[endIndex].Weight);

			Line tangentLine = Line.Create(curveEvaluation.Point, (initialPoint - curveEvaluation.Point).Direction);
			curveEvaluation = tangentLine.ProjectPoint(controlPoints[tangentIndex].Position);
			controlPoints[tangentIndex] = new ControlPoint(curveEvaluation.Point, controlPoints[tangentIndex].Weight);
		}

	}

	class OffsetEdgesButtonCapsule : TabPropertiesButtonCapsule {
		public OffsetEdgesButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("OffsetEdges", Resources.OffsetEdgesText, null, Resources.OffsetEdgesHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			base.OnExecute(command, context, buttonRect);

			ICollection<IDesignEdge> designEdges = Window.ActiveWindow.ActiveContext.GetSelection<IDesignEdge>();
			List<ITrimmedCurve> trimmedCurves = new List<ITrimmedCurve>();
			Dictionary<ITrimmedCurve, IDesignEdge> curveToEdge = new Dictionary<ITrimmedCurve, IDesignEdge>();

			foreach (IDesignEdge designEdge in designEdges) {
				ITrimmedCurve trimmedCurve = designEdge.Shape;
				trimmedCurves.Add(trimmedCurve);
				curveToEdge[trimmedCurve] = designEdge;
			}

			TrimmedCurveChain curveChain = new TrimmedCurveChain(trimmedCurves);

			Point startPoint = curveChain.Curves[0].StartPoint;
			Point startTail = startPoint + GetAverageNormal(startPoint, curveToEdge[curveChain.Curves[0].TrimmedCurve]) * edgeOffset;

			for (int i = 0; i < curveChain.Curves.Count; i++) {
				Point endPoint = curveChain.Curves[i].EndPoint;
				Point endTail = endPoint + GetAverageNormal(endPoint, curveToEdge[curveChain.Curves[i].TrimmedCurve]) * edgeOffset;

				if (i != curveChain.Curves.Count - 1)
					endTail = new Point[] { endTail, endPoint + GetAverageNormal(endPoint, curveToEdge[curveChain.Curves[i + 1].TrimmedCurve]) * edgeOffset }.Average();

				CurveSegment curveSegment = CurveSegment.Create(startTail, endTail);
				DesignCurve curve = DesignCurve.Create(activeWindow.ActiveContext.Context as Part, curveSegment);

				startPoint = endPoint;
				startTail = endTail;
			}
		}

		static Direction GetAverageNormal(Point point, IDesignEdge designEdge) {
			Vector startNormal = Vector.Zero;
			SurfaceEvaluation surfEval = null;

			foreach (IDesignFace designFace in designEdge.Faces) {
				surfEval = designFace.Shape.Geometry.ProjectPoint(point);
				startNormal += surfEval.Normal.UnitVector;
			}

			return startNormal.Direction;
		}
	}

	class MakeTabsButtonCapsule : TabPropertiesButtonCapsule {
		public MakeTabsButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("MakeTabs", Resources.MakeTabsText, null, Resources.MakeTabsHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			base.OnExecute(command, context, buttonRect);

			Window activeWindow = Window.ActiveWindow;

			List<Window> curveWindows = new List<Window>(Document.Open(@"C:\Users\bcr\Documents\Models\Pod Tent\TabStrap.scdoc", ImportOptions.Create()));
			List<DesignBody> designBodies = new List<DesignBody>((curveWindows[0].Scene as Part).Bodies);
			Body tabBody = designBodies[0].Shape;
			Debug.Assert(tabBody != null);

			ICollection<IDesignEdge> designEdges = activeWindow.ActiveContext.GetSelection<IDesignEdge>();

			foreach (IDesignEdge designEdge in designEdges) {
				Double scale = designEdge.Shape.Length / (1 * inches);
				Edge edge = designEdge.Shape as Edge;
				Direction edgeDir = (edge.EndPoint - edge.StartPoint).Direction;

				List<Face> faces = new List<Face>(edge.Faces);
				Face face = faces[0];

				SurfaceEvaluation surfEval = face.ProjectPoint(edge.StartPoint);
				Direction normal = surfEval.Normal;
				if (areTabsFlipped)
					normal = -normal;

				Body body = tabBody.Copy();
				DesignBody designBody = DesignBody.Create(activeWindow.ActiveContext.Context as Part, "Tab", body);

				designBody.Scale(Frame.World, scale, 1, 1);
				Matrix transform = Matrix.CreateMapping(Frame.Create(edge.StartPoint, edgeDir, Direction.Cross(normal, edgeDir)));
				designBody.Transform(transform);

			}

			foreach (Window window in curveWindows)
				window.Delete();

		}
	}
}
