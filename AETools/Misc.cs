using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddIn.CAM;
using SpaceClaim.AddInLibrary;
using SpaceClaim.AddIn.Unfold;
using Application = SpaceClaim.Api.V10.Application;
using BenTools.Data;
using BenTools.Mathematics;
using VectorVoronoi = BenTools.Mathematics.Vector;
using Vector = SpaceClaim.Api.V10.Geometry.Vector;

namespace SpaceClaim.AddIn.AETools {
    static class Export {
        const string exportCommandName = "AEExport";
        public static void Initialize() {
            Command command;
            command = Command.Create(exportCommandName);
            command.Text = "XXX";
            command.Hint = "Export to BRL-CAD (Experimental).";
            command.Executing += XXX_Executing;
            command.Updating += AddInHelper.EnabledCommand_Updating;
            command = Command.Create("AECurves");
            command.Text = "Curves";
            command.Hint = "TestCurves.";
            command.Executing += Curves_Executing;
            command.Updating += AddInHelper.EnabledCommand_Updating;
            command = Command.Create("AEVector");
            command.Text = "Vector";
            command.Hint = "Test Vector.";
            command.Executing += Vector_Executing;
            command.Updating += AddInHelper.EnabledCommand_Updating;
        }
        static void XXX_Executing(object sender, EventArgs eventArgs) {
            
        }
#if false // wrapping
			Window activeWindow = Window.ActiveWindow;
			Part part = activeWindow.Scene as Part;
			ICollection<IDesignBody> iDesBodies = activeWindow.GetAllSelectedIDesignBodies();
			if (iDesBodies.Count != 2)
				return;
			Body pathBody = iDesBodies.First().Master.Shape.Copy();
			pathBody.Transform(iDesBodies.First().TransformToMaster.Inverse);
			Body wrapBody = iDesBodies.Last().Master.Shape.Copy();
			wrapBody.Transform(iDesBodies.Last().TransformToMaster.Inverse);
			Debug.Assert(wrapBody.Faces.Count == 1);
			Debug.Assert(wrapBody.Faces.First().Loops.Count == 1);
			Point middle = wrapBody.GetBoundingBox(Matrix.Identity).Center;
			var wrapCurves = new TrimmedCurveChain(wrapBody.Edges.Where(d => d.StartPoint.Z < middle.Z && d.EndPoint.Z < middle.Z).Cast<ITrimmedCurve>().ToArray());
			var wrapPoints = new List<Point>();
			double tolerance = 0.01;
			double length = wrapCurves.Length;
			int count = (int) Math.Ceiling(length / tolerance);
			Point point;
			for (int i = 0; i < count; i++) {
				if (!wrapCurves.TryGetPointAlongCurve(length * i / count, out point))
					break;
				wrapPoints.Add(point);
			}
			wrapPoints.Add(wrapCurves.EndPoint);
			Separation separation = pathBody.GetClosestSeparation(wrapBody);
			int closestWrapIndex = 0;
			double closestDistance = double.MaxValue;
			for (int i = 0; i < wrapPoints.Count; i++) {
				double distance = (separation.PointB - wrapPoints[i]).MagnitudeSquared();
				if (distance < closestDistance) {
					closestWrapIndex = i;
					closestDistance = distance;
				}
			}
			Debug.Assert(pathBody.Faces.Count == 1);
			Debug.Assert(pathBody.Faces.First().Loops.Count == 1);
			var pathCircularList = new CircularList<ITrimmedCurve>(pathBody.Faces.First().Loops.First().Fins.Select(f => f.Edge).ToArray());
			var pathList = pathCircularList.ToList();
			List<ITrimmedCurve> pathListOrdered = pathList.OrderBy(p => (p.ProjectPoint(separation.PointA).Point - separation.PointA).MagnitudeSquared()).ToList();
			int backwardPathIndex = Math.Min(pathList.IndexOf(pathListOrdered[0]), pathList.IndexOf(pathListOrdered[1]));
			int forwardPathIndex = Math.Max(pathList.IndexOf(pathListOrdered[0]), pathList.IndexOf(pathListOrdered[1]));
			Point backwardPoint = new Point[] { pathList[backwardPathIndex].StartPoint, pathList[backwardPathIndex].EndPoint }.Average();
			Point forwardPoint = new Point[] { pathList[forwardPathIndex].StartPoint, pathList[forwardPathIndex].EndPoint }.Average();
			DesignCurve startCurvePoint = DesignCurve.Create(part, CurveSegment.Create(PointCurve.Create(backwardPoint)));
			startCurvePoint.SetColor(null, System.Drawing.Color.Red);
			startCurvePoint = DesignCurve.Create(part, CurveSegment.Create(PointCurve.Create(forwardPoint)));
			startCurvePoint.SetColor(null, System.Drawing.Color.Red);
			//	Point startPoint = pathList[closePaths[0]];
			int profileSense = 1;
			if ((backwardPoint - wrapPoints[closestWrapIndex + 1]).MagnitudeSquared() < (forwardPoint - wrapPoints[closestWrapIndex + 1]).MagnitudeSquared())
				profileSense = -1;
			var finishedPoints = new List<Point>();
			finishedPoints.AddRange(Create3DPoints(wrapPoints, closestWrapIndex, pathCircularList, false, -profileSense, forwardPathIndex + 1));
			finishedPoints.Add(wrapPoints[closestWrapIndex]);
			finishedPoints.AddRange(Create3DPoints(wrapPoints, closestWrapIndex, pathCircularList, true, profileSense, backwardPathIndex));
			DesignCurve.Create(part, CurveSegment.Create(NurbsCurve.CreateThroughPoints(true, finishedPoints, tolerance)));
		//	foreach (Point p in finishedPoints)
		//	    DesignCurve.Create(part, CurveSegment.Create(PointCurve.Create(p)));
		}
		private static IList<Point> Create3DPoints(List<Point> wrapPoints, int closestWrapIndex, CircularList<ITrimmedCurve> pathCircularList, bool wrapSense, int profileSense, int startPathIndex) {
			var pathOrdered = new List<ITrimmedCurve>();
			for (int i = 0; i < pathCircularList.Count; i++)
				pathOrdered.Add(pathCircularList[startPathIndex + profileSense * i]);
			var pathChain = new TrimmedCurveChain(pathOrdered);
			var curvePoints = new List<Point>();
			Point point;
			double offset = 0;
			if (wrapSense) {
				for (int i = closestWrapIndex + 1; i < wrapPoints.Count; i++) {
					offset += (wrapPoints[i] - wrapPoints[i - 1]).Magnitude;
					if (!pathChain.TryGetPointAlongCurve(offset, out point))
						break;
					curvePoints.Add(Point.Create(point.X, point.Y, wrapPoints[i].Z));
				}
			}
			else {
				pathChain.Reverse();
				for (int i = closestWrapIndex; i > 0; i--) {
					offset += (wrapPoints[i] - wrapPoints[i - 1]).Magnitude;
					if (!pathChain.TryGetPointAlongCurve(offset, out point))
						break;
					curvePoints.Add(Point.Create(point.X, point.Y, wrapPoints[i].Z));
				}
				curvePoints.Reverse();
			}
			return curvePoints;
		}
#endif
#if false // planar offset
            const double inches = 25.4 / 1000;
            const double webWidth = (double)3 / 32 * inches;
            const double circuitBoardBandThickness = 0.0005;
            const double circuitBoardBandWidth = 0.01;
            const double circuitBoardBandPeriod = (double)1 / 30;
            double circuitBoardBandFlat = circuitBoardBandWidth * Const.Phi; // circuitBoardBandPeriod / 2;
            const double circuitBoardLedSize = 0.005;
            const double circuitBoardLedHoleSize = 0.281 * inches; // K drill -- 5mm * sqrt(2) = 0.278388
            const double circuitBoardLedHeight = 0.0025;
            const double circuitBoardResistorHeight = 0.0015;
            const double circuitBoardResistorWidth = 0.002;
            const double circuitBoardResistorLength = 0.004;
            const double circuitBoardResistorOffset = circuitBoardLedSize / 2 + 0.001 + circuitBoardResistorWidth / 2;
            const double focusMaterialThickness = (0.75 - .01) * inches; //  http://www.interstateplastics.com/Black-Hdpe-Sheet-HDPBE.php?sku=HDPBE&vid=201211120902-7p&dim2=48&dim3=48&thickness=0.750&qty=1&recalculate.x=123&recalculate.y=18
            //     const double focusMaterialThickness = (0.75 - .038) * inches; // McMaster 8619K487	
            const double circuitBoardTopDepth = circuitBoardBandThickness * 4;
            const double circuitBoardTopHeight = focusMaterialThickness - circuitBoardTopDepth;
            const double focusHeight = circuitBoardTopHeight - circuitBoardLedHeight;
            //    const double domeMaterialThickness = 0.75; //   http://www.interstateplastics.com/Clear-Acrylic-Cast-Paper-Sheet-ACRCLCP.php?sku=ACRCLCP&vid=201211180031-7p&dim2=48&dim3=48&thickness=0.750&qty=1
            //         const double domeMaterialThickness = (double)11 / 16 - 0.049; // McMaster 8560K367	
            const double domeHeight = (double)21 / 32 * inches;
            const double domeRootThickness = (double)3 / 16 * inches;
            const double boxSize = 48 * inches;
            const double boxLimit = 46 * inches;

            int maxPrint = 55;

            // Circuit board part
            Point circuitBoardBoxCorner = Point.Create(circuitBoardBandFlat / 2, circuitBoardBandWidth / 2, circuitBoardBandThickness / 2);
            Box circuitBoardBox = Box.Create(circuitBoardBoxCorner, -1 * circuitBoardBoxCorner);
            Point circuitBoardLedCorner = Point.Create(circuitBoardLedSize / 2, circuitBoardLedSize / 2, circuitBoardLedHeight / 2);
            Box circuitBoardLedBox = Box.Create(circuitBoardLedCorner, -1 * circuitBoardLedCorner);
            Point circuitBoardResistorCorner = Point.Create(circuitBoardResistorWidth / 2, circuitBoardResistorLength / 2, circuitBoardResistorHeight / 2);
            Box circuitBoardResistorBox = Box.Create(circuitBoardResistorCorner, -1 * circuitBoardResistorCorner);

            Point circuitBendStartLeft = Point.Create(circuitBoardBandFlat / 2, circuitBoardBandWidth / 2, 0);
            Point circuitBendStartRight = Point.Create(circuitBoardBandFlat / 2, -circuitBoardBandWidth / 2, 0);
            Point circuitBendEndLeft = Point.Create(-circuitBoardBandFlat / 2, circuitBoardBandWidth / 2, 0);
            Point circuitBendEndRight = Point.Create(-circuitBoardBandFlat / 2, -circuitBoardBandWidth / 2, 0);
            Vector circuitBendVector = Vector.Create(-circuitBoardBandFlat, 0, 0);
            int count = 1280;
            int countExtra = 1600;

            Part MainPart = Window.ActiveWindow.Scene as Part;
            DesignBody desBody;
            Layer reflectorLayer = Layer.Create(MainPart.Document, "Relectors", System.Drawing.Color.LightBlue);
            Layer lensLayer = Layer.Create(MainPart.Document, "Lenses", System.Drawing.Color.White);
            Layer stripLayer = Layer.Create(MainPart.Document, "Strip", System.Drawing.Color.LightSlateGray);
            Layer toolpathLayer = Layer.Create(MainPart.Document, "Toolpaths", System.Drawing.Color.DarkGoldenrod);
            Part LedPart = Part.Create(MainPart.Document, "Led");
            DesignCurve.Create(LedPart, CurveSegment.Create(PointCurve.Create(Point.Origin)));
            desBody = ShapeHelper.CreateBlock(circuitBoardBox, LedPart);
            desBody.Transform(Matrix.CreateTranslation(Direction.DirZ * -circuitBoardBandThickness / 2));
            desBody.Layer = stripLayer;
            desBody = ShapeHelper.CreateBlock(circuitBoardLedBox, LedPart);
            desBody.Transform(Matrix.CreateTranslation(Direction.DirZ * circuitBoardLedHeight / 2));
            desBody.Layer = stripLayer;

            desBody = ShapeHelper.CreateBlock(circuitBoardResistorBox, LedPart);
            desBody.Transform(Matrix.CreateTranslation(Direction.DirX * circuitBoardResistorOffset + Direction.DirZ * circuitBoardResistorHeight / 2));
            desBody.Layer = stripLayer;
            desBody = ShapeHelper.CreateBlock(circuitBoardResistorBox, LedPart);
            desBody.Transform(Matrix.CreateTranslation(Direction.DirX * -circuitBoardResistorOffset + Direction.DirZ * circuitBoardResistorHeight / 2));
            desBody.Layer = stripLayer;


            var points = new List<Point>();
            for (int i = 1; i < countExtra + 1; i++) {
                double angle = (double)i * (Const.Phi - 1) * 2 * Math.PI;
                double radius = (double)Math.Sqrt(i) * 0.06;
                Matrix trans = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), angle);
                Point point = trans * Point.Create(radius, 0, 0);
                points.Add(point);
            }
            Box box = Box.Create(points.Take(count).ToArray());
            Point[] boxPolygon = new Point[] {
                Point.Create(boxSize / 2, boxSize / 2, 0),
                Point.Create(-boxSize / 2, boxSize / 2, 0),
                Point.Create(-boxSize / 2, -boxSize / 2, 0),
                Point.Create(boxSize / 2, -boxSize / 2, 0)
            };
            boxPolygon.AsPolygon().Print();
            double size = Math.Max(box.Size.X, box.Size.Y);
            points = points.Select(p => (p - box.Center.Vector) * (boxLimit / size)).ToList();
            Vector newCenter = -box.Center.Vector * (boxLimit / size);
            Dictionary<VectorVoronoi, Cell> cells;
            List<VectorVoronoi> vectors;
            List<CurveSegment> outerEdges;
            CreateVoronoi(count, points, out cells, out vectors, out outerEdges);

            // Find cell nearest neighbors, outward
            int seedIndex = 0;
            List<Cell> seeds = new List<Cell>();
            List<Cell> used = new List<Cell>();
            while (seedIndex < count) {
                Cell cell = cells[vectors[seedIndex++]];
                if (used.Contains(cell))
                    continue;

                seeds.Add(cell);
                double lastAngle = 0;
                double maxBendAngle = Math.PI / 3;
                while (cell != null) {
                    used.Add(cell);
                    Cell[] nextCandidates = cell.Neighbors
                        .Where(c => c.Center.Vector.MagnitudeSquared() > cell.Center.Vector.MagnitudeSquared())
                        .Where(c => !used.Contains(c))
                        .OrderBy(p => (cell.Center - p.Center).MagnitudeSquared())
                        .ToArray();
                    cell.Next = null;

                    if (nextCandidates.Length == 0)
                        break;

                    // Max attainable distance is the period between cells * cos(2*angle deviation)
                    double angle0 = (nextCandidates[0].Center - cell.Center).AngleInXY();
                    if (lastAngle == 0) {
                        cell.Next = nextCandidates[0];
                        lastAngle = angle0;
                        cell = cell.Next;
                        continue;
                    }

                    if (nextCandidates.Length >= 2) {
                        double angle1 = (nextCandidates[1].Center - cell.Center).AngleInXY();
                        //    Debug.WriteLine("{0}:  {1} {2} -- {3}", seedIndex, AbsAngleDifference(angle0, lastAngle), AbsAngleDifference(angle1, lastAngle), Math.Abs(angle1) < Math.Abs(angle0) && (nextCandidates[1].Center - cell.Center).Magnitude /* * Math.Cos(2 * (angle1 - lastAngle))*/ < circuitBoardBandPeriod);
                        if (AbsAngleDifference(angle1, lastAngle) < AbsAngleDifference(angle0, lastAngle) && AbsAngleDifference(angle1, lastAngle) < maxBendAngle && (nextCandidates[1].Center - cell.Center).Magnitude / Math.Cos(2 * (angle1 - lastAngle)) < circuitBoardBandPeriod) {
                            cell.Next = nextCandidates[1];
                            lastAngle = angle1;
                            cell = cell.Next;
                            continue;
                        }
                    }

                    if (AbsAngleDifference(angle0, lastAngle) < maxBendAngle && (nextCandidates[0].Center - cell.Center).Magnitude / Math.Cos(2 * (angle0 - lastAngle)) < circuitBoardBandPeriod) {
                        cell.Next = nextCandidates[0];
                        lastAngle = angle0;
                    }

                    cell = cell.Next;
                }
            }

            Part mainPart = Window.ActiveWindow.Scene as Part;

            foreach (Cell cell in seeds) {
                Part part;

                int length = 1;
                Cell thisCell = cell;
                while (thisCell.Next != null) {
                    thisCell = thisCell.Next;
                    length++;
                }

                double angle;

                if (length == 1) {
                    angle = cell.Center.Vector.AngleInXY();
                    part = CreateRotatedInstanceWithPart(circuitBoardTopHeight, LedPart, mainPart, cell, angle);
                    continue;
                }

                if (length == 2) {
                    angle = (cell.Center - cell.Next.Center).AngleInXY();
                    part = CreateRotatedInstanceWithPart(circuitBoardTopHeight, LedPart, mainPart, cell, angle);
                    part = CreateRotatedInstanceWithPart(circuitBoardTopHeight, LedPart, mainPart, cell.Next, angle);
                    continue;
                }


                angle = ((cell.Center - cell.Next.Center).Direction.UnitVector + (cell.Next.Center - cell.Next.Next.Center).Direction.UnitVector).AngleInXY();
                angle = angle - 2 * (angle - (cell.Center - cell.Next.Center).AngleInXY());
                part = CreateRotatedInstanceWithPart(circuitBoardTopHeight, LedPart, mainPart, cell, angle);

                Cell previousCell = cell;
                Cell previousPreviousCell = null;
                thisCell = cell.Next;
                while (thisCell.Next != null) {
                    angle = ((previousCell.Center - thisCell.Center).Direction.UnitVector + (thisCell.Center - thisCell.Next.Center).Direction.UnitVector).AngleInFrame(Frame.World);
                    part = CreateRotatedInstanceWithPart(circuitBoardTopHeight, LedPart, mainPart, thisCell, angle);
                    previousPreviousCell = previousCell;
                    previousCell = thisCell;
                    thisCell = thisCell.Next;
                }

                angle = ((thisCell.Center - previousCell.Center).Direction.UnitVector + (previousCell.Center - previousPreviousCell.Center).Direction.UnitVector).AngleInXY();
                angle = angle - 2 * (angle - (thisCell.Center - previousCell.Center).AngleInXY()) + Math.PI;
                part = CreateRotatedInstanceWithPart(circuitBoardTopHeight, LedPart, mainPart, thisCell, angle);
            }

            // LED Strps
            var ledStripPart = Part.Create(mainPart.Document, "LED Strips");
            Component.Create(mainPart, ledStripPart);
            foreach (Cell cell in seeds) {
                Cell thisCell = cell;
                Cell previousCell = null;
                Matrix transStart, transEnd;
                double leftoverStrip = (circuitBoardBandPeriod - circuitBoardBandFlat) / 2;

                transStart =  thisCell.LedComponent.Placement;
                var profile = new List<ITrimmedCurve>();
                profile.Add(CurveSegment.Create(transStart * circuitBendStartRight, transStart * (circuitBendStartRight - circuitBendVector.Direction * leftoverStrip)));
                profile.Add(CurveSegment.Create(transStart * circuitBendStartLeft, transStart * (circuitBendStartLeft - circuitBendVector.Direction * leftoverStrip)));
                profile.Add(CurveSegment.Create(transStart * (circuitBendStartRight - circuitBendVector.Direction * leftoverStrip), transStart * (circuitBendStartLeft - circuitBendVector.Direction * leftoverStrip)));

                profile.Add(CurveSegment.Create(transStart * circuitBendStartRight, transStart * circuitBendEndRight));
                profile.Add(CurveSegment.Create(transStart * circuitBendStartLeft, transStart * circuitBendEndLeft));

                while (thisCell.Next != null) {
                    transStart =  thisCell.LedComponent.Placement;
                    transEnd =  thisCell.Next.LedComponent.Placement;
                    profile.Add(CurveSegment.Create(NurbsCurve.CreateFromKnotPoints(false, new Point[] { transStart * circuitBendEndRight, transEnd * circuitBendStartRight }, transStart * circuitBendVector, transEnd * circuitBendVector)));
                    profile.Add(CurveSegment.Create(NurbsCurve.CreateFromKnotPoints(false, new Point[] { transStart * circuitBendEndLeft, transEnd * circuitBendStartLeft }, transStart * circuitBendVector, transEnd * circuitBendVector)));

                    profile.Add(CurveSegment.Create(transStart * circuitBendStartRight, transStart * circuitBendEndRight));
                    profile.Add(CurveSegment.Create(transStart * circuitBendStartLeft, transStart * circuitBendEndLeft));

                    previousCell = thisCell;
                    thisCell = thisCell.Next;
                }

                transStart =  thisCell.LedComponent.Placement;
                profile.Add(CurveSegment.Create(transStart * circuitBendEndRight, transStart * (circuitBendEndRight + circuitBendVector.Direction * leftoverStrip)));
                profile.Add(CurveSegment.Create(transStart * circuitBendEndLeft, transStart * (circuitBendEndLeft + circuitBendVector.Direction * leftoverStrip)));
                profile.Add(CurveSegment.Create(transStart * (circuitBendEndRight + circuitBendVector.Direction * leftoverStrip), transStart * (circuitBendEndLeft + circuitBendVector.Direction * leftoverStrip)));

                profile.Add(CurveSegment.Create(transStart * circuitBendStartRight, transStart * circuitBendEndRight));
                profile.Add(CurveSegment.Create(transStart * circuitBendStartLeft, transStart * circuitBendEndLeft));

                Plane plane = Plane.Create(Frame.Create(Point.Create(0, 0, -circuitBoardTopHeight), Direction.DirX, Direction.DirY));
                //     Body.CreatePlanarBody(Plane.Create(Frame.Create(Point.Create(0, 0, -circuitBoardTopHeight), Direction.DirX, Direction.DirY)), profile).Print();
                try {
                    var stripBody = Body.ExtrudeProfile(plane, profile, -circuitBoardBandThickness);
                    cell.LedDesBody = DesignBody.Create(ledStripPart, String.Format("LED Strip {0}", cell.Index), stripBody);
                    cell.LedDesBody.Layer = stripLayer;
                }
                catch {
                    profile.Print();
                }


                double draft = Const.Tau / 12;
                Matrix trans = Matrix.CreateTranslation(Direction.DirZ * -circuitBoardTopDepth);
                profile = profile.OffsetTowards(plane.ProjectPoint(cell.Center).Point, plane, -Math.Tan(draft) * circuitBoardBandThickness).ToList();
                var offsetProfile = profile.OffsetTowards(plane.ProjectPoint(cell.Center).Point, plane, Math.Tan(draft) * circuitBoardTopDepth);
                offsetProfile = offsetProfile.Select(c => c.CreateTransformedCopy(trans)).ToArray();

                Body b1 = Body.CreatePlanarBody(plane, profile);
                Body b2 = Body.CreatePlanarBody(plane.CreateTransformedCopy(trans), offsetProfile);
                try {
                    cell.LedClearanceBody = Body.LoftProfiles(new List<ICollection<ITrimmedCurve>> { b1.Edges.ToArray(), b2.Edges.ToArray() }, false, false);
                }
                catch {
                    cell.LedClearanceBody = Body.ExtrudeProfile(plane, profile, -circuitBoardTopDepth);
                    b1.Edges.ToArray().Print();
                    b2.Edges.ToArray().Print();
                }

                thisCell = cell.Next;
                while (thisCell != null) {
                    thisCell.LedDesBody = cell.LedDesBody;
                    thisCell.LedClearanceBody = cell.LedClearanceBody;
                    thisCell = thisCell.Next;
                }

            }

            int printIndex = 0;
            var notePlane = DatumPlane.Create(MainPart, "Indices", Plane.PlaneXY);
            foreach (Cell cell in cells.Values.OrderBy(c => (c.Center.Vector - newCenter).MagnitudeSquared())) {
                Part part = cell.Component.Template;
                part.Name = string.Format("Cell {0}", printIndex);
                Note.Create(notePlane, notePlane.Shape.Geometry.ProjectPoint(cell.Center).Param, TextPoint.Center, 0.25 * inches, printIndex.ToString());

                Point focus = cell.Center - Direction.DirZ * focusHeight;
                ICollection<ITrimmedCurve> nominalPath = cell.Edges.Cast<ITrimmedCurve>().ToArray();
                ICollection<ITrimmedCurve> insetPath = cell.Edges.Cast<ITrimmedCurve>().ToArray().OffsetTowards(cell.Center, Plane.PlaneXY, webWidth / 2);
                Body nominalBody = Body.CreatePlanarBody(Plane.PlaneXY, nominalPath);
                Body insetBody = Body.CreatePlanarBody(Plane.PlaneXY, insetPath);
                if (printIndex++ > maxPrint)
                    continue;
                Body lip = nominalBody.Copy();
                lip.Subtract(new[] { insetBody.Copy() });
                Body parabolas = ParabolaLoft(focusHeight, cell.Center, insetBody);
                Body parabolaBottom = Body.CreatePlanarBody(
                    Plane.Create(Frame.Create(Point.Create(0, 0, -focusHeight), Direction.DirZ)),
                    parabolas.Edges.Where(edge => Accuracy.EqualLengths(edge.StartPoint.Z, -focusHeight)).ToArray()
                );
                Body outside = Body.ExtrudeProfile(Plane.PlaneXY, nominalPath, -focusMaterialThickness);
                outside.DeleteFaces(outside.Faces.Where(f => Accuracy.EqualLengths(f.Loops.SelectMany(l => l.Vertices).Select(v => v.Position.Z).Average(), 0)).ToArray(), RepairAction.None);
                var tracker = Tracker.Create();
                Face[] newParabolas = parabolas.Faces.ToArray();
                Face newParabolaBottom = parabolaBottom.Faces.First();
                outside.Stitch(new[] { lip, parabolas, parabolaBottom }, Accuracy.LinearResolution * 100, tracker);
                Body hole = ShapeHelper.CreateCylinder(cell.Center, cell.Center - Vector.Create(0, 0, focusMaterialThickness), circuitBoardLedHoleSize);
                outside.Subtract(new[] { hole });

                // Troughs
                BallMill troughBallMill = BallMill.StandardSizes.Values.Where(b => Accuracy.EqualLengths(b.Radius * 2, (double)3 / 8 * inches)).First();
                double troughWidth = (double)1 / 2 * inches;
                double troughUpperOffsetIn = troughWidth / 2 - troughBallMill.Radius;
                double troughDepth = (double)5 / 16 * inches;
                double troughLowerOffsetDown = troughDepth - troughBallMill.Radius;
                ICollection<ITrimmedCurve> troughUpperPath = cell.Edges.Cast<ITrimmedCurve>().ToArray()
                    .OffsetTowards(cell.Center, Plane.PlaneXY, troughUpperOffsetIn)
                    .Select(c => (ITrimmedCurve)c.CreateTransformedCopy(Matrix.CreateTranslation(Direction.DirZ * -focusMaterialThickness))).ToArray();
                ICollection<ITrimmedCurve> troughLowerPath = cell.Edges.Cast<ITrimmedCurve>()
                    .Select(c => (ITrimmedCurve)c.CreateTransformedCopy(Matrix.CreateTranslation(Direction.DirZ * (-focusMaterialThickness + troughLowerOffsetDown)))).ToArray();

                try {
                    outside.Subtract(troughLowerPath.Select(c => ShapeHelper.CreateCable(c, troughBallMill.Radius * 2)).ToArray());
                    outside.Subtract(troughUpperPath.Select(c => ShapeHelper.CreateCable(c, troughBallMill.Radius * 2)).ToArray());
                }
                catch { }

                if (outside.PieceCount > 1)
                    outside = outside.SeparatePieces().OrderBy(b => b.Volume).Last();

                outside.Subtract(new[] { cell.LedClearanceBody.Copy() });

                newParabolas = newParabolas.Select(f => tracker.GetSurvivors(f).First()).ToArray();
                newParabolaBottom = tracker.GetSurvivors(newParabolaBottom).First();
                desBody = DesignBody.Create(part, "Reflector", outside);
                desBody.Layer = reflectorLayer;
                BallMill smallerBall = BallMill.StandardSizes.Values.Where(b => Accuracy.EqualLengths(b.Radius * 2, (double)3 / 16 * inches)).First();
                var contouringParams = new CuttingParameters(smallerBall.Radius, 1, 0.25 * inches);
                var bottommingParams = new CuttingParameters(smallerBall.Radius / 2, 1, 0.25 * inches);
                foreach (Face face in newParabolas) {
                    //   var toolPath = new FaceToolPath(face, smallerBall, contouringParams, FaceToolPath.StrategyType.UV);
                    // hetre     FaceToolPathObject.Create(desBody.Faces.Where(f => f.Shape == face).First(), toolPath, System.Drawing.Color.DarkCyan);
                }


                //     var bottommingToolPath = new FaceToolPath(newParabolaBottom, smallerBall, bottommingParams, FaceToolPath.StrategyType.Spiral);
                //here     FaceToolPathObject.Create(desBody.Faces.Where(f => f.Shape == newParabolaBottom).First(), bottommingToolPath, System.Drawing.Color.DarkOrange);
                //   Body insideTop = ArcLoft(domeHeight, cell.Center, insetBody);
                Matrix m = Matrix.CreateTranslation(Direction.DirZ * domeRootThickness);
                Body nominalBody2 = Body.CreatePlanarBody(Plane.PlaneXY, nominalPath);
                Body outsideTop = ArcLoft(domeHeight - domeRootThickness, m * cell.Center, nominalBody2.CreateTransformedCopy(m));
                Body blockTop = Body.ExtrudeProfile(Plane.PlaneXY, nominalPath, domeRootThickness);
                //     var heights = outside.Faces.Select(f => f.Loops.SelectMany(l => l.Vertices).Select(v => v.Position.Z).Average()).ToArray();
                blockTop.DeleteFaces(blockTop.Faces.Where(f => Accuracy.EqualLengths(f.Loops.SelectMany(l => l.Vertices).Select(v => v.Position.Z).Average(), domeRootThickness)).ToArray(), RepairAction.None);
                // blockTop.DeleteFaces(blockTop.Faces.Where(f => Direction.Cross(Direction.DirZ, f.Geometry.Evaluate(PointUV.Origin).Normal).IsZero).ToArray(), RepairAction.None);
                //  outsideTop.Fuse(new[] { blockTop }, true, null);
                outsideTop.Stitch(new[] { blockTop }, Accuracy.LinearResolution * 10, null);
                desBody = DesignBody.Create(part, "Lens", outsideTop);
                desBody.Layer = lensLayer;
                desBody.Style = BodyStyle.Transparent;




            }
            ICollection<ITrimmedCurve> outerPath = outerEdges.Cast<ITrimmedCurve>().ToArray().OffsetTowards(Point.Origin, Plane.PlaneXY, -webWidth / 2);
            outerPath.Print();
        }

        private static Part CreateRotatedInstanceWithPart(double focusHeight, Part LedPart, Part mainPart, Cell cell, double angle) {
            Part part;
            part = Part.Create(mainPart.Document, string.Format("Cell {0}", cell.Index));
            cell.Component = Component.Create(mainPart, part);
            cell.LedComponent = CreateRotatedInstance(part, LedPart, cell.Center.Vector - focusHeight * Direction.DirZ, angle);
            return part;
        }
#if true
        public static double AbsAngleDifference(double angle1, double angle2) {
            double val = Math.Abs(angle1 - angle2);
            return val > Math.PI ? Const.Tau - val : val;
        }
#else
        public static double AbsAngleDifference(double angle1, double angle2) {
            return Math.Abs(((angle1 - angle2 + 4 * Math.PI) % (2 * Math.PI)) - Math.PI);
        }
#endif
        private static void CreateVoronoi(int count, List<Point> points, out Dictionary<VectorVoronoi, Cell> cells, out List<VectorVoronoi> vectors, out List<CurveSegment> outerEdges) {
            cells = new Dictionary<VectorVoronoi, Cell>();
            vectors = points.Select(p => new VectorVoronoi(new double[] { p.X, p.Y })).ToList();
            VoronoiGraph graph = Fortune.ComputeVoronoiGraph(vectors);
            vectors = vectors.Take(count).ToList();
            outerEdges = new List<CurveSegment>();
            //   List<CurveSegment> delauny = new List<CurveSegment>();
            foreach (VoronoiEdge edge in graph.Edges) {
                if (
                    double.IsNaN(edge.VVertexA.X) ||
                    double.IsNaN(edge.VVertexA.Y) ||
                    double.IsNaN(edge.VVertexB.X) ||
                    double.IsNaN(edge.VVertexB.Y)
                    )
                    continue;
                int borderEdgeCount = 0;
                if (vectors.Contains(edge.LeftData)) {
                    if (!cells.ContainsKey(edge.LeftData))
                        cells[edge.LeftData] = new Cell(Point.Create(edge.LeftData.X, edge.LeftData.Y, 0), vectors.IndexOf(edge.LeftData));
                    cells[edge.LeftData].AddVoironoiEdge(edge);
                    borderEdgeCount++;
                }
                if (vectors.Contains(edge.RightData)) {
                    if (!cells.ContainsKey(edge.RightData))
                        cells[edge.RightData] = new Cell(Point.Create(edge.RightData.X, edge.RightData.Y, 0), vectors.IndexOf(edge.RightData));
                    cells[edge.RightData].AddVoironoiEdge(edge);
                    borderEdgeCount++;
                }
                if (borderEdgeCount == 1) {
                    Point pA = Point.Create(edge.VVertexA.X, edge.VVertexA.Y, 0);
                    Point pB = Point.Create(edge.VVertexB.X, edge.VVertexB.Y, 0);
                    outerEdges.Add(CurveSegment.Create(pA, pB));
                }
                if (borderEdgeCount == 2) {
                    cells[edge.RightData].Neighbors.Add(cells[edge.LeftData]);
                    cells[edge.LeftData].Neighbors.Add(cells[edge.RightData]);

                    //         delauny.Add(CurveSegment.Create(cells[edge.RightData].Center, cells[edge.LeftData].Center));
                }
            }

            Cell[] orderedCells = cells.Values.OrderBy(c => c.Center.Vector.MagnitudeSquared()).ToArray();
            for (int i = 0; i < orderedCells.Length; i++)
                orderedCells[i].Index = i;
        }

        private static Component CreateRotatedInstance(Part MainPart, Part seedPart, Vector position, double angle) {
            Component component = Component.Create(MainPart, seedPart);
            component.Transform(
                Matrix.CreateTranslation(position) *
                Matrix.CreateRotation(Frame.World.AxisZ, angle)
                );
            return component;
        }
        private static Body ParabolaLoft(double focusHeight, Point center, Body insetBody) {
            List<ITrimmedCurve> parabolaCurves = new List<ITrimmedCurve>();
            List<Body> domeBodies = new List<Body>();
            Point focus = center - Direction.DirZ * focusHeight;
            foreach (Fin fin in insetBody.Faces.First().Loops.First().Fins) {
                const double loftResoluion = 0.002;
                int loftCount = Math.Max((int)(fin.Edge.Length / loftResoluion), 2);
                ITrimmedCurve[][] profiles = new ITrimmedCurve[loftCount][];
                for (int i = 0; i < loftCount; i++) {
                    double t = (double)i / (loftCount - 1) * fin.Edge.Bounds.Span + fin.Edge.Bounds.Start;
                    CurveEvaluation eval = fin.Edge.Geometry.Evaluate(t);
                    Point insetCurvePoint = eval.Point - center.Vector;
                    Frame frame = Frame.Create(focus, -Direction.DirZ, insetCurvePoint.Vector.Direction);
                    Plane plane = Plane.Create(frame);
                    //   plane.Print();
                    Matrix trans = Matrix.CreateMapping(frame);
                    double x = insetCurvePoint.Vector.Magnitude;
                    //    CurveSegment.Create(frame.Origin, frame.Origin + insetCurvePoint.Vector).Print();
                    double r0 = Math.Sqrt(x * x + focusHeight * focusHeight);
                    double theta0 = Math.Atan2(x, -focusHeight);
                    double a = (double)-1 / 2 * r0 * (1 + Math.Cos(theta0));
                    int pointCount = 128;
                    Point[] parabolaPoints = new Point[pointCount];
                    for (int j = 0; j < pointCount; j++) {
                        double theta = Math.PI / 2 + (theta0 - Math.PI / 2) * j / (pointCount - 1);  // from top to same z as focus
                        double r = (double)-2 * a / (1 + Math.Cos(theta));
                        parabolaPoints[j] = trans * Point.Create(r * Math.Cos(theta), r * Math.Sin(theta), 0);
                    }
                    //double radius = 0.125 * 0.0254;
                    //for (int j = 1; j < pointCount - 1; j++) {
                    //    Direction dir = -((parabolaPoints[j] - parabolaPoints[j - 1]).Direction.UnitVector + (parabolaPoints[j] - parabolaPoints[j + 1]).Direction.UnitVector).Direction;
                    //    Point toolCenter = parabolaPoints[j] + radius * dir;
                    //    CurveSegment.Create(toolCenter, toolCenter - Direction.DirZ * radius).Print();
                    //}
                    profiles[i] = new ITrimmedCurve[] { CurveSegment.Create(NurbsCurve.CreateThroughPoints(false, parabolaPoints, Accuracy.LinearResolution * 10)) };
                    //   profiles[i].Print();
                }

                Body body = Body.LoftProfiles(profiles, false, false);
                domeBodies.Add(body);
                Face face = body.Faces.First();
                Edge topEdge = null;
                foreach (Edge edge in face.Edges) {
                    if (!Accuracy.LengthIsZero(edge.StartPoint.Z))
                        continue;
                    if (!Accuracy.LengthIsZero(edge.EndPoint.Z))
                        continue;
                    topEdge = edge;
                }
                Surface surface = face.Geometry;
                UV<Parameterization> param = surface.Parameterization;
                double startU = param.U.Bounds.Start.GetValueOrDefault();
                double endU = param.U.Bounds.End.GetValueOrDefault();
                double startV = param.V.Bounds.Start.GetValueOrDefault();
                double endV = param.V.Bounds.End.GetValueOrDefault();
                SurfaceEvaluation surfEval = surface.Evaluate(PointUV.Create((startU + endU) / 2, (startV + endV) / 2));
                Double sign = Vector.Dot(surfEval.Normal.UnitVector, Direction.DirZ.UnitVector);
                bool normalFlip = sign < 0; //face.IsReversed;
                double vStep = 0.125 * 0.0254;
                int vCount = (int)Math.Max(topEdge.Length / vStep, 2);
                //     int uCount = 32;
                //for (int j = 0; j < vCount; j++) {
                //    double v = startV + (double)j / (vCount - 1) * (endV - startV);
                //    Point[] points = new Point[uCount];
                //    Direction[] normals = new Direction[uCount];
                //    for (int i = 0; i < uCount; i++) {
                //        double u = startU + (double)i / (uCount - 1) * (endU - startU);
                //        SurfaceEvaluation eval = surface.Evaluate(PointUV.Create(u, v));
                //        points[i] = eval.Point;
                //        normals[i] = normalFlip ? -eval.Normal : eval.Normal;
                //    }
                //    double radius = 0.125 * 0.0254;
                //    for (int i = 0; i < uCount; i++) {
                //        Point toolCenter = points[i] + radius * normals[i];
                //        CurveSegment.Create(Circle.Create(Frame.Create(toolCenter, Direction.Cross(normals[i], Direction.DirZ)), radius)).Print();
                //    }
                //}
            }
            // domeBodies.Print();
            Body targetBody = domeBodies[0];
            domeBodies.RemoveAt(0);
            while (domeBodies.Count > 0) {
                Body toolBody = domeBodies[0];
                domeBodies.RemoveAt(0);
                targetBody.Fuse(new Body[] { toolBody }, true, null);
            }
            //  targetBody.Print();
            //  return domeBodies.TryStitchBodies().First();
            return targetBody;
        }
        private static Body ArcLoft(double domeHeight, Point center, Body insetBody) {
            List<Body> domeBodies = new List<Body>();
            foreach (Fin fin in insetBody.Faces.First().Loops.First().Fins) {
                const double loftResoluion = 0.004;
                int loftCount = Math.Max((int)(fin.Edge.Length / loftResoluion), 2);
                ITrimmedCurve[][] profiles = new ITrimmedCurve[loftCount][];
                for (int i = 0; i < loftCount; i++) {
                    double t = (double)i / (loftCount - 1) * fin.Edge.Bounds.Span + fin.Edge.Bounds.Start;
                    CurveEvaluation eval = fin.Edge.Geometry.Evaluate(t);
                    Point insetCurvePoint = eval.Point;
                    Point apex = center + Direction.DirZ * domeHeight;
                    Frame domeFrame = Frame.Create(center, (insetCurvePoint - center).Direction, Direction.DirZ);
                    Plane plane = Plane.Create(domeFrame);
                    Line tangentLine = Line.Create(apex, domeFrame.AxisX.Direction);
                    Circle circle = Circle.CreateTangentToOne(plane, new CurveParam(tangentLine, 0), apex, insetCurvePoint);
                    double param0 = circle.ProjectPoint(center).Param;
                    double param1 = circle.ProjectPoint(insetCurvePoint).Param;
                    profiles[i] = new ITrimmedCurve[] { CurveSegment.Create(circle, Interval.Create(param1, param0)) };
                }
                domeBodies.Add(Body.LoftProfiles(profiles, false, false));
            }
            return domeBodies.TryStitchBodies().First();
        }
        private static Body RuledLoft(IList<ITrimmedCurve> curves) {
            Body[] bodies = new Body[curves.Count];
            for (int i = 0; i < curves.Count; i++)
                bodies[i] = Body.LoftProfiles(new ICollection<ITrimmedCurve>[] { 
                    new ITrimmedCurve[] { curves[i] },
                    new ITrimmedCurve[] { curves[(i + 1) % curves.Count] }
                }, false, false);
            return bodies.TryUnionBodies().First();
        }
        class Cell {
            public Point Center { get; set; }
            public Cell Next { get; set; }
            public int VoronoiIndex { get; set; }
            public int Index { get; set; }
            public List<CurveSegment> Edges { get; set; }
            public List<Cell> Neighbors { get; set; }
            public Component Component { get; set; }
            public Component LedComponent { get; set; }
            public DesignBody LedDesBody { get; set; }
            public Body LedClearanceBody { get; set; }
            public Cell(Point center, int index) {
                Center = center;
                Edges = new List<CurveSegment>();
                Neighbors = new List<Cell>();
                VoronoiIndex = index;
            }
            public void AddVoironoiEdge(VoronoiEdge edge) {
                Point pA = Point.Create(edge.VVertexA.X, edge.VVertexA.Y, 0);
                Point pB = Point.Create(edge.VVertexB.X, edge.VVertexB.Y, 0);
                Edges.Add(CurveSegment.Create(pA, pB));
            }
        }

#endif

#if false // matrix interp two components
		static void XXX_Executing(object sender, EventArgs e) {
			Window activeWindow = Window.ActiveWindow;
			List<IDocObject> selection = activeWindow.ActiveContext.Selection.ToList();
			if (selection.Count != 2)
				return;
			Component a = selection[0] as Component;
			Component b = selection[1] as Component;
			if (a == null || b == null || a.Template != b.Template) 
				return;
			int steps = 10;
			for (int i = 1; i < steps; i++) {
				double t = (double) i / steps;
				Component component = Component.Create(a.Parent, a.Template);
				component.Placement = Interpolation.Interpolate(a.Placement, b.Placement, t);
			}
		}
#endif
#if false // Sphere Eversion
		static void XXX_Executing(object sender, EventArgs e) {
			Window activeWindow = Window.ActiveWindow;
			List<Tope> topes = new List<Tope>();
			string path = @"C:\Users\bcr\Documents\Models\Sphere Eversion\avn\h2d78.mov";
			BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open));
			string text;
			while (true) {
				text = reader.ReadLine();
				if (text.Length >= 6 && text.Substring(0, 6) == "(exit)")
					break;
				if (text.Contains("OFF"))
					topes.Add(new Tope(reader));
			}
			reader.Close();
			Part mainPart = Window.ActiveWindow.Scene as Part;
			//foreach (Point point in topes.First().verts)
			//    DesignCurve.Create(mainPart, CurveSegment.Create(PointCurve.Create(point)));
			int topeNum = 0;
			Matrix rotation = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), Math.PI);
			double diameter = 0.002;
			List<Point> allPoints = new List<Point>();
			foreach (Tope tope in topes) {
				if (topeNum++ % 4 != 0)
					continue;
				Window window = Document.Create();
				mainPart = window.Scene as Part;
				Matrix scale = Matrix.CreateScale(3.5 * 25.4 / 5000);
				Point[] verts = new Point[tope.nverts];
				for (int i = 0; i < verts.Length; i++)
					verts[i] = scale * tope.verts[i];
				Part part = Part.Create(mainPart, string.Format("Tope {0:00}", (topeNum - 1) / 4));
				Component component = Component.Create(mainPart, part);
				var segments = new List<Segment>();
				for (int i = 0; i < tope.nfaces; i++) {
					var indices = new List<int>();
					for (int j = 0; j < tope.nfv[i]; j++)
						indices.Add(tope.fv[tope.fv0[i] + j]);
					for (int j = 0; j < indices.Count; j++) {
						int a = indices[j];
						int b = indices[(j + 1) % indices.Count];
						Segment segment = Segment.Create(Math.Min(a, b), Math.Max(a, b));
						if (!segments.Contains(segment))
							segments.Add(segment);
					}
				}
				var bodies = new List<Body>();
				//Part topeContainer = Part.Create(part, "TopeHolder");
				//Component topeComponent = Component.Create(part, topeContainer);
				//topeComponent.Transform(Matrix.CreateTranslation(Vector.Create(0, 0, 0)));
				var desCurves = new List<DesignCurve>();
				foreach (Segment segment in segments) {
					Point pointA = verts[segment.Item1].Quantify();
					Point pointB = verts[segment.Item2].Quantify();
					bodies.Add(ShapeHelper.CreateCylinder(pointA, pointB, diameter));
					allPoints.Add(pointA);
					allPoints.Add(pointB);
					pointA = (rotation * pointA).Quantify();
					pointB = (rotation * pointB).Quantify();
					bool isMatching = false;
					foreach (Segment otherSegment in segments) {
						if ((PointsAreClose(pointA, verts[otherSegment.Item1]) && PointsAreClose(pointB, verts[otherSegment.Item2])) || (PointsAreClose(pointA, verts[otherSegment.Item2]) && PointsAreClose(pointB, verts[otherSegment.Item1])))
							isMatching = true;
					}
					allPoints.Add(pointA);
					allPoints.Add(pointB);

					if (!isMatching)
						bodies.Add(ShapeHelper.CreateCylinder(pointA, pointB, diameter));

					DesignCurve desCurve = DesignCurve.Create(mainPart, CurveSegment.Create(verts[segment.Item1], verts[segment.Item2]));
					desCurves.Add(desCurve);
					desCurve = desCurve.Copy();
					desCurve.Transform(rotation);
					desCurves.Add(desCurve);
				}
				//Box box = Box.Empty;
				//box |= Box.Create(desCurves.Select(b => b.Shape).Select(s => s.StartPoint).ToArray());
				//box |= Box.Create(desCurves.Select(b => b.Shape).Select(s => s.EndPoint).ToArray());
				//box = box.Inflate(diameter / 2);
				//GenerateKizamuInputData(desCurves, diameter, box);
				//foreach (Point point in verts)
				//    bodies.Add(ShapeHelper.CreateSphere(point, diameter));
				foreach (Body body in bodies) {
					DesignBody desbody = DesignBody.Create(part, "tope", body);
				}
				mainPart.Document.SaveAs(string.Format(@"c:\tope {0:00}.1.scdoc", (topeNum - 1) / 4));
				window.Delete();
				break;
			}
		}
		//	ShapeHelper.CreateBlock(Box.Create(allPoints).Inflate(diameter / 2), mainPart);

		public static void GenerateKizamuInputData(IList<DesignCurve> desCurves, double diameter, Box box) {
			StreamWriter writer = new StreamWriter(@"C:\Users\bcr\Desktop\EversionTest1.txt");
			writer.WriteLine("// Set the generation and rendering attributes");
			writer.WriteLine("set dimension 3");
			writer.WriteLine("set minLevel 0");
			writer.WriteLine("set maxLevel 10");
			writer.WriteLine("set tileDepth 3");
			writer.WriteLine("set maxError 0.005");
			writer.WriteLine("set distEps 0.0");
			writer.WriteLine("set euclidean 1");
			writer.WriteLine("set renderAsSurface 1");
			writer.WriteLine("set imageSize 1024");
			double maxSize = Math.Max(Math.Max(box.Size.X, box.Size.Y), box.Size.Z);
			Matrix trans = Matrix.CreateScale(1 / maxSize) * Matrix.CreateTranslation(-box.MinCorner.Vector);
			var objectNames = new List<string>();
			string objectName;
			for (int i = 0; i < desCurves.Count; i++) {
				//for (int i = 0; i < 500; i++) {
				desCurves[i].Transform(trans);
				objectName = string.Format("Line{0:00}", i + 1);
				writer.WriteLine(string.Format("define {0} line3D {1} {2} {3} {4} {5} {6} {7}", objectName,
					desCurves[i].Shape.StartPoint.X, desCurves[i].Shape.StartPoint.Y, desCurves[i].Shape.StartPoint.Z,
					desCurves[i].Shape.EndPoint.X, desCurves[i].Shape.EndPoint.Y, desCurves[i].Shape.EndPoint.Z,
					diameter / 2 / maxSize)
				);
				objectNames.Add(objectName);
			}
			foreach (string str in objectNames)
				writer.WriteLine(String.Format("command push {0}", str));
			for (int i = 0; i < objectNames.Count - 1; i++)
				writer.WriteLine("command add");
			writer.Close();
		}
		public static Point Quantify(this Point point) {
			const int quantization = 6;
			return Point.Create(
				Math.Round(point.X, quantization),
				Math.Round(point.Y, quantization),
				Math.Round(point.Z, quantization)
			);
		}
		public static bool PointsAreClose(Point pointA, Point pointB) {
			return
				(pointA.X - pointB.X) * (pointA.X - pointB.X) +
				(pointA.Y - pointB.Y) * (pointA.Y - pointB.Y) +
				(pointA.Z - pointB.Z) * (pointA.Z - pointB.Z) -
				(Accuracy.LinearResolution * 10000) * (Accuracy.LinearResolution * 10000) < 0;
		}

		class Tope {
			public int nverts { get; set; }
			public Point[] verts { get; set; }
			public int nfaces { get; set; }
			public int[] nfv { get; set; }		/* nfv[nfaces] : number of verts on each face */
			public int[] fv0 { get; set; }		/* fv0[nfaces] : starting index in fv[] */
			public List<int> fv { get; set; }		/* fv[totfv] : vertex indices per face */
			public Tope(BinaryReader reader) {
				nverts = reader.ReadBigEndianInt();
				nfaces = reader.ReadBigEndianInt();
				reader.ReadBigEndianInt();
				Debug.Assert(!(nverts < 0 || nverts > 100000000 || nfaces < 0 || nfaces > 100000000), "Looks like wrong number of verts and faces.");
				verts = new Point[nverts];
				for (int i = 0; i < nverts; i++)
					verts[i] = Point.Create(
						(double) reader.ReadBigEndianFloat(),
						(double) reader.ReadBigEndianFloat(),
						(double) reader.ReadBigEndianFloat()
						);
				nfv = new int[nfaces];
				fv0 = new int[nfaces];
				fv = new List<int>();
				for (int i = 0; i < nfaces; i++) {
					int numFaceVerts = reader.ReadBigEndianInt();
					Debug.Assert(!(numFaceVerts <= 0 || numFaceVerts > 100000), "Unreasonable number of verts (%d) on face %d of tope %d (file offset %d)\n");
					nfv[i] = numFaceVerts;
					fv0[i] = fv.Count;
					for (int k = 0; k < numFaceVerts; k++)
						fv.Add(reader.ReadBigEndianInt());
					int numColorComponents = reader.ReadBigEndianInt(); //color size
					for (int k = 0; k < numColorComponents; k++)
						reader.ReadBigEndianFloat(); //throw it away
				}
			}
		}
		class Segment : IComparable<Segment>, IEqualityComparer<Segment>, IEquatable<Segment> {
			public int Item1 { get; set; }
			public int Item2 { get; set; }
			public Segment(int a, int b) {
				this.Item1 = a;
				this.Item2 = b;
			}
			public static Segment Create(int a, int b) {
				return new Segment(a, b);
			}
        #region IComparable Members
			public int CompareTo(Segment other) {
				if (Item1 < other.Item1)
					return -1;
				if (Item1 > other.Item1)
					return 1;
				if (Item2 < other.Item2)
					return -1;
				if (Item2 > other.Item2)
					return 1;
				return 0;
			}
        #endregion
        #region IEqualityComparer<Tuple> Members
			public bool Equals(Segment x, Segment y) {
				return x.Item1 == y.Item1 && x.Item2 == y.Item2;
			}
			public int GetHashCode(Segment obj) {
				return obj.Item1.GetHashCode() ^ obj.Item2.GetHashCode();
			}
        #endregion
        #region IEquatable<Tuple> Members
			public bool Equals(Segment other) {
				return Equals(this, other);
			}
        #endregion
		}
#endif

#if false
		static void XXX_Executing(object sender, EventArgs e) {
			//ITrimmedCurve iTrimmedCurve = (Window.ActiveWindow.ActiveContext.SingleSelection as DesignCurve).Shape;
			//DesignCurve.Create(Window.ActiveWindow.ActiveContext.ActivePart, iTrimmedCurve.CreateHelixAroundCurve(20, 0.05, 10000));
			//return;
			var primaryPoints = new List<Point>();
			double length = 1;
			double primaryTurns = 4;
			double primaryRadius = 0.5;
			double secondaryTurns = 10 * primaryTurns;
			double secondaryRadius = 0.08;
			double tertiaryTurns = 10 * secondaryTurns;
			double tertiaryRadius = 0.02;
			Line axis = Line.Create(Point.Origin, Direction.DirZ);
			int count = 10000;
			for (int i = 0; i < count; i++) {
				double ratio = (double)i / count;
				Point point = Point.Create(primaryRadius, 0, length * ratio);
				Frame frame = Frame.Create(point, Direction.DirX, Direction.DirY);  // TBD should be angled
				Matrix primaryRotation = Matrix.CreateRotation(axis, 2 * Math.PI * primaryTurns * ratio);
				point = primaryRotation * point;
				frame = primaryRotation * frame;
				primaryPoints.Add(point);
			}
			CurveSegment curveSegment = CurveSegment.Create(NurbsCurve.CreateFromKnotPoints(false, primaryPoints));
			curveSegment = CreateHelixAroundCurve(curveSegment, secondaryTurns, secondaryRadius, count);
			curveSegment = CreateHelixAroundCurve(curveSegment, tertiaryTurns, tertiaryRadius, count);
			DesignCurve.Create(Window.ActiveWindow.ActiveContext.ActivePart, curveSegment);
		}
	//{
			//var points = new List<Point>();
			//double length = 1;
			//double primaryTurns = 4;
			//double primaryRadius = 0.5;
			//double secondaryTurns = 10 * primaryTurns;
			//double secondaryRadius = 0.08;
			//double tertiaryTurns = 10 * secondaryTurns;
			//double tertiaryRadius = 0.02;
			//Line axis = Line.Create(Point.Origin, Direction.DirZ);
			//int count = 10000;
			//for (int i = 0; i < count; i++) {
			//    double ratio = (double)i / count;
			//    Point point = Point.Create(primaryRadius, 0, length * ratio);
			//    Frame frame = Frame.Create(point, Direction.DirX, Direction.DirY);  // TBD should be angled
			//    Matrix primaryRotation = Matrix.CreateRotation(axis, 2 * Math.PI * primaryTurns * ratio);
			//    point = primaryRotation * point;
			//    frame = primaryRotation * frame;
			//    Matrix secondaryRotation = Matrix.CreateRotation(Line.Create(point, frame.DirY), 2 * Math.PI * secondaryTurns * ratio);
			//    point += frame.DirX * secondaryRadius;
			//    point = secondaryRotation * point;
			//    frame = secondaryRotation * frame;
			//    Matrix tertiaryRotation = Matrix.CreateRotation(Line.Create(point, frame.DirY), 2 * Math.PI * tertiaryTurns * ratio);
			//    point += frame.DirX * tertiaryRadius;
			//    point = tertiaryRotation * point;
			//    frame = tertiaryRotation * frame;
			//    points.Add(point);
			//}
			//DesignCurve.Create(Window.ActiveWindow.ActiveContext.ActivePart, CurveSegment.Create(NurbsCurve.CreateThroughPoints(false, points, 0.000001)));
		}
#elif false
		static double XSquared(double x) {
			return x * x;
		}
		static void XXX_Executing(object sender, EventArgs e) {
	
		double x = MathHelper.IntegrateSimple(0,1,100, XSquared);

			OpenFileDialog fileDialog = new OpenFileDialog();
			fileDialog.Filter = "Mapgen dat files (*.dat.txt)|*.dat.txt";
			fileDialog.Title = "Open Mapgen data";
			if (fileDialog.ShowDialog(SpaceClaim.Api.V10.Application.MainWindow) != DialogResult.OK)
				return;
			StreamReader file = File.OpenText(fileDialog.FileName);
			string line;
			string regexp = @"([-\d\.]+)\s+([-\d\.]+)";
			var profiles = new List<List<Point>>();
			Match match;
			double lat = 0, lon = 0;
			while (!file.EndOfStream) {
				line = file.ReadLine();
				match = Regex.Match(line, regexp);
				if (match.Success) {
					if (!double.TryParse(match.Groups[1].Value, out lon))
						continue;
					if (!double.TryParse(match.Groups[2].Value, out lat))
						continue;
				}
				else {
					match = Regex.Match(line, "#");
					if (match.Success) {
						profiles.Add(new List<Point>());
						continue;
					}
					else
						Debug.Fail("Unhandled line: \n" + line);
				}
				lat *= Math.PI / 180;
				lon *= Math.PI / 180;
				profiles[profiles.Count - 1].Add(Point.Create(
					Math.Cos(lon) * Math.Cos(lat),
					Math.Sin(lon) * Math.Cos(lat),
					Math.Sin(lat)
				));
			}
			Part part = Window.ActiveWindow.Scene as Part;
			
			Body body = null;
			foreach (DesignBody designBody in part.Bodies) {
				body = designBody.Shape;
				break;
			}
			var faceCurves = new Dictionary<DesignCurve, Face>();
			FlatPattern flatPattern = new FlatPattern(body, null, false, true, "Flat Pattern");
			flatPattern.Render();
			string etchingLayerName = "Etching";
			System.Drawing.Color etchingLayerColor = System.Drawing.Color.FromArgb(255, 0, 255);
			profiles = profiles.CloseProfiles();
			int count = 0;
			foreach (List<Point> profile in profiles) {
				var cleanProfile = profile.CleanProfile(0.01);
				if (cleanProfile.Count < 3)
					continue;
				if (cleanProfile[0] != cleanProfile[cleanProfile.Count - 1])
					cleanProfile.Add(cleanProfile[0]);
				etchingLayerName = count.ToString();
				Random random = new Random(count);
				etchingLayerColor = System.Drawing.Color.FromArgb(
					random.Next(200),
					random.Next(200),
					random.Next(200)
				);
				for (int i = 0; i < cleanProfile.Count - 1; i++) {
					Point? p0 = null, p1 = null;
					Face face = TryProjectPointsToBodyFace(body, cleanProfile[i], cleanProfile[i + 1], out p0, out p1, false);
					if (p0 == null || p1 == null) {
						Matrix scale = Matrix.CreateScale(0.001);
						Body cutter = ShapeHelper.CreatePolygon(new Point[] {
			                        cleanProfile[i],
			                        cleanProfile[i + 1],
			                        scale * cleanProfile[i + 1],
			                        scale * cleanProfile[i]
			                    }, 0);
						cutter.Imprint(body.Copy());
						foreach (Edge edge in cutter.Edges) {
							if (edge.Fins.Count == 2) {
								//DesignCurve desCurve = DesignCurve.Create(part, edge);
								//desCurve.Layer = NoteHelper.CreateOrGetLayer(Window.ActiveWindow.Document, "Etching-sibs", System.Drawing.Color.Purple);
								Face imprintFace = TryProjectPointsToBodyFace(body, edge.StartPoint, edge.EndPoint, out p0, out p1, true);
								Matrix imprintTransform = flatPattern.FlatFaceMapping[imprintFace].Transform;
								CreateTransformedDesignCurveOnLayer(part, edge.StartPoint, edge.EndPoint, Matrix.Identity /* imprintTransform */, "Etching-sibs", System.Drawing.Color.Purple);
							}
						}
					}
					else {
						Matrix transform = flatPattern.FlatFaceMapping[face].Transform;
						CreateTransformedDesignCurveOnLayer(part, p0, p1, Matrix.Identity /* transform */, etchingLayerName, etchingLayerColor);
					}
				}
			}
		}
		/////////////////////////////////
#endif
#if false	
							else if (lastFace != null && face != lastFace) {
								if (flatPattern.FlatFaceMapping[lastFace].IsAdjacentTo(flatPattern.FlatFaceMapping[face])) {
									desCurve = DesignCurve.Create(part, CurveSegment.Create(lastPoint.Value, p0.Value));
									desCurve.Layer = NoteHelper.CreateOrGetLayer(Window.ActiveWindow.Document, "Etching-adjacent", System.Drawing.Color.Pink);
								}
								else {
									Point? facePoint = null, otherProfilePoint = null;
									if (p0 == null) {
										facePoint = p1;
										otherProfilePoint = cleanProfile[i];
										intersections = face.Geometry.IntersectCurve(Line.Create(Point.Origin, cleanProfile[i].Vector.Direction));
										if (intersections.Count > 0)
											p0 = intersections.ToArray()[0].Point;
									}
									if (p1 == null) {
										facePoint = p0;
										otherProfilePoint = cleanProfile[i + 1];
										intersections = face.Geometry.IntersectCurve(Line.Create(Point.Origin, cleanProfile[i + 1].Vector.Direction));
										if (intersections.Count > 0)
											p1 = intersections.ToArray()[0].Point;
									}
									intersections = lastFace.Geometry.IntersectCurve(Line.Create(Point.Origin, otherProfilePoint.Value.Vector.Direction));
									Debug.Assert(intersections.Count > 0);
									Point otherPoint = intersections.ToArray()[0].Point;
									Matrix otherTransform = flatPattern.FlatFaceMapping[lastFace].Transform;
									facePoint = otherTransform * facePoint;
									otherPoint = otherTransform * otherPoint;
									desCurve = DesignCurve.Create(part, CurveSegment.Create(facePoint.Value, otherPoint));
									desCurve.Layer = NoteHelper.CreateOrGetLayer(Window.ActiveWindow.Document, "Etching-sibs", System.Drawing.Color.Purple);
								}
							}
#endif
        //	count++;
        //		}
        //	}

        //var nurbsCurve = NurbsCurve.CreateThroughPoints(false, cleanProfile, 10);
        //DesignCurve.Create(Window.ActiveWindow.Scene as Part, CurveSegment.Create(nurbsCurve));
        //	const double scale = 0.001;
        //ShapeHelper.CreatePolygon(new Point[] {
        //    profile[i],
        //    profile[i + 1],
        //    Matrix.CreateScale(scale, Point.Origin) * profile[i + 1],
        //    Matrix.CreateScale(scale, Point.Origin) * profile[i]
        //}, 0, Window.ActiveWindow.Scene as Part);
        //}
        //IList<IDesignCurve> designCurves = ShapeHelper.CreatePolygon(profile, Window.ActiveWindow.Scene as Part);
        //if (designCurves != null)
        //    designCurves[designCurves.Count - 1].Delete();

#if false // beams
			//ICollection<Beam> beams = Window.ActiveWindow.ActiveContext.GetSelection<Beam>();
			ICollection<Beam> beams = (Window.ActiveWindow.Scene as Part).Beams;
			IPart part = Window.ActiveWindow.ActiveContext.ActivePart;
			//temp
			foreach (IDesignCurve iDesignCurve in part.Curves) {
				Ellipse ellipse = iDesignCurve.Shape.ProjectToPlane(Plane.PlaneXY).Geometry as Ellipse;
				if (ellipse != null) 
					DesignCurve.Create(part, CurveSegment.Create(ellipse));
			}

			return;
			//end temp
			foreach (Beam beam in beams) {
				DesignCurve.Create(part, beam.Shape);
			//	Matrix trans = Matrix.CreateMapping(beam.SectionAnchor.
				foreach (ICollection<ITrimmedCurve> profile in beam.SectionProfile) {
					foreach (ITrimmedCurve iTrimmedCurve in profile) {
						DesignCurve.Create(part, iTrimmedCurve);
					}
				}
				//Frame profileFrame = Frame.Create(point1, dirX, dirY);
				//Plane profilePlane = Plane.Create(profileFrame);
				//IList<ITrimmedCurve> profile = new List<ITrimmedCurve>();
				//Line axisLine = Line.Create(point1, dirX);
				//profile.Add(CurveSegment.Create(axisLine, Interval.Create(-radius, lengthVector.Magnitude + radius)));
				//Circle circle1 = Circle.Create(profileFrame, radius);
				//profile.Add(CurveSegment.Create(circle1, Interval.Create(Math.PI / 2, Math.PI)));
				//Line tangentLine = Line.Create(Matrix.CreateTranslation(dirY * radius) * point1, dirX);
				//profile.Add(CurveSegment.Create(tangentLine, Interval.Create(0, lengthVector.Magnitude)));
				//Circle circle2 = Circle.Create(Frame.Create(point2, dirX, dirY), radius);
				//profile.Add(CurveSegment.Create(circle2, Interval.Create(0, Math.PI / 2)));
				//IList<ITrimmedCurve> path = new List<ITrimmedCurve>();
				//Circle sweepCircle = Circle.Create(Frame.Create(point1, dirY, dirZ), radius);
				//path.Add(CurveSegment.Create(sweepCircle, sweepCircle.Parameterization.Range.Value));
				//Body body = Body.SweepProfile(Plane.Create(profileFrame), profile, path);
				//if (body == null) {
				//    Debug.Fail("Profile was not connected, not closed, or swept along an inappropriate path.");
				//    return null;
				//}
				//DesignBody desBodyMaster = DesignBody.Create(part.Master, "Sausage", body);
				//desBodyMaster.Transform(part.TransformToMaster);
				//return desBodyMaster;
			}
#endif
#if false // Puzzle solving
			Window activeWindow = Window.ActiveWindow;
			Part mainPart = activeWindow.Scene as Part;
			if (mainPart == null)
				return;
			Part brickPart = null;
			Part assyPart = null;
			foreach (Component component in mainPart.Components) {
				if (component.Template.Name == "Brick")
					brickPart = component.Template;
				if (component.Template.Name == "Assy")
					assyPart = component.Template;
			}
			Debug.Assert(brickPart != null);
			Debug.Assert(assyPart != null);
			Body brickBody = null;
			foreach (DesignBody desBody in brickPart.Bodies) {
				brickBody = desBody.Shape;
				break;
			}
			Matrix trans = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), Math.PI / 2) *
							Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirY), Math.PI / 2);
			double stepSize = 0.01;
			Line twistAxis = Line.Create(Point.Create(0, stepSize, stepSize), Direction.DirX);
	//		DesignCurve.Create(mainPart, CurveSegment.Create(twistAxis, Interval.Create(0, 0.1)));
			for (int r = 0; r < 4; r++) {
				for (int i = 0; i < 3; i++) {
					for (int j = 0; j < 3; j++) {
						Body body1 = brickBody.Copy();
						body1.Transform(Matrix.CreateRotation(twistAxis, (double)r * Math.PI / 2));
						body1.Transform(Matrix.CreateTranslation(Vector.Create(0, (double)i * stepSize, (double)j * stepSize)));
						Body body2 = body1.Copy();
						body2.Transform(trans);
						ICollection<Body> mergedBodies = new Body[] { body1, body2 }.TryUnionBodies();
						if (mergedBodies.Count == 2) {
							Part topPart = Part.Create(mainPart, string.Format("r{0}: ({1}, {2})", r, i, j));
							Component topComponent = Component.Create(mainPart, topPart);
							Part fourBrickPart = Part.Create(mainPart, "four");
							Component.Create(topPart, fourBrickPart);
							Component.Create(topPart, fourBrickPart).Transform(trans);
							Component.Create(topPart, fourBrickPart).Transform(trans.Inverse);
							Part twoBrickPart = Part.Create(mainPart, "two");
							Component.Create(fourBrickPart, twoBrickPart);
							Component.Create(fourBrickPart, twoBrickPart).Transform(Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), Math.PI));
							Part brickPartNew = Part.Create(mainPart, "brick");
							Component.Create(twoBrickPart, brickPartNew);
							Component.Create(twoBrickPart, brickPartNew).Transform(Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirX), Math.PI));
							foreach (Body body in mergedBodies) {
								DesignBody.Create(brickPartNew, "Solution", body);
								break;
							}
						}
					}
				}
			}
#endif


        private static Face TryProjectPointsToBodyFace(Body body, Point i1, Point i2, out Point? p0, out Point? p1, bool isRequiringThatBothPointsProject) {
            p0 = p1 = null;
            foreach (Face face in body.Faces) {
                CurveSegment ray;
                ICollection<IntPoint<SurfaceEvaluation, CurveEvaluation>> intersections;
                ray = CurveSegment.Create(Point.Origin, i1);
                intersections = face.IntersectCurve(ray);
                if (intersections.Count > 0)
                    p0 = intersections.ToArray()[0].Point;
                ray = CurveSegment.Create(Point.Origin, i2);
                intersections = face.IntersectCurve(ray);
                if (intersections.Count > 0)
                    p1 = intersections.ToArray()[0].Point;
                if (isRequiringThatBothPointsProject) {
                    if (p0 != null && p1 != null)
                        return face;
                }
                else {
                    if (p0 != null || p1 != null)
                        return face;
                }
            }
            return null;
        }
        private static DesignCurve CreateTransformedDesignCurveOnLayer(Part part, Point? p0, Point? p1, Matrix transform, string layerName, System.Drawing.Color color) {
            p0 = transform * p0;
            p1 = transform * p1;
            DesignCurve desCurve = null;
            if (p0 == p1)
                desCurve = DesignCurve.Create(part, p0.Value.AsITrimmedCurve());
            else
                desCurve = DesignCurve.Create(part, CurveSegment.Create(p0.Value, p1.Value));
            desCurve.Layer = NoteHelper.CreateOrGetLayer(part.Document, layerName, color);
            return desCurve;
        }
        static bool TryBuildC2NurbsData(CurveSegment curve, bool isReversed, double scale, out List<Knot> knots, out List<ControlPoint> controlPoints) {
            knots = new List<Knot>();
            controlPoints = new List<ControlPoint>();
            const int steps = 4;
            scale /= Math.Pow(2, steps) - 1;
            List<double> evalParams = new List<double>();
            double t = 0;
            for (int i = 0; i <= steps * 3 + 1; i++) {
                Debug.Assert(curve.Geometry.TryOffsetParam(curve.Bounds.Start, scale * (Math.Pow(2, (double)i / 3) - 1), out t));
                evalParams.Add(t);
            }
            evalParams.Reverse();
            CurveEvaluation curveEvaluation = null;
            for (int i = 0; i < evalParams.Count; i++) {
                curveEvaluation = curve.Geometry.Evaluate(evalParams[i]);
                if (i % 3 == 0)
                    knots.Add(new Knot((double)i / 3, 3));
                controlPoints.Add(new ControlPoint(curveEvaluation.Point, 1));
                DesignCurve.Create(Window.ActiveWindow.Scene as Part, CurveSegment.Create(curveEvaluation.Point, Point.Create(0.01, 0.01, 0)));
            }
            //Debug.Assert(curve.Geometry.TryOffsetAlongCurve(curve.Bounds.Min, - scale * 6, out t));
            //curveEvaluation = curve.Geometry.Evaluate(t);
            //controlPoints.Add(new ControlPoint(curveEvaluation.Point, 1));
            //DesignCurve.Create(Window.ActiveWindow.Scene as Part, CurveSegment.Create(curveEvaluation.Point, Point.Create(0.01, 0.01, 0)));
            return true;  // TBD, gather the assert above
        }
        static void Curves_Executing(object sender, EventArgs e) {
            const double inches = 25.4 / 1000;
#if true
            ITrimmedCurve[] curves = Window.ActiveWindow.GetAllSelectedITrimmedCurves().ToArray();
            if (curves.Length != 2)
                Debug.Fail("Need exactly two curves");
            ITrimmedCurve curve0 = curves[0], curve1 = curves[1];

            Point curve1StartPoint = curve1.StartPoint;
            Point curve1EndPoint = curve1.EndPoint;
            if ((curve1.StartPoint - curve0.StartPoint).Magnitude > (curve1.EndPoint - curve0.StartPoint).Magnitude) {
                curve1StartPoint = curve1.EndPoint;
                curve1EndPoint = curve1.StartPoint;
            }
            int uSteps = 7, vSteps = 17;
            double tentHeight = 0.125 * inches;
            Point[,] mesh = new Point[uSteps, vSteps];
            for (int i = 0; i < uSteps; i++) {
                double uRatio = (double)i / (uSteps - 1);
                for (int j = 0; j < vSteps; j++) {
                    double vRatio = (double)j / (vSteps - 1);
                    mesh[i, j] =
                        Point.Create(0, 0, tentHeight * (1 - Math.Abs(2 * vRatio - 1))) +
                        //    Point.Create(0, 0, 0) +
                        uRatio * vRatio * curve0.StartPoint.Vector +
                        (1 - uRatio) * vRatio * curve0.EndPoint.Vector +
                        uRatio * (1 - vRatio) * curve1StartPoint.Vector +
                        (1 - uRatio) * (1 - vRatio) * curve1EndPoint.Vector;
                    mesh[i, j].Print();
                }
            }
            double targetLength = (double)1 / 60 / 2;
            double elasticity = 0;// 0.25;
            double elasticityZ = -0.00005;
            double uSpring = 0.0004;
            //   double vSpring = 0.0003;
            double vTangency = 0.15;
            double vSquare = 0.2;
            double targetUSpacing = curve0.Length / (uSteps - 1);
            double targetVSpacing = targetLength / (vSteps - 1);
            Plane plane = Plane.Create(Frame.Create(curve0.StartPoint, Direction.DirZ));
            Point[,] newMesh = new Point[uSteps, vSteps];
            ITrimmedCurve[][] loftCurves = new ITrimmedCurve[uSteps][];
            for (int n = 0; n < 99; n++) {
                loftCurves = new ITrimmedCurve[uSteps][];
                for (int i = 0; i < uSteps; i++) {
                    double uRatio = (double)i / (uSteps - 1);
                    Point[] points = new Point[vSteps];
                    for (int j = 0; j < vSteps; j++) {
                        double vRatio = (double)j / (vSteps - 1);
                        Vector force = Vector.Zero;
                        if (i + 1 < uSteps)
                            force += (elasticity * (mesh[i, j] - mesh[i + 1, j]) + elasticityZ * Direction.DirZ) * (targetUSpacing / (mesh[i, j] - mesh[i + 1, j]).Magnitude - 1);
                        if (i != 0)
                            force += (elasticity * (mesh[i, j] - mesh[i - 1, j]) + elasticityZ * Direction.DirZ) * (targetUSpacing / (mesh[i, j] - mesh[i - 1, j]).Magnitude - 1);
                        if (j + 1 < vSteps)
                            force += (elasticity * (mesh[i, j] - mesh[i, j + 1]) + elasticityZ * Direction.DirZ) * (targetVSpacing / (mesh[i, j] - mesh[i, j + 1]).Magnitude - 1);
                        if (j != 0)
                            force += (elasticity * (mesh[i, j] - mesh[i, j - 1]) + elasticityZ * Direction.DirZ) * (targetVSpacing / (mesh[i, j] - mesh[i, j - 1]).Magnitude - 1);
                        if (i == 2 && j == 6) {
                            Console.WriteLine((targetVSpacing / (mesh[i, j] - mesh[i, j + 1]).Magnitude - 1));
                            Console.WriteLine((targetVSpacing / (mesh[i, j] - mesh[i, j - 1]).Magnitude - 1));
                        }
                        if (i + 1 < uSteps && i != 0)
                            force -= uSpring * ((mesh[i, j].Vector - mesh[i + 1, j].Vector).Direction.UnitVector + (mesh[i, j].Vector - mesh[i - 1, j].Vector).Direction.UnitVector);
                        //if (j + 1 < vSteps && j != 0)
                        //    force -= vSpring * ((mesh[i, j].Vector - mesh[i, j + 1].Vector).Direction.UnitVector + (mesh[i, j].Vector - mesh[i, j - 1].Vector).Direction.UnitVector);
                        if (j == 1 || j == vSteps - 2)
                            force += vTangency * (mesh[i, j].ProjectToPlane(plane) - mesh[i, j]);
                        if (j == 2 || j == vSteps - 3)
                            force += vTangency / 2 * (mesh[i, j].ProjectToPlane(plane) - mesh[i, j]);
                        if (j == 3 || j == vSteps - 4)
                            force += vTangency / 4 * (mesh[i, j].ProjectToPlane(plane) - mesh[i, j]);
                        if (i < uSteps - 1 && !(j == 0 || j == vSteps - 1)) {
                            Plane midPlane = Plane.Create(Frame.Create(new Point[] { mesh[i + 1, j - 1], mesh[i + 1, j + 1] }.Average(), (mesh[i + 1, j - 1] - mesh[i + 1, j + 1]).Direction));
                            force += vSquare * (mesh[i, j].ProjectToPlane(midPlane) - mesh[i, j]);
                        }
                        if (i > 0 && !(j == 0 || j == vSteps - 1)) {
                            Plane midPlane = Plane.Create(Frame.Create(new Point[] { mesh[i - 1, j - 1], mesh[i - 1, j + 1] }.Average(), (mesh[i - 1, j - 1] - mesh[i - 1, j + 1]).Direction));
                            force += vSquare * (mesh[i, j].ProjectToPlane(midPlane) - mesh[i, j]);
                        }
                        if (j == vSteps - 1 || j == 0)
                            force = Vector.Zero;

                        newMesh[i, j] = mesh[i, j] + force;
                        newMesh[i, j].Print();
                        points[j] = newMesh[i, j];
                    }
                    loftCurves[i] = new ITrimmedCurve[] { CurveSegment.Create(NurbsCurve.CreateFromKnotPoints(false, points)) };
                }
                mesh = newMesh;
            }
            Body.LoftProfiles(loftCurves, false, false).Print();
#else
            ICollection<DrawingView> views = Window.ActiveWindow.ActiveContext.GetSelection<DrawingView>();

            Part part = Window.ActiveWindow.Scene as Part;
            foreach (DesignBody desBody in part.GetDescendants<DesignBody>()) {
                foreach (Face face in desBody.Shape.Faces) {
                    NurbsSurface nurbsSurface = null;
                    if (face.Geometry is NurbsSurface)
                        nurbsSurface = (NurbsSurface)face.Geometry;
                    if (face.Geometry is ProceduralSurface)
                        nurbsSurface = ((ProceduralSurface)face.Geometry).AsSpline(BoxUV.Create(
                            Interval.Create(face.Geometry.Parameterization.U.Bounds.Start.Value, face.Geometry.Parameterization.U.Bounds.End.Value),
                            Interval.Create(face.Geometry.Parameterization.V.Bounds.Start.Value, face.Geometry.Parameterization.V.Bounds.End.Value)
                        ));
                    if (nurbsSurface == null)
                        continue;
                    foreach (double paramU in nurbsSurface.DataU.Knots.Select(k => k.Parameter)) {
                        foreach (double paramV in nurbsSurface.DataV.Knots.Select(k => k.Parameter)) {
                            Point point = nurbsSurface.Evaluate(PointUV.Create(paramU, paramV)).Point;
                            DesignCurve.Create(part, CurveSegment.Create(PointCurve.Create(point)));
                        }
                    }
                }
            }

            // remove small faces
            Part mainPart = Window.ActiveWindow.Scene as Part;
            Body body = mainPart.GetChildren<DesignBody>().First().Shape;
            var avoidFaces = new List<Face>();
            while (true) {
                Face seedFace = null;
                foreach (Face face in body.Faces) {
                    if (face.Geometry is Plane & !avoidFaces.Contains(face)) {
                        seedFace = face;
                        break;
                    }
                }
                if (seedFace == null)
                    break;
                var faces = new List<Face>();
                faces.Add(seedFace);
                while (true) {
                    bool found = false;
                    foreach (Face face in seedFace.AdjacentFaces) {
                        if (!(face.Geometry is Plane) || faces.Contains(face))
                            continue;
                        seedFace = face;
                        faces.Add(face);
                        found = true;
                    }
                    if (!found)
                        break;
                }
                if (faces.Count < 3) {
                    avoidFaces.AddRange(faces);
                    continue;
                }
                try {
                    body.DeleteFaces(faces, RepairAction.GrowSurrounding);
                }
                catch {
                    DesignCurve.Create(mainPart, faces.First().GetBoundingBox(Matrix.Identity).Center.AsITrimmedCurve());
                    avoidFaces.AddRange(faces);
                }
            }

            //    Dictionary<string, string> tabIds = new Dictionary<string, string>() {
            //    {"Design", "DesignRibbonTab"},
            //    {"Detail", "DetailingRibbonTab"},
            //    {"Display", "ViewRibbonTab"},
            //    {"Measure", "AnalysisRibbonTab"},
            //    {"Repair", "CaeRepairRibbonTab"},
            //    {"Prepare", "CaePrepareRibbonTab"},
            //    {"Sheet Metal", "SheetMetalRibbonTab"},
            //    {"KeyShot", "HyperShotRibbonTab"},
            //    {"Edit", "EditSplineRibbonTab"},
            //    {"Symbol", "BlockEditRibbonTab"},
            //    {"Journal Tools", "JournalRibbonTab"},
            //    {"Format", "GeometricToleranceRibbonTab"}
            //};

            //Part part = Window.ActiveWindow.Scene as Part;
            //Debug.Assert(part != null, "part != null");
            ////double length = 0.02;
            ////double width = 0.02;
            //double height = 1;
            ////	double desiredArea = 0.02;
            //ICollection<IDesignFace> designFaces = Window.ActiveWindow.ActiveContext.GetSelection<IDesignFace>();
            //List<Body> cutterBodies = new List<Body>();
            //foreach (IDesignBody desBody in part.GetDescendants<IDesignBody>())
            //    cutterBodies.Add(desBody.Master.Shape.Copy());
            //foreach (IDesignFace designFace in designFaces) {
            //    Face face = designFace.Master.Shape;
            //    List<ITrimmedCurve> profile = new List<ITrimmedCurve>();
            //    foreach (Loop loop in face.Loops) {
            //        foreach (Fin fin in loop.Fins) {
            //            profile.Add(fin.Edge);
            //        }
            //    }
            //    Body body = Body.ExtrudeProfile(Plane.PlaneXY, profile, height);
            //    body.Subtract(cutterBodies);
            //    List<Face> deleteFaces = new List<Face>();
            //    foreach (Face deleteFace in body.Faces) {
            //        foreach (Edge edge in deleteFace.Edges) {
            //            if (edge.StartPoint.Z == height || edge.EndPoint.Z == height) {
            //                deleteFaces.Add(deleteFace);
            //                break;
            //            }
            //        }
            //    }
            //    body.DeleteFaces(deleteFaces, RepairAction.None);
            //    DesignBody desBodyMaster = DesignBody.Create(part, "Metal", body);
            //    desBodyMaster.SetColor(null, System.Drawing.Color.DarkTurquoise);
            //}
            /////////////////////////
            //            DatumPlane datum = DatumPlane.Create(Window.ActiveWindow.Scene as Part, "CurvePlane", Plane.PlaneXY);
            //            CurveSegment curve1 = CurveSegment.Create(Line.Create(Point.Origin, Direction.DirX), Interval.Create(0.01, 0.02));
            //            CurveSegment curve2 = CurveSegment.Create(Line.Create(Point.Origin, Direction.DirY), Interval.Create(0.01, 0.02));
            //            DesignCurve.Create(datum, curve1);
            //            DesignCurve.Create(datum, curve2);
            //            Knot[] knotArray = new Knot[] {
            //                new Knot(-1, 3),
            //                new Knot(1, 3)
            //            };
            //            CurveEvaluation curveEval = curve1.Geometry.Evaluate(curve1.Bounds.Start);
            //            Direction t1 = curveEval.Tangent;
            //            curveEval = curve2.Geometry.Evaluate(curve1.Bounds.Start);
            //            Direction t2 = curveEval.Tangent;
            //            Vector c1 = Vector.Zero;
            //            Vector c2 = Vector.Zero;
            //            Point p0 = curve1.StartPoint;
            //            Point p3 = curve2.StartPoint;
            //            Point p1 = p0 - t1 * (6 * p3.Vector + 12 * p0.Vector - 2 * c1 - c2).Magnitude / 18;
            //            Point p2 = p3 - t2 * (6 * p0.Vector + 12 * p3.Vector - 2 * c2 - c1).Magnitude / 18;
            //            ControlPoint[] controlPointArray = new ControlPoint[] { 
            //                new ControlPoint(p0, 1),
            //                new ControlPoint(p1, 1),
            //                new ControlPoint(p2, 1),
            //                new ControlPoint(p3, 1)
            //            };
            //            NurbsData data = new NurbsData(4, true, true, knotArray);
            //            Curve curve = NurbsCurve.CreateFromControlPoints(data, controlPointArray);
            //            CurveSegment curveSegment = CurveSegment.Create(curve, Interval.Create(-1, 1));
            //            DesignCurve.Create(datum, curveSegment);
            //#if false
            //            List<Knot> someKnots = new List<Knot>();
            //            List<ControlPoint> someControlPoints = new List<ControlPoint>();
            //            Debug.Assert(TryBuildC2NurbsData(curves[1], false, 0.005, out someKnots, out someControlPoints));
            //            knots.AddRange(someKnots);
            //            controlPoints.AddRange(someControlPoints);
            //            List<Knot> someKnotsCorrectParams = new List<Knot>();
            //            Debug.Assert(TryBuildC2NurbsData(curve2, false, 0.005, out someKnots, out someControlPoints));
            //            someKnots.Reverse();
            //            double param = knots[knots.Count - 1].Parameter;
            //            foreach (Knot knot in someKnots)
            //                someKnotsCorrectParams.Add(new Knot(++param, knot.Multiplicity));
            //            someControlPoints.Reverse();
            //            knots.AddRange(someKnotsCorrectParams);
            //            controlPoints.AddRange(someControlPoints);
            //            Knot[] knotArray = new Knot[knots.Count];
            //            knots.CopyTo(knotArray);
            //            NurbsData data = new NurbsData(4, true, true, knotArray);
            //            ControlPoint[] controlPointArray = new ControlPoint[controlPoints.Count];
            //            controlPoints.CopyTo(controlPointArray);
            //            Curve curve = NurbsCurve.Create(data, controlPointArray);
            //            CurveSegment curveSegment = CurveSegment.Create(curve, Interval.Create(0, 9));
            //            DesignCurve.Create(datum, curveSegment);
            //            curveSegment = CurveSegment.Create(curve, Interval.Create(4, 5));
            //            DesignCurve.Create(datum, curveSegment);
            //#endif
            //#if false
            //            Circle circle = Circle.Create(Frame.Create(Point.Origin, Direction.DirX, Direction.DirY), 1);
            //            Interval interval = Interval.Create(0, 2 * Math.PI);
            //            CurveSegment curveSegment = CurveSegment.Create(circle, interval);
            //            DesignCurve.Create(datum, curveSegment);
            //            //Vector knotVector = null;
            //            for (double fudge = 3.7; fudge < 4; fudge += 0.05) {
            //                CurveEvaluation curveEvaluation = null;
            //                List<Knot> knots = new List<Knot>();
            //                List<ControlPoint> controlPoints = new List<ControlPoint>();
            //                for (double t = 0; t <= 2 * Math.PI; t += 2 * Math.PI / 8) {
            //                    curveEvaluation = circle.Evaluate(t);
            //                    knots.Add(new Knot(t, 3));
            //                    if (t > 0)
            //                        controlPoints.Add(new ControlPoint(curveEvaluation.Point - curveEvaluation.Tangent * circle.Radius / fudge, 1));
            //                    controlPoints.Add(new ControlPoint(curveEvaluation.Point, 1));
            //                    if (t < 2 * Math.PI)
            //                        controlPoints.Add(new ControlPoint(curveEvaluation.Point + curveEvaluation.Tangent * circle.Radius / fudge, 1));
            //                }
            //                Knot[] knotArray = new Knot[knots.Count];
            //                knots.CopyTo(knotArray);
            //                NurbsData data = new NurbsData(4, true, true, knotArray);
            //                ControlPoint[] controlPointArray = new ControlPoint[controlPoints.Count];
            //                controlPoints.CopyTo(controlPointArray);
            //                Curve curve = NurbsCurve.Create(data, controlPointArray);
            //                curveSegment = CurveSegment.Create(curve, interval);
            //                DesignCurve.Create(datum, curveSegment);
            //            }
            //#endif

            //#if false
            //        static void Curves_Executing(object sender, EventArgs e) {
            //            DatumPlane datum = DatumPlane.Create(Window.ActiveWindow.Scene as Part, "CurvePlane", Plane.PlaneXY);
            //            //int multiplicity = 1;
            //            Knot[] knots = new Knot[] {
            //                new Knot(0, 4),
            //                new Knot(0.5, 3),
            //                new Knot(1, 4)
            //            };
            //            ControlPoint[] controlPoints = new ControlPoint[] {
            //                new ControlPoint(Point.Create(0, 0, 0), 1),
            //                new ControlPoint(Point.Create(0, 1, 0), 1),
            //                new ControlPoint(Point.Create(0, 2, 0), 1),
            //                new ControlPoint(Point.Create(2, 2, 0), 1),
            //                new ControlPoint(Point.Create(0, 2, 0), 1),
            //                new ControlPoint(Point.Create(4, 1, 0), 1),
            //                new ControlPoint(Point.Create(4, 0, 0), 1)
            //            };
            //            NurbsData data = new NurbsData(4, false, false, knots);
            //            Curve curve = NurbsCurve.Create(data, controlPoints);
            //            Interval interval = Interval.Create(0, 1);
            //            CurveSegment curveSegment = CurveSegment.Create(curve, interval);
            //            DesignCurve.Create(datum, curveSegment);
            //        }
            //#endif
        }
#endif
        }
#if true
        static void Vector_Executing(object sender, EventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "CSV Files (*.csv)|*.csv";
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK) {
                Part part = Window.ActiveWindow.Scene as Part;
                if (part == null)
                    return;
                ImportSpheres(dialog.FileName, part);
                Window.ActiveWindow.ZoomExtents();
            }
            return;
        }
        static void ImportSpheres(string fileName, Part part) {
            StreamReader stream = File.OpenText(fileName);
            Window window = Window.ActiveWindow;
            string error = "Error reading file";
            string streamLine = stream.ReadLine();
            double diameter;
            if (!window.Units.Length.TryParse(streamLine, out diameter)) {
                Application.ReportStatus(error, StatusMessageType.Error, null);
                return;
            }
            double x, y, z;
            Regex regex = new Regex(@"(-?[\d\.]+)\s*,\s*(-?[\d\.]+)\s*,\s*(-?[\d\.]+)");
            while (!string.IsNullOrEmpty(streamLine = stream.ReadLine())) {
                Match match = regex.Match(streamLine);
                if (match.Success &&
                    window.Units.Length.TryParse(match.Groups[1].Value, out x) &&
                    window.Units.Length.TryParse(match.Groups[2].Value, out y) &&
                    window.Units.Length.TryParse(match.Groups[3].Value, out z)
                )
                    ShapeHelper.CreateSphere(Point.Create(x, y, z), diameter, part);
                else {
                    Application.ReportStatus(error, StatusMessageType.Error, null);
                    return;
                }
            }

#else
		static void Vector_Executing(object sender, EventArgs e) {
			Part rootPart = Window.ActiveWindow.Scene as Part;
			Debug.Assert(rootPart != null);
			foreach (IComponent iComponent in rootPart.Components) 
				SetVisibility(iComponent, false);
			foreach (IComponent iComponent in rootPart.Components) {
				SetVisibility(iComponent, true);
				string fileNameBase = rootPart.Document.Path;
				Window.ActiveWindow.Export(WindowExportFormat.AutoCadDwg, String.Format("{0}-{1}.dwg", fileNameBase, iComponent.Content.Master.Name));
			//	Window.ActiveWindow.Export(WindowExportFormat.Png, String.Format("{0}-{1}.png", fileNameBase, iComponent.Content.Master.Name));
				SetVisibility(iComponent, false);
			}
		}
		static void SetVisibility(IComponent iComponent, bool isVisible) {
			foreach (IHasVisibility iHasVisibility in iComponent.Content.GetChildren<IHasVisibility>())
				iHasVisibility.SetVisibility(null, isVisible);
		}
#endif
        }
    }
    public static class ReaderExtensions {
        public static string ReadLine(this BinaryReader reader) {
            List<byte> buffer = new List<byte>();
            byte thisByte;
            while ((thisByte = reader.ReadByte()) != '\n')
                buffer.Add(thisByte);
            return Encoding.ASCII.GetString(buffer.ToArray());
        }
        public static int ReadBigEndianInt(this BinaryReader reader) {
            int v = reader.ReadInt32();
            return (v & 0xFF) << 24 | (v & 0xFF00) << 8 | (v >> 8) & 0xFF00 | (v >> 24) & 0xFF;
        }
        public static float ReadBigEndianFloat(this BinaryReader reader) {
            int v = reader.ReadBigEndianInt();
            return BitConverter.ToSingle(BitConverter.GetBytes(v), 0);
        }
    }
}
