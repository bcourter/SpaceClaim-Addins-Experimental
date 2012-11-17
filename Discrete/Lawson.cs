using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Display;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.Discrete {
	public class Lawson {
		double p = 0.5;
		double q = 1;
		double circleAngle = Math.PI / 2;
		Vector inverseOffset = Vector.Create(0.5, 0, 0);

		const int iSteps = 24;
		const int jSteps = 48;
		const double scale = 1;// 0.01;

		public double AngleForce { get; set; }
		public double VForce { get; set; }
		public double UAngleForce { get; set; }
		public double UTouchForce { get; set; }
		public double AverageVForce { get; set; }
		public double AverageTabAngleForce { get; set; }

		CircularList<CircularList<double>> vParameters = new CircularList<CircularList<double>>(iSteps * 2);
		CircularList<CircularList<double>> tabAngles = new CircularList<CircularList<double>>(iSteps * 2);
		CircularList<CircularList<Point>> points = new CircularList<CircularList<Point>>(iSteps * 2);

		public int Iteration { get; set; }
		public double CumulativeError { get; set; }
		public double MaxError { get; set; }

		public Lawson(double angleForce, double vForce, double uAngleForce, double uTouchForce, double averageVForce, double averageTabAngleForce) {
			this.AngleForce = angleForce;
			this.VForce = vForce;
			this.UTouchForce = uTouchForce;
			this.UAngleForce = uAngleForce;
			this.AverageVForce = averageVForce;
			this.AverageTabAngleForce = averageTabAngleForce;
			Iteration = 0;

			Reset();
		}

		public void Reset() {
			for (int i = 0; i < iSteps * 2; i++) {
				double u = 2 * Math.PI * (double) i / iSteps;
				vParameters.Add(new CircularList<double>(jSteps));
				tabAngles.Add(new CircularList<double>(jSteps));
				points.Add(new CircularList<Point>(jSteps));
				for (int j = 0; j < jSteps; j++) {
					vParameters[i].Add(2 * Math.PI * ((double) j - 0.5) / jSteps);
					//	tabAngles[i].Add((double) 5 / 6 * Math.PI);
					tabAngles[i].Add((double) Math.PI);
					points[i].Add(Evaluate(PointUV.Create(vParameters[i][j], u), p, q, circleAngle, inverseOffset, true) * scale);
				}
			}

			//for (int i = 0; i < iSteps * 2; i++) {
			//    double u = 2 * Math.PI * (double) i / iSteps;
			//    tabAngles.Add(new CircularList<double>(jSteps));
			//    for (int j = 0; j < jSteps; j++) {
			//        int iOther = (j % 2 != 0) ? i - 1 : i + 1;
			//        double l = (points[i][j + 1] - points[i][j]).Magnitude;
			//        double d = (new[] { points[i][j + 1], points[i][j] }.Average() -
			//                    new[] { points[iOther][j + 1], points[iOther][j] }.Average()).Magnitude;

			//        double angle = Math.PI - 2 * Math.Asin(l * d / (Math.Pow(l / 2, 2) + Math.Pow(d, 2)));
			//        tabAngles[i][j] = angle;
			//    }
			//}
		}

		public void Iterate() {
			Iteration++;

			var newVParameters = new CircularList<CircularList<double>>(iSteps * 2);
			var backupVParameters = new CircularList<CircularList<double>>(iSteps * 2);
			var newTabAngles = new CircularList<CircularList<double>>(iSteps * 2);

			for (int i = 0; i < iSteps * 2; i++) {
				newVParameters.Add(new CircularList<double>(jSteps));
				backupVParameters.Add(new CircularList<double>(jSteps));
				newTabAngles.Add(new CircularList<double>(jSteps));
				for (int j = 0; j < jSteps; j++) {
					newVParameters[i].Add(vParameters[i][j]);
					backupVParameters[i].Add(vParameters[i][j]);
					newTabAngles[i].Add(tabAngles[i][j]);
				}
			}

			double cumulativeErrorTally = 0;
			MaxError = 0;
			for (int i = 0; i < iSteps * 2; i++) {
				for (int j = 0; j < jSteps; j++) {
					bool swap = j % 2 == 0;
					int iOtherDir = swap ? -1 : 1;
					int iOther = i + iOtherDir;

					Circle baseCircle = GetTabFromPoints(true, i, j, iOther);
					Circle circle0 = GetTabFromPoints(true, iOther, j - 1, i);
					Circle circle1 = GetTabFromPoints(true, iOther, j + 1, i);

					double distance0 = (baseCircle.Frame.Origin - circle0.Frame.Origin).Magnitude - baseCircle.Radius - circle0.Radius;
					double distance1 = (baseCircle.Frame.Origin - circle1.Frame.Origin).Magnitude - baseCircle.Radius - circle1.Radius;
					cumulativeErrorTally += distance0 * distance0 + distance1 * distance1;
					MaxError = Math.Max(Math.Abs(distance0), MaxError);
					MaxError = Math.Max(Math.Abs(distance1), MaxError);

					double angleAdjust = (distance0 + distance1) / 2 * AngleForce / GetSpanAt(i, j) / baseCircle.Radius;// / GetSpanAt(i, j) / baseCircle.Radius; // / baseCircle.Radius;
					newTabAngles[i][j] -= angleAdjust;

					newTabAngles[i][j - 1] -= angleAdjust / 2;
					newTabAngles[i][j + 1] -= angleAdjust / 2;

					double iAngle = AddInHelper.AngleBetween(
						circle0.Frame.Origin - baseCircle.Frame.Origin,
						circle1.Frame.Origin - baseCircle.Frame.Origin
					);

					double vOffset = (distance1 - distance0) * VForce;
					//double uAngleOffset = -(Math.PI / 2 - Math.Abs(iAngle)) * UAngleForce;
					double uAngleOffset = -(Math.PI * 2 / 3 - Math.Abs(iAngle)) * UAngleForce;

					Circle circleV = GetTabFromPoints(true, iOther, j, i);
					double distanceV = (baseCircle.Frame.Origin - circleV.Frame.Origin).Magnitude - baseCircle.Radius - circleV.Radius;
					double uTouchOffset = -distanceV * UTouchForce;

					double averageOffset = 0;
					double averageAngle = 0;
					double count = 0;

#if false
					for (int ii = i - 1; ii <= i + 1; ii++) {
					    for (int jj = j - 2; jj <= j + 2; jj++) {
							if (i == ii && j == jj) 
								continue;

							double weight =  (double) 1 / (Math.Pow(i - ii, 2) + Math.Pow(j - jj, 2));  
							averageOffset += GetSpanAt(ii, jj) * weight;
							averageAngle += GetTabFromPoints(true, i, j, j % 2 == 0 ? i - 1 : i + 1).Radius * weight;
							count += weight;
						}
					}

					averageOffset = averageOffset / count - GetSpanAt(i, j);
					averageOffset *= -AverageVForce / 2;

					averageAngle = averageAngle / count - baseCircle.Radius;
					newTabAngles[i][j] += averageAngle * AverageTabAngleForce;
#elif true
					foreach (int ii in new int[] { i, iOther }) {
						foreach (int jj in new int[] { j - 1, j + 1 }) {
							averageAngle += GetTabFromPoints(true, ii, jj, ii == i ? iOther : i).Radius;
							count++;
						}
					}

					averageOffset += GetSpanAt(i - 1, j);
					averageOffset += GetSpanAt(i + 1, j);
					averageOffset += GetSpanAt(i, j - 1);
					averageOffset += GetSpanAt(i, j + 1);
					averageOffset /= 4;

					averageOffset = averageOffset / count - GetSpanAt(i, j);
					averageOffset *= -AverageVForce / 2;

					averageAngle = averageAngle / count - baseCircle.Radius;
					newTabAngles[i][j] -= averageAngle * AverageTabAngleForce;
#else
					Point midpointV = GetFivePointNurbsAverage(new int[,] {
						{i, j-2},
						{i, j-1},
						{i, j},
						{i, j+1},
						{i, j+2}
					});

					Point midpointD0 = GetFivePointNurbsAverage(new int[,] {
						{i-iOtherDir, j-2},
						{i, j-1},
						{i, j},
						{i+iOtherDir, j+1},
						{i+ 2 * iOtherDir, j+2}
					});

					Point midpointD1 = GetFivePointNurbsAverage(new int[,] {
						{i-iOtherDir, j+2},
						{i, j+1},
						{i, j},
						{i+iOtherDir, j-1},
						{i+ 2 * iOtherDir, j-2}
					});

					Point midpointU = GetFivePointNurbsAverage(new int[,] {
						{i - 2* iOtherDir, j},
						{i - iOtherDir, j},
						{i, j},
						{i + iOtherDir, j},
						{i + 2 * iOtherDir, j}
					});

					averageOffset = (midpointV.Y + midpointU.Y) / 2 - GetSpanAt(i, j);
					averageOffset *= -AverageVForce / 2;

					averageAngle = (midpointV.X + 2 * midpointD0.X + 2 * midpointD1.X) / 5 - tabAngles[i][j];
					newTabAngles[i][j] += averageAngle * AverageTabAngleForce;
#endif

					double size = 1;// (points[i][j + 1] - points[i][j]).Magnitude;
					double slip = (uAngleOffset + averageOffset + uTouchOffset) * size;

					newVParameters[i][j] += vOffset + slip;
					newVParameters[i][j + 1] += vOffset - slip;

					newVParameters[i][j - 1] += (vOffset + slip) / 2;
					newVParameters[i][j + 2] += (vOffset - slip) / 2;

					//				double oldSpan = (points[i][j + 1] - points[i][j]).Magnitude;
					//				double newSpan = (points[i][j + 1] - points[i][j]).Magnitude;


					Trace.WriteIf(Iteration % 400 == 0, tabAngles[i][j] + " ");
					//					Trace.WriteIf(Iteration % 400 == 0, iAngle + " ");
					//					Trace.WriteIf(Iteration % 400 == 0, GetSpanAt(i,j) + " ");
				}
				Trace.WriteLineIf(Iteration % 400 == 0, "");
			}

			for (int i = 0; i < iSteps * 2; i++) {
				for (int j = 0; j < jSteps; j++) {
					newTabAngles[i][j] = Math.Max(Math.PI / 2, newTabAngles[i][j]);
					newTabAngles[i][j] = Math.Min(Math.PI * 3 / 4, newTabAngles[i][j]);

					double l1 = backupVParameters[i][j + 1] - backupVParameters[i][j];
					double l2 = newVParameters[i][j + 1] - newVParameters[i][j];
					double b1 = Math.PI - tabAngles[i][j];

					double a = l1 / l2 / Math.Tan(b1 / 2) * (1 + 1 / Math.Cos(b1 / 2));
					double phi = 2 * Math.Atan(1 / a);
					tabAngles[i][j] = Math.PI - 2 * phi;

					//double b2 = 2 * Math.Asin(l2 / l1 * Math.Sin(b1 / 2));
					//tabAngles[i][j] = Math.PI - b2;
				}
			}

			for (int i = 0; i < iSteps * 2; i++) {
				// make intersecting tabs identical

				//double angle = (
				//    newTabAngles[i][0] +
				//    newTabAngles[i + iSteps][0] +
				//    newTabAngles[i + iSteps / 2][jSteps / 2] +
				//    newTabAngles[i + 3 * iSteps / 2][jSteps / 2]
				//) / 4;

				//newTabAngles[i][0] = angle;
				//newTabAngles[i + iSteps][0] = angle;
				//newTabAngles[i + iSteps / 2][jSteps / 2] = angle;
				//newTabAngles[i + 3 * iSteps / 2][jSteps / 2] = angle;

				double span = (
					(newVParameters[i][1] - newVParameters[i][0]) +
					(newVParameters[i + iSteps][1] - newVParameters[i + iSteps][0]) +
					(newVParameters[i + iSteps / 2][jSteps / 2 + 1] - newVParameters[i + iSteps / 2][jSteps / 2]) +
					(newVParameters[i + 3 * iSteps / 2][jSteps / 2 + 1] - newVParameters[i + 3 * iSteps / 2][jSteps / 2])
				) / 8;

				newVParameters[i][0] = -span;
				newVParameters[i][1] = span;
				newVParameters[i + iSteps][0] = -span;
				newVParameters[i + iSteps][1] = span;
				newVParameters[i + iSteps / 2][jSteps / 2] = Math.PI - span;
				newVParameters[i + iSteps / 2][jSteps / 2 + 1] = Math.PI + span;
				newVParameters[i + 3 * iSteps / 2][jSteps / 2] = Math.PI - span;
				newVParameters[i + 3 * iSteps / 2][jSteps / 2 + 1] = Math.PI + span;

			}

			// Average antipodal points
			// for l(i, j),
			// l(i, j) == l(-i, pi+j)* == l(i+2pi, -j) == l(-i-2pi, pi-j)*
			// Where * means x=x, y=-y, z=-z
			for (int i = 0; i < iSteps * 2; i++) {
				for (int j = 0; j < jSteps / 2; j++) {
					double average = (
						newVParameters[i][j] +
						newVParameters[-i][j + jSteps / 2] - Math.PI +
						(j < 2 ? 0 : 2 * Math.PI) - newVParameters[i + iSteps][-j + 1] +
						Math.PI - newVParameters[-i - iSteps][-j + jSteps / 2 + 1]
					) / 4;

					newVParameters[i][j] = average;
					newVParameters[-i][j + jSteps / 2] = average + Math.PI;
					newVParameters[i + iSteps][-j + 1] = (j < 2 ? 0 : 2 * Math.PI) - average;
					newVParameters[-i - iSteps][-j + jSteps / 2 + 1] = Math.PI - average;

					average = (
						tabAngles[i][j] +
						tabAngles[-i][j + jSteps / 2 + 1] +
						tabAngles[i + iSteps][-j] +
						tabAngles[-i - iSteps][-j + jSteps / 2 + 1]
					) / 4;

					tabAngles[i][j] = average;
					tabAngles[-i][j + jSteps / 2 + 1] = average;
					tabAngles[i + iSteps][-j] = average;
					tabAngles[-i - iSteps][-j + jSteps / 2 + 1] = average;
				}
			}

			CumulativeError = Math.Sqrt(cumulativeErrorTally / (iSteps * 2 * jSteps * 2));
			//	Trace.WriteLine(lastCumulativeError);

			// We're not calculating the points for the last iteration.  Whatevs.
			vParameters = newVParameters;
			tabAngles = newTabAngles;

			for (int i = 0; i < iSteps * 2; i++) {
				double u = 2 * Math.PI * (double) i / iSteps;
				for (int j = 0; j < jSteps; j++) {
					points[i][j] = Lawson.Evaluate(PointUV.Create(vParameters[i][j], u), p, q, circleAngle, inverseOffset, true) * scale;
					//		Trace.WriteLine(string.Format("{0} {1} {2}", i, j, tabAngles[i][j]));
				}
			}
		}

		private Point GetFivePointNurbsAverage(int[,] indices) {
			var knots = new Knot[] {
						new Knot(0, 4),
						new Knot(1, 4)
					};

			var controlPoints = new ControlPoint[] {
						new ControlPoint(Point.Create(tabAngles[indices[0,0]][indices[0,1]], GetSpanAt(indices[0,0], indices[0,1]),0), 1),
						new ControlPoint(Point.Create(tabAngles[indices[1,0]][indices[1,1]], GetSpanAt(indices[1,0], indices[1,1]),0), 1),
						new ControlPoint(Point.Create(tabAngles[indices[2,0]][indices[2,1]], GetSpanAt(indices[2,0], indices[2,1]),0), 1),
						new ControlPoint(Point.Create(tabAngles[indices[3,0]][indices[3,1]], GetSpanAt(indices[3,0], indices[3,1]),0), 1),
						new ControlPoint(Point.Create(tabAngles[indices[4,0]][indices[4,1]], GetSpanAt(indices[4,0], indices[4,1]),0), 1)
					};

			NurbsData data = new NurbsData(5, false, false, knots);
			NurbsCurve curve = NurbsCurve.CreateFromControlPoints(data, controlPoints);

			Point midpoint = curve.Evaluate(0.5).Point;
			return midpoint;
		}

		private Circle GetTabFromPoints(bool swap, int i, int j, int iOther) {
			Point p0 = points[i][j];
			Point p1 = points[i][j + 1];
			Point pn = (new Point[] { points[iOther][j], points[iOther][j + 1] }).Average();
			Direction normal = Vector.Cross(p0 - pn, p1 - pn).Direction;
			return Tabs.GetCircularTabCircle(p0, p1, normal, tabAngles[i][j], swap);
		}

		private double GetSpanAt(int i, int j) {
			return (vParameters[i][j + 1] - vParameters[i][j] + Math.PI * 2) % (Math.PI * 2);
		}

		public Graphic GetGraphic() {
			var graphics = new List<Graphic>();

			int iSteps = points.Count / 2;
			int jSteps = points[0].Count;

			//		Body body = Tabs.CreateCircularTab(p0, p1, normal, angle, isMale);

			bool swap = false;
			for (int i = 0; i < iSteps; i++) {
				var facetVertices = new List<FacetVertex>();
				var facets = new List<Facet>();
				var tabMeshes = new List<MeshPrimitive>();
				for (int j = 0; j < jSteps; j++) {
					int facetOffset = facetVertices.Count;
					// Main ring
					Point p00 = points[i][j];
					Point p01 = points[i][j + 1];
					Point p10 = points[i + 1][j];
					Point p11 = points[i + 1][j + 1];

					//Vector n0 = Vector.Cross(p00 - p01, p00 - p10);
					//Vector n1 = Vector.Cross(p11 - p10, p11 - p01);
					//Direction n = (n0 + n1).Direction;

					//facetVertices.Add(new FacetVertex(p00, n));
					//facetVertices.Add(new FacetVertex(p01, n));
					//facetVertices.Add(new FacetVertex(p10, n));
					//facetVertices.Add(new FacetVertex(p11, n));

					//if ((p00 - p11).Magnitude < (p10 - p01).Magnitude) {
					//    facets.Add(new Facet(0 + facetOffset, 1 + facetOffset, 3 + facetOffset));
					//    facets.Add(new Facet(0 + facetOffset, 3 + facetOffset, 2 + facetOffset));
					//}
					//else {
					//    facets.Add(new Facet(0 + facetOffset, 2 + facetOffset, 1 + facetOffset));
					//    facets.Add(new Facet(1 + facetOffset, 3 + facetOffset, 2 + facetOffset));
					//}

					Point pn0 = (new Point[] { points[i - 1][j], points[i - 1][j + 1] }).Average();
					Point pn1 = (new Point[] { points[i + 2][j], points[i + 2][j + 1] }).Average();

					Direction normal0 = Vector.Cross(p01 - pn0, p00 - pn0).Direction;
					Direction normal1 = Vector.Cross(p10 - pn1, p11 - pn1).Direction;

					tabMeshes.Add(Tabs.GetCircularTabMesh(p01, p00, -normal0, tabAngles[i][j], swap));
					tabMeshes.Add(Tabs.GetCircularTabMesh(p10, p11, -normal1, tabAngles[i + 1][j], !swap));

					swap = !swap;
				}

				HSBColor hsbFill = new HSBColor(255, (float) i / iSteps * 360, 122, 88);
				HSBColor hsbLine = new HSBColor(System.Drawing.Color.MidnightBlue);
				hsbLine.H = (float) i / iSteps * 360;

				var style = new GraphicStyle {
					EnableDepthBuffer = true,
					LineColor = hsbLine.Color,
					LineWidth = 1,
					FillColor = hsbFill.Color,

					EnableCulling = true
				};

				//		graphics.Add(Graphic.Create(style, MeshPrimitive.Create(facetVertices, facets)));
				foreach (MeshPrimitive mesh in tabMeshes)
					graphics.Add(Graphic.Create(style, mesh));

			}

			return Graphic.Create(null, null, graphics);
		}

		public ICollection<DesignBody> CreateSolid(Part mainPart) {
			var bands = new List<ICollection<Body>>();
			var cutters = new List<Body[]>();
			double newScale = 0.094;
			double cutterHeight = 0.01 / newScale;
			double cutterWidth = 0.0005 / newScale;

			bool swap = false;
			for (int i = 0; i < iSteps; i++) {
				var band = new List<Body>();


				//if (i == 4) {
				//        DesignCurve.Create(Window.ActiveWindow.Scene as Part, CurveSegment.Create(points[i][0], points[i][1]));
				//        DesignCurve.Create(Window.ActiveWindow.Scene as Part, CurveSegment.Create(points[i + iSteps / 2][jSteps / 2], points[i + iSteps / 2][jSteps / 2 + 1]));
				//}


				for (int j = 0; j < jSteps; j++) {
					// Main ring
					Point p00 = points[i][j];
					Point p01 = points[i][j + 1];
					Point p10 = points[i + 1][j];
					Point p11 = points[i + 1][j + 1];

					Body b0, b1;
					if ((p00 - p11).Magnitude < (p10 - p01).Magnitude) {
						b0 = ShapeHelper.CreatePolygon(new Point[] { p00, p01, p11 }, 0);
						b1 = ShapeHelper.CreatePolygon(new Point[] { p00, p11, p10 }, 0);
					}
					else {
						b0 = ShapeHelper.CreatePolygon(new Point[] { p01, p10, p00 }, 0);
						b1 = ShapeHelper.CreatePolygon(new Point[] { p01, p11, p10 }, 0);
					}

					// Tabs
					/*            Male      Female      Male
					 * ---p00last-------p00--------p01-------p10next--- v+
					 *       |           |          |           |
					 *       |    pn0    |          |    pn1    |
					 *       |           |          |           |
					 * ---p10last-------p10--------p11-------p11next---
					 * 
					 */
					Point pn0 = (new Point[] { points[i - 1][j], points[i - 1][j + 1] }).Average();
					Point pn1 = (new Point[] { points[i + 2][j], points[i + 2][j + 1] }).Average();

					Direction normal0 = Vector.Cross(p01 - pn0, p00 - pn0).Direction;
					Direction normal1 = Vector.Cross(p10 - pn1, p11 - pn1).Direction;

					Body tab0 = Tabs.CreateCircularTab(p01, p00, -normal0, tabAngles[i][j], swap);
					Body tab1 = Tabs.CreateCircularTab(p10, p11, -normal1, tabAngles[i + 1][j], !swap);

					//DesignBody annotateMe = DesignBody.Create(mainPart, "annotatme", (swap ? tab0 : tab1).Copy());
					//NoteHelper.AnnotateFace(mainPart, annotateMe.Faces.First(), string.Format("{0},{1}", i, j), 0.02, null);
					//annotateMe.Delete();

					//DesignBody.Create(Window.ActiveWindow.Scene as Part, "test", b0.Copy());
					//DesignBody.Create(Window.ActiveWindow.Scene as Part, "test", b1.Copy());
					//DesignBody.Create(Window.ActiveWindow.Scene as Part, "test", tab0.Copy());
					//DesignBody.Create(Window.ActiveWindow.Scene as Part, "test", tab1.Copy());

					try {
						b0.Unite(new Body[] { b1, tab0, tab1 });
					}
					catch {
						DesignBody.Create(Window.ActiveWindow.Scene as Part, "test", b0.Copy());
						DesignBody.Create(Window.ActiveWindow.Scene as Part, "test", b1.Copy());
						DesignBody.Create(Window.ActiveWindow.Scene as Part, "test", tab0.Copy());
						DesignBody.Create(Window.ActiveWindow.Scene as Part, "test", tab1.Copy());
						return null;
					}

					//	Debug.Assert(b0.Shells.Count == 1);
					band.Add(b0);

					swap = !swap;
				}

				bands.Add(band.TryUnionBodies());

				// Cutters
				Point p0ThisSide0 = points[i][0];
				Point p0ThisSide1 = points[i][1];
				Point p0OtherSide0 = points[i + iSteps / 2][jSteps / 2];
				Point p0OtherSide1 = points[i + iSteps / 2][1 + jSteps / 2];

				Point p1ThisSide0 = points[i + 1][0];
				Point p1ThisSide1 = points[i + 1][1];
				Point p1OtherSide0 = points[i + 1 + iSteps / 2][jSteps / 2];
				Point p1OtherSide1 = points[i + 1 + iSteps / 2][1 + jSteps / 2];

				Point p0 = CurveSegment.Create(p0ThisSide0, p0ThisSide1).GetInBetweenPoint(
					CurveSegment.Create(p0OtherSide0, p0OtherSide1
				));

				Point p1 = CurveSegment.Create(p1ThisSide0, p1ThisSide1).GetInBetweenPoint(
					CurveSegment.Create(p1OtherSide0, p1OtherSide1
				));

				//Point p0 = CurveSegment.Create(p0ThisSide0, p0ThisSide1).IntersectCurve(
				//    CurveSegment.Create(p0OtherSide0, p0OtherSide1
				//)).First().Point;

				//Point p1 = CurveSegment.Create(p1ThisSide0, p1ThisSide1).IntersectCurve(
				//    CurveSegment.Create(p1OtherSide0, p1OtherSide1
				//)).First().Point;

				Direction n0 = (p0OtherSide1 - p0OtherSide0).Direction;
				Direction n1 = (p1OtherSide1 - p1OtherSide0).Direction;

				Direction d0 = (p0ThisSide1 - p0ThisSide0).Direction;
				Direction d1 = (p1ThisSide1 - p1ThisSide0).Direction;

				var profiles = new List<ICollection<ITrimmedCurve>>();
				profiles.Add(p0.GetRectanglePointsAround(d0 * cutterHeight, n0 * cutterWidth).GetProfile());
				profiles.Add(p1.GetRectanglePointsAround(d1 * cutterHeight, n1 * cutterWidth).GetProfile());
				Body cutterA = Body.LoftProfiles(profiles, false, true);

				profiles = new List<ICollection<ITrimmedCurve>>();
				profiles.Add(p0.GetRectanglePointsAround(n0 * cutterHeight, d0 * cutterWidth).GetProfile());
				profiles.Add(p1.GetRectanglePointsAround(n1 * cutterHeight, d1 * cutterWidth).GetProfile());
				Body cutterB = Body.LoftProfiles(profiles, false, true);

				cutters.Add(new Body[] { cutterA, cutterB });
			}

			var designBands = new List<DesignBody>();
			Layer cutterLayer = NoteHelper.CreateOrGetLayer(mainPart.Document, "Cutters", System.Drawing.Color.DarkViolet);
			Matrix scaleMatrix = Matrix.CreateScale(scale, Point.Origin);

			for (int i = 0; i < bands.Count; i++) {
				int whichCutter = i % 2;

				Part part = Part.Create(mainPart.Document, string.Format("Band {0:00}", i));
				Component.Create(mainPart, part);

				int ii = i;
				if (whichCutter == 0)
					ii = i + iSteps / 2;

				List<Body> mergedCutters = new Body[] {
                    cutters[(ii + iSteps - 1) % iSteps][whichCutter].Copy(),
                    cutters[ii % iSteps][whichCutter].Copy(),
                    cutters[(ii + 1) % iSteps][whichCutter].Copy()
                }.TryUnionBodies().ToList();

				Debug.Assert(mergedCutters.Count == 1, "Couldn't merge cutters");

				double nominalRadius = 0.02;
				double innerRadius = (nominalRadius - cutterWidth / 2) / newScale;
				double outerRadius = (nominalRadius + cutterWidth / 2) / newScale;

				var edgeRounds = new List<KeyValuePair<Edge, EdgeRound>>();
				foreach (Edge edge in mergedCutters[0].Edges) {
					if (edge.Length > cutterHeight * 1.1 || edge.Length < cutterHeight * 0.9)
						continue;

					double angle = edge.GetAngle();
					if (Math.Abs(angle) > Math.PI / 4 || angle == 0)
						continue;

					edgeRounds.Add(new KeyValuePair<Edge, EdgeRound>(edge, new FixedRadiusRound(angle > 0 ? outerRadius : innerRadius)));
				}

				mergedCutters[0].RoundEdges(edgeRounds);

				mergedCutters.Add(cutters[(ii - 1 + iSteps / 2) % iSteps][1 - whichCutter].Copy());
				mergedCutters.Add(cutters[(ii + 1 + iSteps / 2) % iSteps][1 - whichCutter].Copy());

				HSBColor hsbColor = new HSBColor(0, 100, 200);
				hsbColor.H = (float) ((double) i / bands.Count * 360);

				var cutBand = new List<Body>();
				foreach (Body body in bands[i]) {
					foreach (Body cutterBody in mergedCutters) {
						body.Imprint(cutterBody);
						foreach (Face face in body.Faces) {
							if (!IsSpanningBody(face, cutterBody))
								continue;

							body.DeleteFaces(new Face[] { face }, RepairAction.None);
							//	DesignBody designBody = DesignBody.Create(part, "Cutter", cutterBody.Copy());
							//	designBody.SetColor(null, hsbColor.Color);
						}
					}

					cutBand.AddRange(body.SeparatePieces());
				}

				cutBand = cutBand.TryUnionBodies().ToList();

				//foreach (Body body in bands[i]) {
				foreach (Body body in cutBand) {
					body.Transform(scaleMatrix);
					DesignBody designBody = DesignBody.Create(part, "Band", body);
					designBody.SetColor(null, hsbColor.Color);
					designBands.Add(designBody);
				}

				//foreach (Body body in mergedCutters) {
				//    DesignBody designBody = DesignBody.Create(part, "Cutter", body);
				//    designBody.Layer = cutterLayer;
				//    hsbColor.H += 180 * whichCutter;
				//    designBody.SetColor(null, hsbColor.Color);
				////	designBands[i].Shape.Imprint(designBody.Shape);
				//}

			}

			Trace.WriteLine("vParameters");
			for (int j = 0; j < jSteps; j++) {
				for (int i = 0; i < iSteps; i++)
					Trace.Write(vParameters[i][j] + " ");

				Trace.WriteLine("");
			}

			Trace.WriteLine("tabAngles");
			for (int j = 0; j < jSteps; j++) {
				for (int i = 0; i < iSteps; i++)
					Trace.Write(tabAngles[i][j] + " ");

				Trace.WriteLine("");
			}


			return designBands;
		}


		public ICollection<DesignBody> CreateCircularTabsOnly(Part mainPart) {
			var bands = new List<ICollection<Body>>();
			var cutters = new List<Body[]>();
			double newScale = 0.094;
			double cutterHeight = 0.01 / newScale;
			double cutterWidth = 0.0005 / newScale;

			bool swap = false;
			for (int i = 0; i < iSteps; i++) {
				var band = new List<Body>();

				for (int j = 0; j < jSteps; j++) {
					// Main ring
					Point p00 = points[i][j];
					Point p01 = points[i][j + 1];
					Point p10 = points[i + 1][j];
					Point p11 = points[i + 1][j + 1];

					// Tabs
					/*            Male      Female      Male
					 * ---p00last-------p00--------p01-------p01next--- v+
					 *       |           |          |           |
					 *       |    pn0    |          |    pn1    |
					 *       |           |          |           |
					 * ---p10last-------p10--------p11-------p11next---
					 * 
					 */
					Point pn0 = (new Point[] { points[i - 1][j], points[i - 1][j + 1] }).Average();
					Point pn1 = (new Point[] { points[i + 2][j], points[i + 2][j + 1] }).Average();

					Direction normal0 = Vector.Cross(p01 - pn0, p00 - pn0).Direction;
					Direction normal1 = Vector.Cross(p10 - pn1, p11 - pn1).Direction;

					Body tab0 = Tabs.CreateCircularTab(p01, p00, -normal0, tabAngles[i][j], swap);
					Body tab1 = Tabs.CreateCircularTab(p10, p11, -normal1, tabAngles[i + 1][j], !swap);

					//	Debug.Assert(b0.Shells.Count == 1);
					band.Add(tab0);
					band.Add(tab1);

					swap = !swap;
				}

				bands.Add(band);
			}

			var designBands = new List<DesignBody>();
			Matrix scaleMatrix = Matrix.CreateScale(scale, Point.Origin);

			for (int i = 0; i < bands.Count; i++) {
				Part part = Part.Create(mainPart.Document, string.Format("Band {0:00}", i));
				Component.Create(mainPart, part);

				HSBColor hsbColor = new HSBColor(0, 100, 200);
				hsbColor.H = (float) ((double) i / bands.Count * 360);

				foreach (Body body in bands[i]) {
					body.Transform(scaleMatrix);
					DesignBody designBody = DesignBody.Create(part, "Band", body);
					designBody.SetColor(null, hsbColor.Color);
					designBands.Add(designBody);
				}
			}

			return designBands;
		}

		static bool IsSpanningBody(Face face, Body body) {
			foreach (Point point in face.Loops.SelectMany(l => l.Fins).Select(f => f.TrueStartPoint())) {
				if (!body.ContainsPoint(point))
					return false;
			}

			//if (!body.ContainsPoint(face.Geometry.Evaluate(face.BoxUV.Center).Point))
			//	return false;

			//foreach (Point point in face.Loops.SelectMany(l => l.Fins).Select(f => f.TrueStartPoint())) {
			//    if ((body.ProjectPoint(point) - point).Magnitude > Accuracy.LinearResolution * 100)
			//        return false;
			//}

			return true;
		}


		public static Point Evaluate(PointUV uv, double p, double q, double circleAngle, Vector inverseOffset, bool isInverted, out Direction normal) {
			normal = GetNormal(uv, p, q, circleAngle, inverseOffset, isInverted);
			return Evaluate(uv, p, q, circleAngle, inverseOffset, isInverted);
		}

		public static Point Evaluate(PointUV uv, double p, double q, double circleAngle, Vector inverseOffset, bool isInverted) {
			double u = uv.U;
			double v = uv.V;

			Circle circle = Circle.Create(Frame.World, 1);
			// spiraly things     circle = circle.CreateTransformedCopy(Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirY), Math.PI / 4));
			circle = circle.CreateTransformedCopy(Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirY), circleAngle));

			return LawsonTransform(circle.Evaluate(u).Point, v, p, q, inverseOffset, isInverted);
		}

#if true
		public static Direction GetNormal(PointUV uv, double p, double q, double circleAngle, Vector inverseOffset, bool isInverted) {
			double delta = 0.001;
			var deltaU = VectorUV.Create(delta, 0);
			var deltaV = VectorUV.Create(0, delta);

			PointUV uv00 = uv - deltaU;
			PointUV uv01 = uv + deltaU;
			PointUV uv10 = uv - deltaV;
			PointUV uv11 = uv + deltaV;

			Vector du =
				Lawson.Evaluate(uv00, p, q, circleAngle, inverseOffset, isInverted) -
				Lawson.Evaluate(uv01, p, q, circleAngle, inverseOffset, isInverted)
			;

			Vector dv =
				Lawson.Evaluate(uv10, p, q, circleAngle, inverseOffset, isInverted) -
				Lawson.Evaluate(uv11, p, q, circleAngle, inverseOffset, isInverted)
			;

			return Vector.Cross(du, dv).Direction;
		}
#else
		public static Direction GetNormal(PointUV uv, double p, double q, double circleAngle, Vector inverseOffset, bool isInverted) {
			double delta = 0.001;
			var deltaU = VectorUV.Create(delta, 0);
			var deltaV = VectorUV.Create(0, delta);

			PointUV uv00 = uv - deltaU;
			PointUV uv01 = uv + deltaU;
			PointUV uv10 = uv - deltaV;
			PointUV uv11 = uv + deltaV;

			Point pCenter = Lawson.Evaluate(uv, p, q, circleAngle, inverseOffset, isInverted);

			var pxxs = new Point[] {
				Lawson.Evaluate(uv00, p, q, circleAngle, inverseOffset, isInverted),
				Lawson.Evaluate(uv01, p, q, circleAngle, inverseOffset, isInverted),
				Lawson.Evaluate(uv10, p, q, circleAngle, inverseOffset, isInverted),
				Lawson.Evaluate(uv11, p, q, circleAngle, inverseOffset, isInverted)
			};

			var sum = Vector.Zero;
			foreach (Point pxx in pxxs)
				sum += pxx - pCenter;

			if (sum != Vector.Zero)
				return sum.Direction;

			return Vector.Cross(pxxs[1] - pxxs[0], pxxs[1] - pxxs[2]).Direction;
		}
#endif

		public static Point LawsonTransform(Point point, double t, double p, double q, Vector inverseOffset, bool isInverted) {
			//From Daniel Piker
			double xa = point.X;        //getting the coordinates of the input point
			double ya = point.Y;
			double za = point.Z;

			//reverse stereographically project to Riemann hypersphere 
			double xb = 2 * xa / (1 + xa * xa + ya * ya + za * za);
			double yb = 2 * ya / (1 + xa * xa + ya * ya + za * za);
			double zb = 2 * za / (1 + xa * xa + ya * ya + za * za);
			double wb = (-1 + xa * xa + ya * ya + za * za) / (1 + xa * xa + ya * ya + za * za);

			//now rotate the hypersphere - (figuring out this part hurt my head!)
			double xc = xb * Math.Cos(p * t) + yb * Math.Sin(p * t);
			double yc = -xb * Math.Sin(p * t) + yb * Math.Cos(p * t);
			double zc = zb * Math.Cos(q * t) - wb * Math.Sin(q * t);
			double wc = zb * Math.Sin(q * t) + wb * Math.Cos(q * t);

			//then project stereographically back to flat 3D
			double xd = xc / (1 - wc);
			double yd = yc / (1 - wc);
			double zd = zc / (1 - wc);

			point = Point.Create(xd, yd, zd);

			//spherical inversion
			if (isInverted) {
				point += inverseOffset;
				point = Point.Origin + point.Vector.Direction * 1 / point.Vector.Magnitude;

				if (
					Double.IsNaN(point.X) || Double.IsNaN(point.Y) || Double.IsNaN(point.Z) ||
					Double.IsInfinity(point.X) || Double.IsInfinity(point.Y) || Double.IsInfinity(point.Z)
					)
					return Point.Origin;
			}

			return point;
		}
	}
}