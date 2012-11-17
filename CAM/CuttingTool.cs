/*
 * Sample add-in for the SpaceClaim API
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Linq;
using System.Windows.Forms;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Display;
using CAM.Properties;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using Point = SpaceClaim.Api.V10.Geometry.Point;
using ScreenPoint = System.Drawing.Point;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.CAM {
    public abstract class CuttingTool {
        public double Radius { get; set; }
        public double CutingHeight { get; private set; }

        public CuttingTool(double radius, double height) {
            Radius = radius;
            CutingHeight = height;
        }

        public MeshPrimitive GetPrimitive() {
            int revolveSteps = 24;

            var profileFacetVertices = new List<FacetVertex>();
            Direction perpendicular = Direction.DirY;
            CurveSegment[] curveSegments = GetProfile().ToArray();
            foreach (CurveSegment curveSegement in curveSegments) {
                if (curveSegement.Geometry is Circle) {
                    for (double t = curveSegement.Bounds.Start; t < curveSegement.Bounds.End; t += Const.Tau / revolveSteps) {
                        var eval = curveSegement.Geometry.Evaluate(t);
                        profileFacetVertices.Add(new FacetVertex(
                            eval.Point,
                            -Direction.Cross(eval.Tangent, perpendicular)
                        ));
                    }
                    continue;
                }

                if (curveSegement.Geometry is Line) {
                    profileFacetVertices.Add(new FacetVertex(
                        curveSegement.EndPoint,
                        -Direction.Cross(((Line)curveSegement.Geometry).Direction, perpendicular)
                    ));
                    continue;
                }

                throw new NotImplementedException("Only lines and circles supported in tool profiles");
            }
            profileFacetVertices.Add(new FacetVertex(
                curveSegments.Last().EndPoint,
                Direction.DirZ
            ));

            int count = profileFacetVertices.Count;

            var facetVertices = new FacetVertex[revolveSteps * count];
            for (int i = 0; i < revolveSteps; i++) {
                for (int j = 0; j < count; j++)
                    facetVertices[i * count + j] = profileFacetVertices[j].Transform(Matrix.CreateRotation(Frame.World.AxisZ, (double)i / revolveSteps * Const.Tau));
            }

            var facets = new List<Facet>();
            for (int i = 0; i < revolveSteps; i++) {
                for (int j = 0; j < count - 1; j++) {
                    int ii = (i + 1) % revolveSteps;
                    int jj = j + 1;

                    facets.Add(new Facet(
                        i * count + j,
                        ii * count + j,
                        ii * count + jj
                    ));
                    facets.Add(new Facet(
                        i * count + j,
                        i * count + jj,
                        ii * count + jj
                    ));

                }
            }

            return MeshPrimitive.Create(facetVertices, facets);
        }

        public abstract IEnumerable<CurveSegment> GetProfile();
    }

    public class BallMill : CuttingTool {
        public BallMill(double radius, double height)
            : base(radius, height) {
        }

        public override IEnumerable<CurveSegment> GetProfile() {
            Point center = Point.Origin + Direction.DirZ * Radius;
            Point top = center + Direction.DirZ * CutingHeight;

            var ballArc = CurveSegment.Create(Circle.Create(Frame.Create(center, Direction.DirX, Direction.DirZ), Radius), Interval.Create((double)3 / 4 * Const.Tau, Const.Tau));
            var sideLine = CurveSegment.Create(center + Direction.DirX * Radius, top + Direction.DirX * Radius);
            var topLine = CurveSegment.Create(top + Direction.DirX * Radius, top);

            return new[] { ballArc, sideLine, topLine };
        }

    }
}