using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Linq;
using System.Text.RegularExpressions;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.Api.V10.Display;
using Application = SpaceClaim.Api.V10.Application;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.AETools {

	/* Open Code V CCP files.
	HEX_NUMCOMP | Number of elements
	ENVELOPE    | "YES" or "NO" are rays an envelope or individual?
	NUM_RAYS    | Number of rays
	F01ADX      | Front  Aperture X-decenter
	F01ADY      | Front  Aperture Y-decenter
	F01AX       | Front  Aperture X-semi-size
	F01AY       | Front  Aperture Y-semi-size
	F01ARR      | Front  Aperture Racetrack Radius about each corner
	F01X        | Front  Origin X-location
	F01Y        | Front  Origin Y-location
	F01Z        | Front  Origin Z-location
	F01A        | Front  Orientation Alpha angle (not Euler)
	F01B        | Front  Orientation Beta angle (not Euler)
	F01M        | Front  Orientation Gamma angle (not Euler)
	F01C        | Front  Surface Curvature
	F01K        | Front  Surface Conic Constant
	F01D        | Front  4th order Aspheric  AD
	F01E        | Front  6th order Aspheric  AE
	F01F        | Front  8th order Aspheric  AF
	F01G        | Front  10th order Aspheric AG
	F01TYP      | Name of material between front and back surfaces
	F01DEN      | Density of material
	B01ADX      | Back    Aperture X-decenter 
	 B01ADY      | Back    Aperture Y-decenter
	B01AX       | Back    Aperture X-semi-size
	B01AY       | Back    Aperture Y-semi-size
	B01ARR      | Back    Aperture Racetrack Radius about each corner
	B01X        | Back    Origin X-location
	B01Y        | Back    Origin Y-location
	B01Z        | Back    Origin Z-location
	B01A        | Back    Orientation Alpha angle (not Euler)
	B01B        | Back    Orientation Beta angle (not Euler)
	B01M        | Back    Orientation Gamma angle (not Euler)
	B01C        | Back    Surface Curvature
	B01K        | Back    Surface Conic Constant
	B01D        | Back    4th order Aspheric  AD
	B01E        | Back    6th order Aspheric  AE
	B01F        | Back    8th order Aspheric  AF
	B01G        | Back    10th order Aspheric AG
	F01X01      | Ray 1, X-location on Front surface of element 1
	F01Y01      | Ray 1, Y-location on Front surface of element 1
	F01Z01      | Ray 1, Z-location on Front surface of element 1
	B01X01      | Ray 1, X-location on Back  surface of element 1
	B01Y01      | Ray 1, Y-location on Back  surface of element 1 
	 B01Z01      | Ray 1, Z-location on Back  surface of element 1
	. . . . .
	F09X64      | Ray 64, X-location on Front surface of element 9
	F09Y64      | Ray 64, Y-location on Front surface of element 9
	F09Z64      | Ray 64, Z-location on Front surface of element 9
	B09X64      | Ray 64, X-location on Back  surface of element 9
	B09Y64      | Ray 64, Y-location on Back  surface of element 9
	B09Z64      | Ray 64, Z-location on Back  surface of element 9
	 */

	class CodeVOpenHandler : FileOpenHandler {
		public CodeVOpenHandler()
			: base("Code V Raytrace Data", "ccp") {
		}

		public override void OpenFile(string path, Window targetWindow) {
			CcpFile file = new CcpFile(path);
		}
	}

	class CcpFile {
		public CcpFile(String path) {
			Elements = new List<CppElement>();

			StreamReader file = File.OpenText(path);

			string line;
			var profiles = new List<List<Point>>();
			Match match;
			while (!file.EndOfStream) {
				line = file.ReadLine();

				// Comment lines
				if (Regex.Match(line, @"^/\*").Success)
					continue;

				match = Regex.Match(line, @"HEX_NUMCOMP\s*=\s*(\d+)");
				if (match.Success) {
					int components;
					if (int.TryParse(match.Groups[1].Value, out components)) {
						Components = components;
						for (int i = 0; i < components; i++)
							Elements.Add(new CppElement());

						continue;
					}

					Warn(line);
					break;

				}

				match = Regex.Match(line, @"NUM_RAYS\s*=\s*(\d+)");
				if (match.Success) {
					int rays;
					if (int.TryParse(match.Groups[1].Value, out rays)) {
						Rays = rays;
						continue;
					}

					Warn(line);
					break;
				}

				match = Regex.Match(line, @"ENVELOPE\s*=\s*(\w+)");
				if (match.Success) {
					string value = match.Groups[1].Value;
					if (value == "YES") {
						IsEnvelope = true;
						continue;
					}

					if (value == "NO") {
						IsEnvelope = false;
						continue;
					}

					Warn(line);
					break;
				}

				match = Regex.Match(line, @"F(\d\d)TYP\s*=\s*""(.+)""");
				if (match.Success) {
					int index;
					if (int.TryParse(match.Groups[1].Value, out index)) {
						Elements[index - 1].Type = match.Groups[2].Value;
						Elements[index - 1].Type = Elements[index - 1].Type.Replace(' ', '"');
						continue;
					}

					Warn(line);
					break;
				}

				match = Regex.Match(line, @"F(\d\d)DEN\s*=\s*""(\d+)""");
				if (match.Success) {
					int index;
					double value;
					if (
						int.TryParse(match.Groups[1].Value, out index) &&
						double.TryParse(match.Groups[2].Value, out value)
					) {
						Elements[index - 1].Density = value / 1000;  // TBD guessing that density units are also scaled to mm
						continue;
					}

					Warn(line);
					break;
				}

				match = Regex.Match(line, @"([FB])(\d\d)(\w+)\s*=\s*([-\d\.]+)");
				if (match.Success) {
					string side = match.Groups[1].Value;
					int index;
					string key = match.Groups[3].Value;
					double value;
					if ((side == "F" || side == "B") &&
						int.TryParse(match.Groups[2].Value, out index) &&
						double.TryParse(match.Groups[4].Value, out value)
					) {
						CppSideData sideData = side == "F" ? Elements[index - 1].Front : Elements[index - 1].Back;
						double units;
						if (key == "A" || key == "B" || key == "M")
							units = -Math.PI / 180; // angular units are specified in degrees (TBD sign only tested for A rotations, may be different for others)
						else
							units = 0.001; // linear units are specified in mm

						sideData.ElementValues[key] = value * units;
						continue;
					}

					Warn(line);
					break;
				}
			}

			WriteBlock.ExecuteTask("Import CCP data", CreateGeometry);
		}

		private void CreateGeometry() {
			Matrix frontTransform = Matrix.Identity; // TBD example data had no transforms (or much of anything) on back faces, so we don't know whether the transforms are separate yet
			Matrix backTransform = Matrix.Identity;
			for (int i = 0; i < Elements.Count; i++) {
				frontTransform *= Elements[i].Front.Transform;
				Elements[i].Front.GenerateGeometry(Rays, frontTransform);

				backTransform *= Elements[i].Back.Transform;
				Elements[i].Back.GenerateGeometry(Rays, backTransform);
			}

			Document doc = Document.Create();
			Part mainPart = doc.MainPart;

			for (int i = 0; i < Elements.Count; i++) {
				Part part = Part.Create(doc, string.Format("F{0:00} - {1}", i + 1, Elements[i].Type));
				Component.Create(mainPart, part);

				foreach (Body body in new CppSideData[] { Elements[i].Front, Elements[i].Back }.SelectMany(s => s.Bodies))
					DesignBody.Create(mainPart, "Surface", body);

				//for (int j = 0; j < Rays; j++)
				//    CreateDesignCurveIfPointsSeparate(mainPart, Elements[i].Front.RayPoints[j], Elements[i].Back.RayPoints[j]);
			}

			for (int i = 0; i < Elements.Count - 1; i++)
				for (int j = 0; j < Rays; j++)
					//CreateDesignCurveIfPointsSeparate(mainPart, Elements[i].Back.RayPoints[j], Elements[i + 1].Front.RayPoints[j]);
					CreateDesignCurveIfPointsSeparate(mainPart, Elements[i].Front.RayPoints[j], Elements[i + 1].Front.RayPoints[j]);

		}

		private void CreateDesignCurveIfPointsSeparate(Part part, Point a, Point b) {
			if (a != b) // compares within modeling tolerance
				DesignCurve.Create(part, CurveSegment.Create(a, b));
		}

		private void Warn(string line) {
			Application.ReportStatus("Error with line: " + line, StatusMessageType.Error, null);
		}

		public int Components { get; set; }
		public int Rays { get; set; }
		public bool IsEnvelope { get; set; }
		public List<CppElement> Elements { get; set; }
	}

	class CppElement {
		public CppElement() {
			Front = new CppSideData();
			Back = new CppSideData();
		}

		public string Type { get; set; }
		public double Density { get; set; }
		public CppSideData Front { get; set; }
		public CppSideData Back { get; set; }
	}

	class CppSideData {
		List<Point> rayPoints;

		public CppSideData() {
			ElementValues = new Dictionary<string, double>();
			Bodies = new List<Body>();
		}

		public void GenerateGeometry(int rays, Matrix pointTransform) {
			GenerateTransform();
			GenerateBodies();
			GeneratePoints(rays, pointTransform);
		}

		private void GenerateTransform() {
			Matrix translation = Matrix.CreateTranslation(Vector.Create(
				ElementValues["X"],
				ElementValues["Y"],
				ElementValues["Z"]
			));

			Matrix rotationX = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirX), ElementValues["A"]);
			Matrix rotationY = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirY), ElementValues["B"]);
			Matrix rotationZ = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), ElementValues["M"]);

			Transform = translation * rotationX * rotationY * rotationZ;
		}

		private void GenerateBodies() {
			double halfWidth = ElementValues["AX"];
			double halfDepth = ElementValues["AY"];

			Body body;
			if (!Accuracy.LengthIsZero(halfWidth * 2) && !Accuracy.LengthIsZero(halfDepth * 2)) {
				body = CreateFace(Point.Origin, halfWidth, halfDepth);
				body.Transform(Transform);
				Bodies.Add(body);

				double curvature = ElementValues["C"] * 1000000;  // we read all values as mm and converted them to meters, but the units for curvature are 1/mm
				double thickness = ElementValues["K"];
				if (!Accuracy.LengthIsZero(curvature) && !Accuracy.LengthIsZero(thickness)) {  // TBD figure out how to establish thickness when it is zero
					body = CreateSphericalSegment(Point.Origin, Direction.DirZ, 1 / curvature, halfWidth, thickness);
					body.Transform(Transform);
					Bodies.Add(body);
				}
			}
		}

		private static Body CreateFace(Point center, double halfWidth, double halfDepth) {
			Point[] points = new Point[4];
			Vector offsetX = Vector.Create(halfDepth, 0, 0);
			Vector offsetY = Vector.Create(0, halfWidth, 0);

			points[0] = center + offsetX + offsetY;
			points[1] = center - offsetX + offsetY;
			points[2] = center - offsetX - offsetY;
			points[3] = center + offsetX - offsetY;

			Plane plane = Plane.PlaneXY;
			return Body.CreatePlanarBody(plane, new ITrimmedCurve[] {
				CurveSegment.Create(points[0], points[1]),
				CurveSegment.Create(points[1], points[2]),
				CurveSegment.Create(points[2], points[3]),
				CurveSegment.Create(points[3], points[0])
			});
		}

		private static Body CreateSphericalSegment(Point tangent, Direction normal, double radius, double halfWidth, double thickness) {
			Point center = tangent + normal * radius;
			radius = Math.Abs(radius);
			Plane plane = Plane.Create(Frame.Create(center, normal, normal.ArbitraryPerpendicular));
			Circle circle = Circle.Create(plane.Frame, radius);
			CurveSegment circleCurve = CurveSegment.Create(circle, Interval.Create(0, Math.Asin(halfWidth / radius)));

			Point rearCenter = tangent - normal * thickness;
			Point rearUpper = rearCenter + plane.Frame.DirY * halfWidth;

			var profile = new List<ITrimmedCurve>();
			profile.Add(circleCurve);
			profile.Add(CurveSegment.Create(rearCenter, tangent));
			profile.Add(CurveSegment.Create(rearCenter, rearUpper));
			profile.Add(CurveSegment.Create(rearUpper, circleCurve.EndPoint));

			foreach (ITrimmedCurve curve in profile)
				DesignCurve.Create(Window.ActiveWindow.Scene as Part, curve);

			ITrimmedCurve path = CurveSegment.Create(Circle.Create(Frame.Create(tangent, normal), 1));
			return Body.SweepProfile(plane, profile, new ITrimmedCurve[] { path });
//			return Body.CreatePlanarBody(plane, new ITrimmedCurve[] { CurveSegment.Create(circle) });
//			return ShapeHelper.CreateCircle(plane.Frame, radius * 2, Window.ActiveWindow.Scene as Part).Shape.Copy();
		}

		private void GeneratePoints(int count, Matrix transform) {
			rayPoints = new List<Point>(count);
			for (int i = 1; i <= count; i++) {
				string num = string.Format("{0:00}", i);
				rayPoints.Add(Transform * Point.Create(
					ElementValues["X" + num],
					ElementValues["Y" + num],
					ElementValues["Z" + num]
				));
			}
		}

		public Dictionary<string, double> ElementValues { get; set; }
		public List<Point> RayPoints {
			get { return rayPoints; }
		}

		public Matrix Transform { get; set; }
		public Point Center { get; set; }
		public IList<Body> Bodies { get; set; }
	}
}
