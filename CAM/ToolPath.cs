/*
 * Sample add-in for the SpaceClaim API
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
//using System.Threading;
using System.Linq;
//using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

//using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Display;
//using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using Point = SpaceClaim.Api.V10.Geometry.Point;
using ScreenPoint = System.Drawing.Point;

using SpaceClaim.AddInLibrary;
using CAM.Properties;

namespace SpaceClaim.AddIn.CAM {
    [XmlInclude(typeof(Operation))]
    [XmlInclude(typeof(ToolPath))]
    [XmlInclude(typeof(FaceToolPath))]
    public abstract class Instruction {
        //  protected static XmlSerializer serializer = new XmlSerializer(typeof(Instruction));

        protected Instruction() { }

        public override string ToString() {
            using (var stringWriter = new StringWriter()) {
                var settings = new XmlWriterSettings {
                    OmitXmlDeclaration = true
                };

                using (XmlWriter xmlWriter = XmlWriter.Create(stringWriter, settings)) {
                    XmlSerializer serializer = new XmlSerializer(this.GetType());
                    var namespaces = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty }); // omit namespaces from root node
                    serializer.Serialize(xmlWriter, this, namespaces);
                }
                return stringWriter.ToString();
            }
        }

        public static T FromString<T>(string text) where T : Instruction {
            if (string.IsNullOrEmpty(text))
                return null;
            using (var reader = new StringReader(text)) {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                return (T)serializer.Deserialize(reader);
            }
        }
    }

    public class Operation : Instruction {
        List<Instruction> Instructions { get; set; }  // can't be IList due to serialization

        protected Operation() : base() { }

        public Operation(IList<Instruction> instructions) {
            Instructions = instructions.ToList();
        }
    }

    public abstract class ToolPath : Instruction {
        public CuttingTool CuttingTool { get; set; }
        public CuttingParameters CuttingParameters { get; set; }
        [XmlIgnoreAttribute]
        public Frame Csys { get; set; }
        public CutterLocation[] CutterLocations { get; set; }

        protected ToolPath() : base() { }

        protected ToolPath(CuttingTool tool, CuttingParameters parameters) {
            Debug.Assert(tool != null);
            Debug.Assert(parameters != null);

            CuttingTool = tool;
            CuttingParameters = parameters;
            Csys = Frame.World;
        }

        //public abstract bool TryGetCutterLocations(out IList<CutterLocation> locations);
        protected abstract CutterLocation[] GetCutterLocations();

        public void UpdateCutterLocations() {
            CutterLocations = GetCutterLocations();
        }

        public Point RestPoint(Point point) {
            return Point.Create(point.X, point.Y, CuttingParameters.RestZ + CuttingTool.Radius);
        }

        public void GetCurves(out IList<CurveSegment> cutterCurves, out IList<CurveSegment> rapidCurves, out IList<CurveSegment> arrowCurves) {
            Debug.Assert(CutterLocations != null);

            cutterCurves = new List<CurveSegment>();
            rapidCurves = new List<CurveSegment>();
            arrowCurves = new List<CurveSegment>();

            for (int i = 0; i < CutterLocations.Length - 1; i++) {
                CurveSegment curve = CurveSegment.Create(CutterLocations[i].Point, CutterLocations[i + 1].Point);
                //Debug.Assert(curve != null);
                if (curve == null)
                    continue;

                if (CutterLocations[i + 1].IsRapid)
                    rapidCurves.Add(curve);
                else
                    cutterCurves.Add(curve);

                if (CutterLocations[i].IsRapid && !CutterLocations[i + 1].IsRapid)
                    arrowCurves.Add(curve);
            }
        }

        public double[] SerializableFrame {
            get {
                return new[] { 
                    Csys.Origin.X, Csys.Origin.Y, Csys.Origin.Z,
                    Csys.DirX.X, Csys.DirX.Y, Csys.DirX.Z,
                    Csys.DirY.X, Csys.DirY.Y, Csys.DirY.Z,
                };
            }
            set {
                Csys = Frame.Create(
                    Point.Create(value[0], value[1], value[2]),
                    Direction.Create(value[3], value[4], value[5]),
                    Direction.Create(value[6], value[7], value[8])
                );
            }
        }
    }

    public class FaceToolPath : ToolPath {
        [NonSerializedAttribute]
        private Face face;

        public StrategyType Strategy { get; set; }

        private BoxUV boxUV;
        private Surface surface;

        public double StartU { get; set; }
        public double EndU { get; set; }
        public double StartV { get; set; }
        public double EndV { get; set; }

        private double maxLength = 0;
        private bool isNormalFlipped;

        public FaceToolPath() : base() { }

        public FaceToolPath(Face face, CuttingTool tool, CuttingParameters parameters, StrategyType strategy)
            : base(tool, parameters) {
            Debug.Assert(tool != null);
            Debug.Assert(parameters != null);

            Face = face;
            Strategy = strategy;
        }

        protected override CutterLocation[] GetCutterLocations() {
            switch (Strategy) {
                case StrategyType.UV:
                    return UVFacingToolPathFactory.GetCutterLocations(this);

                case StrategyType.Spiral:
                    return SpiralFacingToolPathFactory.GetCutterLocations(this);

                case StrategyType.Contour:
                    return ContourFacingToolPathFactory.GetCutterLocations(this);

                default:
                    throw new NotImplementedException();
            }
        }

        public bool IsNormalFlipped { get { return isNormalFlipped; } }

        public Surface Surface { get { return surface; } }
        public double MaxLength { get { return maxLength; } }

        [XmlIgnoreAttribute]
        public Face Face {
            get { return face; }
            set {
                face = value;
                if (face == null) {
                    CutterLocations = null;
                    return;
                }

                boxUV = face.BoxUV;
                surface = face.Geometry;
                StartU = boxUV.RangeU.Start;
                EndU = boxUV.RangeU.End;
                StartV = boxUV.RangeV.Start;
                EndV = boxUV.RangeV.End;

                const int testSteps = 32;
                for (int i = 0; i < testSteps; i++) {
                    double u = (double)i / testSteps * boxUV.RangeU.Span;
                    maxLength = Math.Max(maxLength, surface.GetLength(
                        PointUV.Create(u, StartV),
                        PointUV.Create(u, EndV)
                    ));
                }

                SurfaceEvaluation surfEval = surface.Evaluate(PointUV.Create((StartU + EndU) / 2, (StartV + EndV) / 2));
                Double sign = Vector.Dot(surfEval.Normal.UnitVector, Csys.DirZ.UnitVector);

                isNormalFlipped = sign < 0;
            }
        }

        public enum StrategyType {
            UV = 0,
            Spiral = 1,
            Contour = 2
        }
    }

    public static class UVFacingToolPathFactory {
        private static ToolEvaluation Evaluate(PointUV pointUV, FaceToolPath toolPath) {
            SurfaceEvaluation eval = toolPath.Surface.Evaluate(pointUV);
            Point surfacePoint = eval.Point;
            Direction surfaceNormal = toolPath.IsNormalFlipped ? -eval.Normal : eval.Normal;
            return new ToolEvaluation(surfacePoint + toolPath.CuttingTool.Radius * surfaceNormal, surfacePoint, surfaceNormal);
        }

        private static IList<IList<Point>> GetChains(FaceToolPath toolPath) {
            var positions = new List<IList<Point>>();

            var points = new List<Point>();

            var box = toolPath.Face.GetBoundingBox(Matrix.Identity);
            var count = box.Size.Z / toolPath.CuttingParameters.StepOver;

            for (int i = 0; i < count; i++) {
                var z = i * toolPath.CuttingParameters.StepOver;
                var plane = Plane.Create(Frame.Create(Point.Create(0, 0, z), Direction.DirZ));
                var section = toolPath.Face.Geometry.IntersectSurface(plane);

                foreach (var iTrimmedCurve in section.Curves) {

                    for (int j = 0; j < 3; j++) {
                        foreach (var offsetCurve in iTrimmedCurve.Offset(plane, j * toolPath.CuttingParameters.StepOver))
                            points.AddRange(iTrimmedCurve.TessellateCurve(toolPath.CuttingParameters.Increment));
                    }

                }

                if (points.Count < 1)
                    continue;

                positions.Add(points);
            }

            return positions;
        }

        public static CutterLocation[] GetCutterLocations(FaceToolPath toolPath) {
            var CutterLocations = new List<CutterLocation>();
            Vector tip = -toolPath.Csys.DirZ * toolPath.CuttingTool.Radius;
            foreach (IList<Point> points in GetChains(toolPath)) {
                CutterLocations.Add(new CutterLocation(toolPath.RestPoint(points[0]) + tip, true));
                CutterLocations.AddRange(points.Select(p => new CutterLocation(p + tip, false)));
                CutterLocations.Add(new CutterLocation(toolPath.RestPoint(points[points.Count - 1]) + tip, true));
            }

            return CutterLocations.ToArray();
        }

        private static int Count(FaceToolPath toolPath) {
            return (int)Math.Ceiling(toolPath.MaxLength / toolPath.CuttingParameters.StepOver);
        }
    }

    public static class SpiralFacingToolPathFactory {
        public static CutterLocation[] GetCutterLocations(FaceToolPath toolPath) {
            Debug.Assert(toolPath.Face.Loops.Where(l => l.IsOuter).Count() == 1);
            ITrimmedCurve[] curves = toolPath.Face.Loops.Where(l => l.IsOuter).First().Edges.ToArray();

            Plane plane = toolPath.Face.Geometry as Plane;
            if (plane == null)
                throw new NotImplementedException();

            SpiralStrategy strategy = new SpiralStrategy(plane, curves, toolPath.CuttingTool.Radius, toolPath);
            return strategy.GetSpiralCuttingLocations();
        }

#if false
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
#endif
    }


    public class SpiralStrategy {
        Plane plane;
        ICollection<ITrimmedCurve> curves;
        ToolPath toolPath;
        CuttingTool tool;
        CuttingParameters parameters;
        double initialOffset;

        Vector centerOffset;
        Vector tip;
        Vector closeClearanceVector;

        public SpiralStrategy(Plane plane, ICollection<ITrimmedCurve> curves, double initialOffset, ToolPath toolPath) {
            this.plane = plane;
            this.curves = curves;
            this.toolPath = toolPath;
            this.tool = toolPath.CuttingTool;
            this.parameters = toolPath.CuttingParameters;
            this.initialOffset = initialOffset;

            SurfaceEvaluation eval = plane.Evaluate(PointUV.Origin);
            centerOffset = eval.Normal * tool.Radius;
            tip = -toolPath.Csys.DirZ * tool.Radius;
            closeClearanceVector = toolPath.Csys.DirZ * tool.Radius;
        }

        public CutterLocation[] GetSpiralCuttingLocations() {
            var root = BuildChainTreeNodes();
            var locations = new List<CutterLocation>();
            RecurseDescendChainTreeNodes(locations, root);
            locations.Reverse();
            return locations.ToArray();
        }

        // Does the heavy lifting of creating the offsets for each spiral, from the outside in, but to not attempt to order the result
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

        // Order the heirarchy of islands and add clearance heights
        bool isOnSpiral = false;
        private void RecurseDescendChainTreeNodes(List<CutterLocation> locations, ChainTreeNode parent) {
            Point initialPoint = locations.Count == 0 ? Point.Origin : locations[locations.Count - 1].Point;
            ChainTreeNode[] nodes = parent.Children.OrderBy(n => (n.OrderedCurves.First().StartPoint - initialPoint).Magnitude).ToArray();

            foreach (ChainTreeNode node in nodes) {
                IList<Point> points = GetPoints(node.OrderedCurves).RemoveAdjacentDuplicates();
                Point startPoint = points[0];
                Point endPoint = points[points.Count - 1];

                if (!isOnSpiral)
                    locations.Add(new CutterLocation(toolPath.RestPoint(startPoint) + tip, true));

                isOnSpiral = true;

                locations.Add(new CutterLocation(startPoint + closeClearanceVector + tip, true));
                locations.AddRange(points.Select(p => new CutterLocation(p + tip, false)));
                locations.Add(new CutterLocation(endPoint + closeClearanceVector + tip, true));

                RecurseDescendChainTreeNodes(locations, node);
                isOnSpiral = false;

                var endLocation = new CutterLocation(toolPath.RestPoint(locations[locations.Count - 1].Point) + tip, true);
                if (locations[locations.Count - 1].Point != endLocation.Point)
                    locations.Add(endLocation);
            }
        }

        public IList<Point> GetPoints(ICollection<ITrimmedCurve> curves) {
            var profile = new List<Point>();
            foreach (ITrimmedCurve curve in new TrimmedCurveChain(curves).SortedCurves)
                profile.AddRange(curve.TessellateCurve(parameters.Increment).Select(p => p + centerOffset));

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


    public static class ContourFacingToolPathFactory {
        private static ToolEvaluation Evaluate(PointUV pointUV, FaceToolPath toolPath) {
            SurfaceEvaluation eval = toolPath.Surface.Evaluate(pointUV);
            Point surfacePoint = eval.Point;
            Direction surfaceNormal = toolPath.IsNormalFlipped ? -eval.Normal : eval.Normal;
            return new ToolEvaluation(surfacePoint + toolPath.CuttingTool.Radius * surfaceNormal, surfacePoint, surfaceNormal);
        }

        private static Point[] GetChainAtVWhere(double v, Func<PointUV, bool> condition, FaceToolPath toolPath) {
            List<Point> points = new List<Point>();

            double u = toolPath.StartU;
            while (u <= toolPath.EndU) {
                PointUV pointUV;
                if (!toolPath.Surface.TryOffsetParam(PointUV.Create(u, v), DirectionUV.DirU, toolPath.CuttingParameters.Increment, out pointUV))
                    break;

                u = pointUV.U;

                if (!condition(pointUV))
                    continue;

                ToolEvaluation eval = Evaluate(pointUV, toolPath);
                points.Add(eval.CenterPoint);
            }

            return points.ToArray();
        }

        private static Point[] GetChainAtV(double v, FaceToolPath toolPath) {
#if false
            return GetChainAtV(v, p => true);
#else
            return GetChainAtVWhere(v, p => {
                if (!toolPath.Face.ContainsParam(p))
                    return false;

                ToolEvaluation eval = Evaluate(p, toolPath);
                Face[] adjacentFaces = toolPath.Face.Edges
                    .Where(e => e.Faces.Count == 2 && e.GetAngle() < 0)  // TBD handle surface bodies correctly
                    .Select(e => toolPath.Face.GetAdjacentFace(e))
                    .Where(f => f != null)
                    .ToArray();

                foreach (Face adjacentFace in adjacentFaces) {
                    if ((eval.CenterPoint - adjacentFace.Geometry.ProjectPoint(eval.CenterPoint).Point).MagnitudeSquared() < Math.Pow(toolPath.CuttingTool.Radius, 2))
                        return false;
                }

                return true;
            }, toolPath);
#endif
        }

        private static IList<IList<Point>> GetChains(FaceToolPath toolPath) {
            var positions = new List<IList<Point>>();

            var points = new List<Point>();
            int count = Count(toolPath);
            for (int i = 0; i < count; i++) {
                double v = count < 2 ? (toolPath.StartV + toolPath.EndV) / 2 : toolPath.StartV + (double)i / (count - 1) * (toolPath.EndV - toolPath.StartV);
                points = GetChainAtV(v, toolPath).ToList();
                if (points.Count < 1)
                    continue;

                positions.Add(points);
            }

            return positions;
        }

        public static CutterLocation[] GetCutterLocations(FaceToolPath toolPath) {
            var CutterLocations = new List<CutterLocation>();
            Vector tip = -toolPath.Csys.DirZ * toolPath.CuttingTool.Radius;
            foreach (IList<Point> points in GetChains(toolPath)) {
                //      CutterLocations.Add(new CutterLocation(toolPath.RestPoint(points[0]) + tip, true));
                CutterLocations.AddRange(points.Select(p => new CutterLocation(p + tip, false)));
                CutterLocations.Add(new CutterLocation(toolPath.RestPoint(points[points.Count - 1]) + tip, true));
            }

            return CutterLocations.ToArray();
        }

        private static int Count(FaceToolPath toolPath) {
            return (int)Math.Ceiling(toolPath.MaxLength / toolPath.CuttingParameters.StepOver);
        }
    }



}