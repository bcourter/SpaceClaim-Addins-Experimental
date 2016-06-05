using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.AETools {
    class SSLFileSaveHandler : FileSaveHandler {
        public SSLFileSaveHandler()
            : base("Stratasys SSL Files", "ssl") {
        }

        public override void SaveFile(string path) {
            WriteBlock.ExecuteTask("Copy and slice", () => SaveFileWork(path));
        }

        private void SaveFileWork(string path) {
            double inch = 0.0254;
            double sliceHeight = 0.01 * inch;
            double roadWidth = 0.02 * inch;

            Window activeWindow = Window.ActiveWindow;
            ICollection<IDesignBody> iDesBodies = activeWindow.Document.MainPart.GetDescendants<IDesignBody>();
            if (iDesBodies.Count == 0)
                return;

            List<Body> bodies = iDesBodies.Select(b => b.Master.Shape.CreateTransformedCopy(b.TransformToMaster.Inverse)).ToList();
            Box box = activeWindow.Document.MainPart.GetBoundingBox(Matrix.Identity);

            double min = box.MinCorner.Z;
            double max = box.MaxCorner.Z;

            var positions = new List<IList<Point>>();
            var count = box.Size.Z / sliceHeight;

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(path)) {
                file.WriteLine("Slice V0");
                file.WriteLine("FILE(" + Path.GetFileName(path) + ")");
                file.WriteLine("UNITS(INCH)");
                file.WriteLine("SLICES()");
                var rand = new Random();



                for (int i = 0; i < count; i++) {
                    var points = new List<Point>();
                    var z = min + i * sliceHeight;
                    var plane = Plane.Create(Frame.Create(Point.Create(0, 0, z), Direction.DirZ));
                    file.WriteLine(String.Format("Z {0:F6} {1:F6}", (z + sliceHeight) / inch, sliceHeight / inch));

                    foreach (Body preserveBody in bodies) {
                        var body = preserveBody.Copy();
                        body.Split(plane, null);
                        var halfBodies = body.SeparatePieces().Where(b => Box.Create(b.Vertices.Select(v => v.Position).ToArray()).Center.Z > z);
                        //var halfBodies = body.SeparatePieces().Where(b => b.GetBoundingBox(Matrix.Identity).Center.Z > z);
                        var sectionCurves = halfBodies.SelectMany(b => b.Edges).Where(e => IsPointOnPlane(plane, e.StartPoint) && IsPointOnPlane(plane, e.EndPoint) && IsPointOnPlane(plane, e.Geometry.Evaluate(e.Bounds.Middle()).Point)).ToList<ITrimmedCurve>();

                        if (sectionCurves.Count == 0)
                            continue;

                        var loops = TrimmedCurveChain.GatherLoops(sectionCurves).ToList();
                        var offsetLoops = new List<TrimmedCurveChain>();
                        if (!body.IsClosed) {
                            foreach (TrimmedCurveChain loop in loops) {
                                var curves = loop.SortedCurves.ToList();
                                var first = curves[0];
                                curves.RemoveAt(0);

                                for (int j = 1; j < 3; j++) {
                                    var offsetCurves = first.OffsetChain(plane, -(roadWidth * j + roadWidth / 2), curves, OffsetCornerType.Round);
                                    offsetLoops.Add(new TrimmedCurveChain(offsetCurves));
                                }
                            }
                        }

                        loops.AddRange(offsetLoops);

                        foreach (TrimmedCurveChain loop in loops) {
                            Point? startPoint = null;
                            Point? lastPoint = null;
                            bool isClosed = false;
                            foreach (OrientedTrimmedCurve orientedCurve in loop.Curves) {
                                List<Point> tessellation = orientedCurve.OriginalTrimmedCurve.GetPolyline().ToList();
                                //if (orientedCurve.IsReversed)
                                //    tessellation.Reverse();

                                if (startPoint == null) {
                                    //        var rnd = rand.Next(1, tessellation.Count);
                                    //        startPoint = tessellation[rnd];
                                    startPoint = tessellation[0];
                                    file.WriteLine(String.Format("{0:F6} {1:F6}", startPoint.Value.X / inch, startPoint.Value.Y / inch));
                                    tessellation = tessellation.Take(1).ToList();
                                }

                                foreach (Point point in tessellation) {
                                    if (lastPoint != null && ArePointsClose(lastPoint.Value, point))
                                        continue;

                                    //if (lastPoint != null)
                                    //    CurveSegment.Create(lastPoint.Value, point).Print();

                                    if (!ArePointsClose(startPoint.Value, point) || !body.IsClosed)
                                        file.WriteLine(String.Format("{0:F6} {1:F6}", point.X / inch, point.Y / inch));

                                    lastPoint = point;
                                }
                            }

                            if (startPoint == null)
                                continue;

                            file.WriteLine(body.IsClosed ? "C" : "O");

                            //var length = loop.Length;
                            //var steps = length / increment;
                            //Point point;
                            //for (int k = 0; k < steps; k++) {
                            //    if (loop.TryGetPointAlongCurve(increment, out point))
                            //        points.Add(point);
                            //}
                        }


                        //for (int j = 0; j < 1; j++) {
                        //    foreach (var offsetCurve in iTrimmedCurve.Offset(plane, j * roadWidth))
                        //        points.AddRange(iTrimmedCurve.GetPolyline());
                        //}

                    }

                    if (points.Count < 1)
                        continue;

                    positions.Add(points);
                }




                file.WriteLine("END");


            }
        }




        double coarseAccuracy = Accuracy.LinearResolution * 10;

        private bool ArePointsClose(Point a, Point b) {
            return (a - b).Magnitude < coarseAccuracy;
        }

        private bool IsPointOnPlane(Plane plane, Point point) {
            return (plane.ProjectPoint(point).Point - point).Magnitude < coarseAccuracy / 3;
        }


    }
}
