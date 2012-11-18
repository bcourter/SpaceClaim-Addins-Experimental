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
    public abstract class ToolPath : Instruction {
        bool IsReversed { get; set; }
        public CuttingTool CuttingTool { get; set; }
        public CuttingParameters CuttingParameters { get; set; }
        public Frame Csys = Frame.World;

        protected ToolPath(CuttingTool tool, CuttingParameters parameters) {
            Debug.Assert(tool != null);
            Debug.Assert(parameters != null);

            CuttingTool = tool;
            CuttingParameters = parameters;
            IsReversed = false;
        }

        public abstract IList<CutterLocation> GetCutterLocations();

        public Point RestPoint(Point point) {
            return Point.Create(point.X, point.Y, CuttingParameters.RestZ);
        }

        public IList<CutterLocation> GetCurves(out IList<CurveSegment> cutterCurves, out IList<CurveSegment> rapidCurves, out IList<CurveSegment> arrowCurves) {
            CutterLocation[] cutterLocations = GetCutterLocations().ToArray();
            cutterCurves = new List<CurveSegment>();
            rapidCurves = new List<CurveSegment>();
            arrowCurves = new List<CurveSegment>();

            for (int i = 0; i < cutterLocations.Length - 1; i++) {
                CurveSegment curve = CurveSegment.Create(cutterLocations[i].Point, cutterLocations[i + 1].Point);
                //Debug.Assert(curve != null);
                if (curve == null)
                    continue;

                if (cutterLocations[i].IsRapid)
                    rapidCurves.Add(curve);
                else
                    cutterCurves.Add(curve);

                if (cutterLocations[i].IsRapid ^ !cutterLocations[i + 1].IsRapid)
                    arrowCurves.Add(curve);
            }

            return cutterLocations;
        }

    }

    public abstract class FaceToolPath : ToolPath {
        protected Face face;

        protected BoxUV boxUV;
        protected Surface surface;
        protected double startU;
        protected double endU;
        protected double startV;
        protected double endV;

        protected bool normalFlip;

        protected FaceToolPath(Face face, CuttingTool tool, CuttingParameters parameters)
            : base(tool, parameters) {
            this.face = face;

            boxUV = face.BoxUV;
            surface = face.Geometry;
            startU = boxUV.RangeU.Start;
            endU = boxUV.RangeU.End;
            startV = boxUV.RangeV.Start;
            endV = boxUV.RangeV.End;

            SurfaceEvaluation surfEval = face.Geometry.Evaluate(PointUV.Create((startU + endU) / 2, (startV + endV) / 2));
            Double sign = Vector.Dot(surfEval.Normal.UnitVector, Csys.DirZ.UnitVector);
            normalFlip = sign < 0;
        }

        public Face Face { get { return face; } }
    }

    public class UVFacingToolPath : FaceToolPath {
        double maxLength = 0;
        const int testSteps = 32;

        public UVFacingToolPath(Face face, CuttingTool tool, CuttingParameters parameters)
            : base(face, tool, parameters) {

            for (int i = 0; i < testSteps; i++) {
                double u = (double)i / testSteps * boxUV.RangeU.Span;
                maxLength = Math.Max(maxLength, surface.GetLength(
                    PointUV.Create(u, startV),
                    PointUV.Create(u, endV)
                ));
            }

            SurfaceEvaluation surfEval = surface.Evaluate(PointUV.Create((startU + endU) / 2, (startV + endV) / 2));
            Double sign = Vector.Dot(surfEval.Normal.UnitVector, Csys.DirZ.UnitVector);

            normalFlip = sign < 0;
        }

        public ToolEvaluation Evaluate(PointUV pointUV) {
            SurfaceEvaluation eval = surface.Evaluate(pointUV);
            Point surfacePoint = eval.Point;
            Direction surfaceNormal = normalFlip ? -eval.Normal : eval.Normal;
            return new ToolEvaluation(surfacePoint + CuttingTool.Radius * surfaceNormal, surfacePoint, surfaceNormal);
        }

        public Point[] GetChainAtV(double v, Func<PointUV, bool> condition) {
            int uCount = 33;

            List<Point> points = new List<Point>(uCount);

            for (int i = 0; i < uCount; i++) {
                double u = startU + (double)i / (uCount - 1) * (endU - startU);
                PointUV pointUV = PointUV.Create(u, v);
                if (!condition(pointUV))
                    continue;

                ToolEvaluation eval = Evaluate(pointUV);
                points.Add(eval.CenterPoint);
            }

            return points.ToArray();
        }

        public Point[] GetChainAtVPPP(double v, Func<PointUV, bool> condition) {
            double increment = 0.0001;

            List<Point> points = new List<Point>();

            double u = startU;
            PointUV result;
            while (u < endU) {
                PointUV pointUV = PointUV.Create(u, v);
                if (!condition(pointUV)) {
                    if (surface.TryOffsetParam(pointUV, DirectionUV.DirU, increment, out result))
                        u = result.U;
                    else {
                        u += increment;
                        //                       Debug.Fail("Stuck U parameter.");
                    }
                    continue;
                }

                ToolEvaluation eval = Evaluate(pointUV);
                points.Add(eval.CenterPoint);

                if (!surface.TryOffsetParam(pointUV, DirectionUV.DirU, increment, out result))
                    break;

                u = result.U;
            }

            return points.ToArray();
        }

        public Point[] GetChainAtV(double v) {
            //       return GetChainAtV(v, p => face.ContainsParam(p));
            return GetChainAtV(v, face.Body);
        }

        public Point[] GetChainAtV(double v, Body excludeBody) {
#if false
            return GetChainAtV(v, p => true);
#else
            return GetChainAtV(v, p => {
                if (!face.ContainsParam(p))
                    return false;

                ToolEvaluation eval = Evaluate(p);
                Face[] adjacentFaces = face.Edges
                    .Where(e => e.Faces.Count == 2 && e.GetAngle() < 0)  // TBD handle surface bodies correctly
                    .Select(e => face.GetAdjacentFace(e))
                    .Where(f => f != null)
                    .ToArray();

                foreach (Face adjacentFace in adjacentFaces) {
                    if ((eval.CenterPoint - adjacentFace.Geometry.ProjectPoint(eval.CenterPoint).Point).MagnitudeSquared() < Math.Pow(CuttingTool.Radius, 2))
                        return false;
                }

                return true;
            });
#endif
        }



        public IList<IList<Point>> GetChains() {
            var positions = new List<IList<Point>>();

            var points = new List<Point>();
            for (int i = 0; i < Count; i++) {
                double v = Count < 2 ? (startV + endV) / 2 : startV + (double)i / (Count - 1) * (endV - startV);
                points = GetChainAtV(v).ToList();
                if (points.Count < 1)
                    continue;

                positions.Add(points);
            }

            return positions;
        }

        public override IList<CutterLocation> GetCutterLocations() {
            var CutterLocations = new List<CutterLocation>();
            Vector tip = -Csys.DirZ * CuttingTool.Radius;
            foreach (IList<Point> points in GetChains()) {
                CutterLocations.Add(new CutterLocation(RestPoint(points[0]), tip, true));
                CutterLocations.AddRange(points.Select(p => new CutterLocation(p, tip, false)));
                CutterLocations.Add(new CutterLocation(RestPoint(points[points.Count - 1]), tip, true));
            }

            return CutterLocations;
        }

        public int Count {
            get {
                return (int)Math.Ceiling(maxLength / CuttingParameters.StepOver);
            }
        }

    }


    public class SpiralFacingToolPath : FaceToolPath {
        Plane plane;
        ITrimmedCurve[] curves;
        double initialOffset;

        public SpiralFacingToolPath(Face face, CuttingTool tool, CuttingParameters parameters)
            : base(face, tool, parameters) {
            plane = face.Geometry as Plane;
            if (plane == null)
                throw new NotImplementedException();

            Debug.Assert(face.Loops.Where(l => l.IsOuter).Count() == 1);
            curves = face.Loops.Where(l => l.IsOuter).First().Edges.ToArray();
            initialOffset = tool.Radius;
        }

        public SpiralFacingToolPath(Face face, ICollection<Face> sideFaces, CuttingTool tool, CuttingParameters parameters)
            : base(face, tool, parameters) {
            Debug.Assert(sideFaces.Where(f => f.Body == face.Body).Count() == sideFaces.Count, "All faces must belong to same body.");

            bool isReversed = Vector.Dot(face.Geometry.Evaluate(PointUV.Origin).Normal.UnitVector, Csys.DirZ.UnitVector) < 0 ^ face.IsReversed;

            Body bodyCopy = face.Body.CopyFaces(sideFaces.Concat(new[] { face }).ToArray());
            bodyCopy.OffsetFaces(null, CuttingTool.Radius * (isReversed ? -1 : 1));
            Face offsetFace = bodyCopy.Faces.Where(f => f.Edges.Where(e => e.Faces.Count == 1).Count() == 0).First();

            plane = face.Geometry as Plane;
            if (plane == null)
                throw new NotImplementedException();

            Debug.Assert(face.Loops.Where(l => l.IsOuter).Count() == 1);

            curves = offsetFace.Loops.Where(l => l.IsOuter).First().Edges.Select(e => e.ProjectToPlane(plane)).ToArray();
            initialOffset = 0;
        }

        public override IList<CutterLocation> GetCutterLocations() {
            SpiralStrategy strategy = new SpiralStrategy(plane, curves, CuttingTool, CuttingParameters, initialOffset, Csys);
            return strategy.GetSpiralCuttingLocations();
        }

    }

    public class SpiralStrategy {
        Plane plane;
        ICollection<ITrimmedCurve> curves;
        CuttingTool tool;
        CuttingParameters parameters;
        double initialOffset;

        Vector centerOffset;
        Vector tip;
        Vector closeClearanceVector;

        public SpiralStrategy(Plane plane, ICollection<ITrimmedCurve> curves, CuttingTool tool, CuttingParameters parameters, double initialOffset, Frame Csys) {
            this.plane = plane;
            this.curves = curves;
            this.tool = tool;
            this.parameters = parameters;
            this.initialOffset = initialOffset;

            SurfaceEvaluation eval = plane.Evaluate(PointUV.Origin);
            centerOffset = eval.Normal * tool.Radius;
            tip = -Csys.DirZ * tool.Radius;
            closeClearanceVector = Csys.DirZ * tool.Radius;
        }

        public IList<CutterLocation> GetSpiralCuttingLocations() {
            var root = BuildChainTreeNodes();
            var locations = new List<CutterLocation>();
            RecurseDescendChainTreeNodes(locations, root);
            locations.Reverse();
            return locations;
        }

        private ChainTreeNode BuildChainTreeNodes() {
            var positions = new List<IList<Point>>();
            Vector toCenter = Direction.DirZ * tool.Radius;

            ChainTreeNode root = new ChainTreeNode(null, curves.ExtractChains().First().ToArray());  // TBD get inner loops working
            RecurseBuildChainTreeNodes(root, initialOffset, 0);
            return root;
        }

        private void RecurseBuildChainTreeNodes(ChainTreeNode parent, double offset, int depth) {
            Debug.Assert(depth < 33, "Exceeded max depth");
            if (depth >= 33)
                return;

            ChainTreeNode[] children = parent.OrderedCurves
                .OffsetChainInward(plane, -offset, OffsetCornerType.Round)
                .ExtractChains()
                .Select(c => new ChainTreeNode(parent, c))
                .ToArray();

            foreach (ChainTreeNode node in children)
                RecurseBuildChainTreeNodes(node, parameters.StepOver, ++depth);
        }

        bool isOnSpiral = false;
        private void RecurseDescendChainTreeNodes(List<CutterLocation> locations, ChainTreeNode parent) {
            Point initialPoint = locations.Count == 0 ? Point.Origin : locations[locations.Count - 1].Center;
            ChainTreeNode[] nodes = parent.Children.OrderBy(n => (n.OrderedCurves.First().StartPoint - initialPoint).Magnitude).ToArray();

            foreach (ChainTreeNode node in nodes) {
                IList<Point> points = GetPoints(node.OrderedCurves).RemoveAdjacentDuplicates();
                Point startPoint = points[0];
                Point endPoint = points[points.Count - 1];

                if (!isOnSpiral)
                    locations.Add(new CutterLocation(Point.Create(startPoint.X, startPoint.Y, parameters.RestZ), tip, true));

                isOnSpiral = true;

                locations.Add(new CutterLocation(startPoint + closeClearanceVector, tip, true));
                locations.AddRange(points.Select(p => new CutterLocation(p, tip, false)));
                locations.Add(new CutterLocation(endPoint + closeClearanceVector, tip, true));

                RecurseDescendChainTreeNodes(locations, node);
                isOnSpiral = false;

                locations.Add(new CutterLocation(Point.Create(locations[locations.Count - 1].Center.X, locations[locations.Count - 1].Center.Y, parameters.RestZ), tip, true));
            }
        }

        public IList<Point> GetPoints(ICollection<ITrimmedCurve> curves) {
            var profile = new List<Point>();
            foreach (ITrimmedCurve curve in new TrimmedCurveChain(curves).SortedCurves)
                profile.AddRange(curve.TessellateCurve(parameters.increment).Select(p => p + centerOffset));

            return profile;
        }

        private class ChainTreeNode {
            public ChainTreeNode Parent { get; private set; }
            public IList<ChainTreeNode> Children { get; private set; }
            public IList<ITrimmedCurve> OrderedCurves { get; private set; }

            public ChainTreeNode(ChainTreeNode parent, IList<ITrimmedCurve> orderedCurves) {
                Parent = parent;
                Children = new List<ChainTreeNode>();
                if (parent != null) // root
                    Parent.Children.Add(this);

                OrderedCurves = orderedCurves;
            }
        }
    }
}