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
    public class CuttingParameters {
        public double StepOver { get; set; }
        public double CutDepth { get; set; }
        public double FeedRate { get; set; }
        public double FeedRateRapid { get; set; }
        public double RestZ { get; set; }
        public double Increment { get; set; }

        public CuttingParameters() { }

        public CuttingParameters(double stepOver, double feedRate, double restZ) {
            StepOver = stepOver;
            CutDepth = stepOver;
            FeedRate = feedRate;
            FeedRateRapid = feedRate * 4;
            RestZ = restZ;
            Increment = 0.002;
        }

    }

}