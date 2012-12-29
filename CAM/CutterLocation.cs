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
using System.Xml;
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
    public struct ToolEvaluation {
        public Point CenterPoint { get; private set; }
        public Point SurfacePoint { get; private set; }
        public Direction SurfaceNormal { get; private set; }

        public ToolEvaluation(Point centerPoint, Point surfacePoint, Direction surfaceNormal) {
            CenterPoint = centerPoint;
            SurfacePoint = surfacePoint;
            SurfaceNormal = surfaceNormal;
        }
    }

    public struct CutterLocation {
        [XmlIgnoreAttribute]
        public Point Point { get; private set; }
        public bool IsRapid { get; set; }

        public CutterLocation(Point point, bool isRapid)
            : this() {
            Point = point;
            IsRapid = isRapid;
        }

        public double[] SerializablePoint {
            get { return new[] { Point.X, Point.Y, Point.Z }; }
            set { Point = Point.Create(value[0], value[1], value[2]); }
        }
    }

}