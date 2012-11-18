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
            Double sign = Vector.Dot(surfEval.Normal.UnitVector, Direction.DirZ.UnitVector);
            normalFlip = sign < 0;
        }

        public Face Face { get { return face; } }
    }

    public class UVToolPath : FaceToolPath {
        double maxLength = 0;
        const int testSteps = 32;

        public UVToolPath(Face face, CuttingTool tool, CuttingParameters parameters)
            : base(face, tool, parameters) {

            for (int i = 0; i < testSteps; i++) {
                double u = (double)i / testSteps * boxUV.RangeU.Span;
                maxLength = Math.Max(maxLength, surface.GetLength(
                    PointUV.Create(u, startV),
                    PointUV.Create(u, endV)
                ));
            }

            SurfaceEvaluation surfEval = surface.Evaluate(PointUV.Create((startU + endU) / 2, (startV + endV) / 2));
            Double sign = Vector.Dot(surfEval.Normal.UnitVector, Direction.DirZ.UnitVector);

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

#if false
        public Point[] GetChainAtV(double v) {
            int uCount = 24;
            Point[] points = new Point[uCount];
            for (int i = 0; i < uCount; i++) {
                double u = startU + (double)i / (uCount - 1) * (endU - startU);
                points[i] = EvaluateCenter(PointUV.Create(u, v));
            }

            return points;
        }
#endif

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
            Vector tip = -Direction.DirZ * CuttingTool.Radius;
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


    public class BottomToolPath : FaceToolPath {
        const int testSteps = 32;
        const double increment = 0.001;
        Vector centerOffset;
        Vector tip;
        Vector closeClearanceVector;

        public BottomToolPath(Face face, CuttingTool tool, CuttingParameters parameters)
            : base(face, tool, parameters) {
            Plane plane = face.Geometry as Plane;
            if (plane == null)
                throw new NotImplementedException();

            SurfaceEvaluation eval = face.Geometry.Evaluate(PointUV.Origin);
            centerOffset = eval.Normal * CuttingTool.Radius;
            tip = -Direction.DirZ * CuttingTool.Radius;
            closeClearanceVector = Direction.DirZ * CuttingTool.Radius;

            Debug.Assert(face.Loops.Where(l => l.IsOuter).Count() == 1);
        }

        public IList<Point> GetPoints(ICollection<ITrimmedCurve> curves) {
            var profile = new List<Point>();
            foreach (ITrimmedCurve curve in new TrimmedCurveChain(curves).SortedCurves)
                profile.AddRange(curve.TessellateCurve(increment).Select(p => p + centerOffset));

            return profile;
        }

        public ICollection<IList<Point>> GetOffsetPoints(double offset) {
            var profiles = new List<IList<Point>>();
            ICollection<ITrimmedCurve> offsetProfile = face.OffsetAllLoops(offset, OffsetCornerType.Round);

            if (offsetProfile == null)
                return null;

            foreach (IList<ITrimmedCurve> curves in offsetProfile.ExtractChains()) {
                profiles.Add(GetPoints(curves));
            }

            return profiles;
        }
#if true
        private ChainTreeNode BuildChainTreeNodes() {
            var positions = new List<IList<Point>>();
            Vector toCenter = Direction.DirZ * CuttingTool.Radius;

            ChainTreeNode root = face.Loops.Where(l => l.IsOuter).Select(l => l.Edges).Select(c => new ChainTreeNode(null, c.Cast<ITrimmedCurve>().ToArray())).First();  // TBD get inner loops working
            RecurseBuildChainTreeNodes(root, CuttingTool.Radius, 0);
            return root;
        }

        private void RecurseBuildChainTreeNodes(ChainTreeNode parent, double offset, int depth) {
            Debug.Assert(depth < 33, "Exceeded max depth");
            if (depth >= 33)
                return;

            ChainTreeNode[] children = parent.OrderedCurves
                .OffsetChainInward(face, -offset, OffsetCornerType.Round)
                .ExtractChains()
                .Select(c => new ChainTreeNode(parent, c))
                .ToArray();

            foreach (ChainTreeNode node in children)
                RecurseBuildChainTreeNodes(node, CuttingParameters.StepOver, ++depth);
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
                    locations.Add(new CutterLocation(Point.Create(startPoint.X, startPoint.Y, CuttingParameters.RestZ), tip, true));

                isOnSpiral = true;

                locations.Add(new CutterLocation(startPoint + closeClearanceVector, tip, true));
                locations.AddRange(points.Select(p => new CutterLocation(p, tip, false)));
                locations.Add(new CutterLocation(endPoint + closeClearanceVector, tip, true));

                RecurseDescendChainTreeNodes(locations, node);
                isOnSpiral = false;

                locations.Add(new CutterLocation(Point.Create(locations[locations.Count - 1].Center.X, locations[locations.Count - 1].Center.Y, CuttingParameters.RestZ), tip, true));
            }
        }

        public override IList<CutterLocation> GetCutterLocations() {
            var root = BuildChainTreeNodes();
            var locations = new List<CutterLocation>();
            RecurseDescendChainTreeNodes(locations, root);
            return locations;
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

#else
        public IList<IList<Point>> GetChains() {
            var positions = new List<IList<Point>>();
            Vector toCenter = Direction.DirZ * CuttingTool.Radius;

            int maxOffsets = 33;
            for (int i = 0; i < maxOffsets; i++) {
                double offset = (CuttingTool.Radius + CuttingParameters.StepOver * i);
                ICollection<IList<Point>> profiles = GetOffsetPoints(-offset);

                if (profiles == null)
                    break;

                foreach (IList<Point> points in profiles) {
                    if (points == null)
                        break;

                    positions.Add(points
                        .RemoveAdjacentDuplicates()
                        .Select(p => p + toCenter)
                        .ToArray()
                    );
                }

                Debug.Assert(!(i == maxOffsets - 1));
            }

            return positions;
        }

        public override IList<CutterLocation> GetCutterLocations() {
            var CutterLocations = new List<CutterLocation>();
            Vector tip = -Direction.DirZ * CuttingTool.Radius;
            foreach (IList<Point> points in GetChains()) {
                CutterLocations.Add(new CutterLocation(RestPoint(points[0]), tip, true));
                CutterLocations.AddRange(points.Select(p => new CutterLocation(p, tip, false)));
                CutterLocations.Add(new CutterLocation(RestPoint(points[points.Count - 1]), tip, true));
            }

            return CutterLocations;
        }
#endif
    }



}