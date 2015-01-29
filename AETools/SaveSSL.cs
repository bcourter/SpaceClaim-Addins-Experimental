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
            double increment = 10;

            double sliceHeight = 0.01;
            double roadWidth = 0.02;

            Window activeWindow = Window.ActiveWindow;
            ICollection<IDesignBody> iDesBodies = activeWindow.Document.MainPart.GetDescendants<IDesignBody>();
            if (iDesBodies.Count == 0)
                return;

            Box box = activeWindow.Document.MainPart.GetBoundingBox(Matrix.Identity);

            double min = box.MinCorner.Z;
            double max = box.MaxCorner.Z;

            var positions = new List<IList<Point>>();
            var count = box.Size.Z / sliceHeight;

            for (int i = 0; i < count; i++) {
                var points = new List<Point>();
                var z = i * sliceHeight;
                var plane = Plane.Create(Frame.Create(Point.Create(0, 0, z), Direction.DirZ));

                foreach (IDesignBody iDesBody in iDesBodies) {
                    var sectionCurves = new List<ITrimmedCurve>();
                    foreach (Face face in iDesBody.Master.Shape.Faces) {
                        if (face.Geometry is Plane && (face.Geometry as Plane).Frame.DirZ.IsParallel(plane.Frame.DirZ))
                            continue;

                        sectionCurves.AddRange(face.Geometry.IntersectSurface(plane).Curves);
                    }

                    ICollection<TrimmedCurveChain> loops = TrimmedCurveChain.GatherLoops(sectionCurves);
                    foreach (TrimmedCurveChain loop in loops) {
                        foreach (ITrimmedCurve curve in loop.SortedCurves)
                            points.AddRange(curve.GetPolyline());

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

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(path)) {
                file.WriteLine("Slice V0");
                file.WriteLine("FILE(" + Path.GetFileName(path) + ")");
                file.WriteLine("UNITS(INCH)");
                file.WriteLine("SLICES()");
                foreach (IList<Point> layer in positions) {
                    file.WriteLine(String.Format("Z {0} {1}", layer[0].Z, 0.01));

                    foreach (Point point in layer)
                        file.WriteLine(String.Format("{0} {1}", point.X, point.Y));

                    file.WriteLine("C");

                }

                file.WriteLine("END");

            }

        }


    }
}
