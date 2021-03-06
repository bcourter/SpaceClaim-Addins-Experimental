﻿/*
 * Sample add-in for the SpaceClaim API
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
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
    [XmlInclude(typeof(BallMill))]
    public abstract class CuttingTool {
        public double Radius { get; set; }
        public double CuttingHeight { get; set; }

        public CuttingTool() { }

        public CuttingTool(double radius, double height) {
            Radius = radius;
            CuttingHeight = height;
        }

        public MeshPrimitive GetPrimitive() {
            int revolveSteps = 24;

            var profileFacetVertices = new List<FacetVertex>();
            Direction perpendicular = Direction.DirY;
            CurveSegment[] curveSegments = GetProfile().ToArray();
            foreach (CurveSegment curveSegement in curveSegments) {
                if (curveSegement.Geometry is Circle) {
                    int steps = (int)(curveSegement.Bounds.Span / Const.Tau * (revolveSteps + 1));
                    for (int i = 0; i < steps; i++) {
                        double t = curveSegement.Bounds.Start + curveSegement.Bounds.Span * i / steps;
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
                        curveSegement.StartPoint,
                        -Direction.Cross(((Line)curveSegement.Geometry).Direction, perpendicular)
                    ));
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
            for (int j = 0; j < count - 1; j++) {
                int jj = j + 1;
                if (facetVertices[j].Position == facetVertices[jj].Position)
                    continue;
                
                for (int i = 0; i < revolveSteps; i++) {
                    int ii = (i + 1) % revolveSteps;

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
        [XmlIgnoreAttribute]
        public abstract Vector CenterToTip { get; }
    }

    public class BallMill : CuttingTool {
        public BallMill() { }

        public BallMill(double radius, double height)
            : base(radius, height) {

            if (height < radius)
                throw new ArgumentException("Height must be larger than radius.");
        }

        public override IEnumerable<CurveSegment> GetProfile() {
            Point center = Point.Origin + Direction.DirZ * Radius;
            Point top = Point.Origin + Direction.DirZ * CuttingHeight;

            var ballArc = CurveSegment.Create(Circle.Create(Frame.Create(center, Direction.DirX, Direction.DirZ), Radius), Interval.Create((double)3 / 4 * Const.Tau, Const.Tau));
            var sideLine = CurveSegment.Create(center + Direction.DirX * Radius, top + Direction.DirX * Radius);
            var topLine = CurveSegment.Create(top + Direction.DirX * Radius, top);

            return new[] { ballArc, sideLine, topLine };
        }

        [XmlIgnoreAttribute]
        public override Vector CenterToTip { get { return -Radius * Direction.DirZ; } }

        // http://www.mcmaster.com/#end-mills/=k7nu4a
        [XmlIgnoreAttribute]
        public static readonly Dictionary<string, BallMill> StandardSizes = new Dictionary<string, BallMill>() {
            {"1/8\" x 3/8\" cut", new BallMill((double)1/8/2 * Const.inches, (double)3/ 8* Const.inches)},
            {"3/16\" x 1/2\" cut", new BallMill((double)3/16/2 * Const.inches, (double)1/2 * Const.inches)},
            {"1/4\" x 5/8\" cut", new BallMill((double)1/4/2 * Const.inches, (double)5/8 * Const.inches)},
            {"5/16\" x 3/4\" cut", new BallMill((double)5/16/2 * Const.inches, (double)3/4 * Const.inches)},
            {"3/8\" x 3/4\" cut", new BallMill((double)3/8/2 * Const.inches, (double)3/4 * Const.inches)},
            {"7/16\" x 1\" cut", new BallMill((double)7/16/2 * Const.inches, (double)1 * Const.inches)},
            {"1/2\" x 1\" cut", new BallMill((double)1/2/2 * Const.inches, (double)1 * Const.inches)},
            {"9/16\" x  1-1/8\" cut", new BallMill((double)9/16/2 * Const.inches, (double)9/8 * Const.inches)},
            {"5/8\" x  1-1/8\" cut", new BallMill((double)5/8/2 * Const.inches, (double)9/8 * Const.inches)},
            {"3/4\" x  1-5/16\" cut", new BallMill((double)3/4/2 * Const.inches, (double)21/16 * Const.inches)}
        };
    }
}