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
    public abstract class Instruction {
        public Instruction() {

        }
    }

    public class Operation : Instruction {
        List<Instruction> Instructions { get; set; }

        public Operation(IEnumerable<Instruction> instructions) {
            Instructions = instructions.ToList();
        }
    }


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
        public Point Center { get; private set; }
        public Point Point { get; private set; }
        public bool IsRapid { get; private set; }

        public CutterLocation(Point center, Point point, bool isRapid)
            : this() {
            Center = center;
            Point = point;
            IsRapid = isRapid;
        }

        public CutterLocation(Point center, Vector tip, bool isRapid)
            : this() {
            Center = center;
            Point = center + tip;
            IsRapid = isRapid;
        }
    }

}