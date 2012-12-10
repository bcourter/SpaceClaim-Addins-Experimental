using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using Application = SpaceClaim.Api.V10.Application;

namespace SpaceClaim.AddIn.AETools {

	/* Open raw Bezier patches from files that are specified by an index format that resembles this:
32
1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16
4,17,18,19,8,20,21,22,12,23,24,25,16,26,27,28
19,29,30,31,22,32,33,34,25,35,36,37,28,38,39,40
31,41,42,1,34,43,44,5,37,45,46,9,40,47,48,13
...
306
1.4,0.0,2.4
1.4,-0.784,2.4
0.784,-1.4,2.4
0.0,-1.4,2.4
...
	 */

	class BezierOpenHandler : FileOpenHandler {
		public BezierOpenHandler()
			: base("Bezier Data", "txt") {
		}
        
        public override void OpenFile(string path, Window targetWindow) {
			BezierPatches file = new BezierPatches(path);
		}
	}

	class BezierPatches {
		int[][,] patchIndices;
		Point[] vertices;

		public BezierPatches(String path) {
			StreamReader file = File.OpenText(path);
			string line;
			Match match;

			// Patches indices
			int patchCount;
			line = file.ReadLine();
			match = Regex.Match(line, @"(\d+)");
			if (!match.Success) {
				Warn(line);
				return;
			}

			if (!int.TryParse(match.Groups[1].Value, out patchCount)) {
				Warn(line);
				return;
			}

			patchIndices = new int[patchCount][,];
			for (int i = 0; i < patchCount; i++) {
				if (file.EndOfStream) {
					Warn("Premature end of file");
					return;
				}

				line = file.ReadLine();

				match = Regex.Match(line, @"(\d+),(\d+),(\d+),(\d+),(\d+),(\d+),(\d+),(\d+),(\d+),(\d+),(\d+),(\d+),(\d+),(\d+),(\d+),(\d+)");
				if (!match.Success) {
					Warn(line);
					return;
				}

				int i00, i01, i02, i03;
				int i10, i11, i12, i13;
				int i20, i21, i22, i23;
				int i30, i31, i32, i33;
				if (
					int.TryParse(match.Groups[1].Value, out i00) && int.TryParse(match.Groups[2].Value, out i01) && int.TryParse(match.Groups[3].Value, out i02) && int.TryParse(match.Groups[4].Value, out i03) &&
					int.TryParse(match.Groups[5].Value, out i10) && int.TryParse(match.Groups[6].Value, out i11) && int.TryParse(match.Groups[7].Value, out i12) && int.TryParse(match.Groups[8].Value, out i13) &&
					int.TryParse(match.Groups[9].Value, out i20) && int.TryParse(match.Groups[10].Value, out i21) && int.TryParse(match.Groups[11].Value, out i22) && int.TryParse(match.Groups[12].Value, out i23) &&
					int.TryParse(match.Groups[13].Value, out i30) && int.TryParse(match.Groups[14].Value, out i31) && int.TryParse(match.Groups[15].Value, out i32) && int.TryParse(match.Groups[16].Value, out i33)
					) {
					patchIndices[i] = new int[,] {
						{i00, i01, i02, i03},
						{i10, i11, i12, i13},
						{i20, i21, i22, i23},
						{i30, i31, i32, i33}
					};

					continue;
				}

				Warn(line);
				return;
			}

			// vertices
			int vertexCount;
			line = file.ReadLine();
			match = Regex.Match(line, @"(\d+)");
			if (!match.Success) {
				Warn(line);
				return;
			}

			if (!int.TryParse(match.Groups[1].Value, out vertexCount)) {
				Warn(line);
				return;
			}

			vertices = new Point[vertexCount];
			for (int i = 0; i < vertexCount; i++) {
				if (file.EndOfStream) {
					Warn("Premature end of file");
					return;
				}

				line = file.ReadLine();

				match = Regex.Match(line, @"([-\d\.]+),([-\d\.]+),([-\d\.]+)");
				if (!match.Success) {
					Warn(line);
					return;
				}

				double x, y, z;
				if (double.TryParse(match.Groups[1].Value, out x) && double.TryParse(match.Groups[2].Value, out y) && double.TryParse(match.Groups[3].Value, out z)) {
					vertices[i] = Point.Create(x, y, z);
					continue;
				}

				Warn(line);
				return;
			}

			WriteBlock.ExecuteTask("Create Patches", CreateGeometry);
		}

		private void CreateGeometry() {
			Window activeWindow = Window.ActiveWindow;
			Part mainPart = activeWindow.Scene as Part;
			if (mainPart == null)
				return;

			List<Body> bodies = new List<Body>();

			foreach (int[,] patchData in patchIndices) {
				ControlPoint[,] controlPoints = new ControlPoint[4, 4];
				for (int i = 0; i < 4; i++) {
					for (int j = 0; j < 4; j++)
						controlPoints[i, j] = new ControlPoint(vertices[patchData[i, j] - 1], 1);
				}

				IDictionary<string, Face> idToFace;
				IDictionary<string, Edge> idToEdge;
				bodies.Add(Body.Import(new BezierPatchForeignBody(controlPoints), out idToFace, out idToEdge));
				if (bodies[bodies.Count - 1].Faces.Count == 0) {
					for (int i = 0; i < 4; i++) {
						for (int j = 0; j < 4; j++)
							DesignCurve.Create(mainPart, CurveSegment.Create(PointCurve.Create(vertices[patchData[i, j] - 1])));
					}
				}

			}

			foreach (Body body in bodies) {
				if (body.Faces.Count > 0)
					DesignBody.Create(mainPart, "Bezier", body);
			}

			activeWindow.InteractionMode = InteractionMode.Solid;
			activeWindow.ZoomExtents();
		}

		private void Warn(string line) {
			Application.ReportStatus("Error with line: " + line, StatusMessageType.Error, null);
		}
	}


	#region BezierPatch foreign topology wrappers

	class BezierPatchForeignBody : ForeignBody {
		ControlPoint[,] vertices;

		public BezierPatchForeignBody(ControlPoint[,] vertices) {
			Debug.Assert(vertices != null);
			this.vertices = vertices;
		}

		#region Housekeeping

		public override bool Equals(ForeignBody other) {
			if (other == null)
				return false;
			if (other == this)
				return true;

			var otherBody = other as BezierPatchForeignBody;
			return otherBody != null && otherBody.vertices == vertices;
		}

		public override int GetHashCode() {
			return vertices.GetHashCode();
		}

		#endregion

		/*
		 * BezierPatch doesn't have the concept of multiple lumps, so we pass the
		 * IBezierPatchBody down to a single lump.
		 */
		public override ICollection<ForeignLump> Lumps {
			get { return new ForeignLump[] { new BezierPatchForeignLump(this, vertices) }; }
		}
	}

	class BezierPatchForeignLump : ForeignLump {
		ControlPoint[,] vertices;

		public BezierPatchForeignLump(BezierPatchForeignBody parent, ControlPoint[,] vertices)
			: base(parent) {
			Debug.Assert(vertices != null);
			this.vertices = vertices;
		}

		#region Housekeeping

		public override bool Equals(ForeignLump other) {
			if (other == null)
				return false;
			if (other == this)
				return true;

			var otherLump = other as BezierPatchForeignLump;
			return otherLump != null && otherLump.vertices == vertices;
		}

		public override int GetHashCode() {
			return vertices.GetHashCode();
		}

		#endregion

		/*
		 * BezierPatch doesn't have the concept of multiple shells, so we pass the
		 * IBezierPatchBody down to a single shell.
		 */
		public override ICollection<ForeignShell> Shells {
			get { return new ForeignShell[] { new BezierPatchForeignShell(this, vertices) }; }
		}
	}

	class BezierPatchForeignShell : ForeignShell {
		ControlPoint[,] vertices;

		public BezierPatchForeignShell(ForeignLump parent, ControlPoint[,] vertices)
			: base(parent) {
			Debug.Assert(vertices != null);
			this.vertices = vertices;
		}

		#region Housekeeping

		public override bool Equals(ForeignShell other) {
			if (other == null)
				return false;
			if (other == this)
				return true;

			var otherShell = other as BezierPatchForeignShell;
			return otherShell != null && otherShell.vertices == vertices;
		}

		public override int GetHashCode() {
			return vertices.GetHashCode();
		}

		#endregion

		/*
		 * Since our BezierPatch API happens to present information in exactly
		 * the form required, this code is simpler than it might otherwise be.
		 */
		public override ICollection<ForeignFace> Faces {
			get {
				var faces = new List<ForeignFace>(1);
				faces.Add(new BezierPatchForeignFace(this, vertices));
				return faces;
			}
		}
	}

	class BezierPatchForeignFace : ForeignFace {
		ControlPoint[,] vertices;
		NurbsSurface surface;

		public BezierPatchForeignFace(ForeignShell parent, ControlPoint[,] vertices)
			: base(parent) {
			Debug.Assert(vertices != null);
			this.vertices = vertices;

			NurbsData dataU = new NurbsData(4, false, false, new Knot[] { new Knot(0, 4), new Knot(1, 4) });
			NurbsData dataV = new NurbsData(4, false, false, new Knot[] { new Knot(0, 4), new Knot(1, 4) });

			surface = NurbsSurface.Create(dataU, dataV, vertices);
		}

		#region Housekeeping

		public override bool Equals(ForeignFace other) {
			if (other == null)
				return false;
			if (other == this)
				return true;

			var otherFace = other as BezierPatchForeignFace;
			return otherFace != null && otherFace.surface == surface;
		}

		public override int GetHashCode() {
			return surface.GetHashCode();
		}

		#endregion

		public override ICollection<ForeignLoop> Loops {
			get {
				var loops = new List<ForeignLoop>(1);
				loops.Add(new BezierPatchForeignLoop(this));
				return loops;
			}
		}

		/*
		 * Since our BezierPatch API happens to present information in exactly
		 * the form required, the code for this property is unrealistically simple.
		 * In practice, the foreign surface would have to be converted into the
		 * corresponding Surface object.
		 */
		public override Surface Surface {
			get { return surface; }
		}

		/*
		 * Whether the face normal is opposite to its surface normal (e.g. true
		 * for a cylindrical hole, but false for a cylindrical bar).
		 */
		public override bool IsReversed {
			get { return false; }
		}

		public ControlPoint[,] Vertices {
			get { return vertices; }
		}
	}

	class BezierPatchForeignLoop : ForeignLoop {
		public BezierPatchForeignLoop(ForeignFace parent)
			: base(parent) {
		}

		#region Housekeeping

		public override bool Equals(ForeignLoop other) {
			return other == this;
		}

		public override int GetHashCode() {
			return this.GetHashCode();
		}

		#endregion

		/*
		 * Fins must be returned in the correct order around the loop.  The loop
		 * direction must be clockwise about the face normal, or counterclockwise
		 * looking down onto the face (for an outer loop), if you prefer to think
		 * about it that way.
		 */
		public override ICollection<ForeignFin> Fins {
			get {
				var fins = new List<ForeignFin>();
				ControlPoint[,] facePoints = ((BezierPatchForeignFace) Face).Vertices;
				int i = 0;

				if (facePoints[0, 0].Position != facePoints[3, 0].Position)
					fins.Add(new BezierPatchForeignFin(this, new ControlPoint[] { facePoints[0, 0], facePoints[1, 0], facePoints[2, 0], facePoints[3, 0] }, i++));
				if (facePoints[3, 0].Position != facePoints[3, 3].Position)
					fins.Add(new BezierPatchForeignFin(this, new ControlPoint[] { facePoints[3, 0], facePoints[3, 1], facePoints[3, 2], facePoints[3, 3] }, i++));
				if (facePoints[3, 3].Position != facePoints[0, 3].Position)
					fins.Add(new BezierPatchForeignFin(this, new ControlPoint[] { facePoints[3, 3], facePoints[2, 3], facePoints[1, 3], facePoints[0, 3] }, i++));
				if (facePoints[0, 3].Position != facePoints[0, 0].Position)
					fins.Add(new BezierPatchForeignFin(this, new ControlPoint[] { facePoints[0, 3], facePoints[0, 2], facePoints[0, 1], facePoints[0, 0] }, i++));
				return fins;
			}
		}
	}

	class BezierPatchForeignFin : ForeignFin {
		ControlPoint[] points;
		int index;

		public BezierPatchForeignFin(ForeignLoop parent, ControlPoint[] points, int index)
			: base(parent) {
			Debug.Assert(points != null);
			this.points = points;
			this.index = index;
		}

		#region Housekeeping

		public override bool Equals(ForeignFin other) {
			if (other == null)
				return false;
			if (other == this)
				return true;

			var otherFin = other as BezierPatchForeignFin;
			return otherFin != null && otherFin.points == points;
		}

		public override int GetHashCode() {
			return points.GetHashCode();
		}

		#endregion

		/*
		 * Whether the fin direction (consistent with the loop direction) is opposite
		 * to its edge direction.
		 */
		public override bool IsReversed {
			get { return false; }
		}

		public override ForeignEdge Edge {
			get { return new BezierPatchForeignEdge(Loop.Face.Shell, points, index); }
		}
	}

	class BezierPatchForeignEdge : ForeignEdge {
		NurbsCurve nurbsCurve;
		int index;

		public BezierPatchForeignEdge(ForeignShell parent, ControlPoint[] points, int index)
			: base(parent) {
			Debug.Assert(points != null);

			NurbsData data = new NurbsData(4, false, false, new Knot[] { new Knot(0, 4), new Knot(1, 4) });
			nurbsCurve = NurbsCurve.CreateFromControlPoints(data, points);
			this.index = index;
		}

		#region Housekeeping

		public override bool Equals(ForeignEdge other) {
			if (other == null)
				return false;
			if (other == this)
				return true;

			var otherEdge = other as BezierPatchForeignEdge;
			return otherEdge != null && otherEdge.nurbsCurve == nurbsCurve;
		}

		public override int GetHashCode() {
			return nurbsCurve.GetHashCode();
		}

		#endregion

		/*
		 * As with BezierPatchForeignFace.Surface, this is unrealistically simple, and
		 * a realistic case would involve geometry conversion.
		 */
		public override Curve Curve {
			get { return nurbsCurve; }
		}

		/*
		 * Whether the edge direction is opposite to its curve direction.
		 */
		public override bool IsReversed {
			get { return false; }
		}

		/*
		 * The sense of 'start' and 'end' is in terms of the edge direction.  The edge
		 * might have two distinct vertices (most common), it might have the same vertex
		 * at each end (unusual but valid), or it might have no vertex (return null) at
		 * both ends (e.g. the ends of a cylindrical bar).  It cannot have a vertex at
		 * one end, but not at the other.
		 */
		public override ForeignVertex StartVertex {
			get {
				return new BezierPatchForeignVertex(Shell, nurbsCurve.ControlPoints[0].Position, index);
			}
		}

		public override ForeignVertex EndVertex {
			get {
				return new BezierPatchForeignVertex(Shell, nurbsCurve.ControlPoints[3].Position, (index + 1) % 4);
			}
		}
	}

	class BezierPatchForeignVertex : ForeignVertex {
		Point position;
		int index;

		public BezierPatchForeignVertex(ForeignShell parent, Point position, int index)
			: base(parent) {
			Debug.Assert(position != null);
			this.position = position;
			this.index = index;
		}

		#region Housekeeping

		public override bool Equals(ForeignVertex other) {
			if (other == null)
				return false;
			if (other == this)
				return true;

			var otherVertex = other as BezierPatchForeignVertex;
			return otherVertex != null && otherVertex.index == index;
		}

		public override int GetHashCode() {
			return position.X.GetHashCode() ^ position.Y.GetHashCode() ^ position.Y.GetHashCode();
		//	return index;
		}

		#endregion

		public override Point Position {
			get { return position; }
		}
	}

	#endregion

}
