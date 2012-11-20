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
    public class ToolPathObject : CustomWrapper<ToolPathObject> {
        readonly DesignFace desFace;
        Color color;
        ToolPath toolPath;
        IList<CutterLocation> cutterLocations;

        const double tau = Math.PI * 2;
        static List<ToolPathObject> allUVPaths = new List<ToolPathObject>();

        // creates a wrapper for an existing custom object
        protected ToolPathObject(CustomObject subject)
            : base(subject) {
            allUVPaths.Add(this);
        }

        // creates a new custom object and a wrapper for it
        protected ToolPathObject(DesignFace desFace, ToolPath toolPath, Color color)
            : base(desFace.GetAncestor<Part>()) {
            this.desFace = desFace;
            this.color = color;
            this.toolPath = toolPath;

            desFace.KeepAlive(true);
            allUVPaths.Add(this);
        }

        ~ToolPathObject() {
            allUVPaths.Remove(this);
        }

        // static Create method follows the API convention and parent should be first argument
        public static ToolPathObject Create(DesignFace desFace, ToolPath toolPath, Color color) {
            Debug.Assert(desFace != null);

            var toolPathObj = new ToolPathObject(desFace, toolPath, color);
            toolPathObj.Initialize();
            toolPathObj.cutterLocations = toolPath.GetCutterLocations().ToArray();
            return toolPathObj;
        }

        public void Regenerate() {
            this.Initialize();
            cutterLocations = toolPath.GetCutterLocations().ToArray();
        }

#if false
        // if this returns true, the custom object can be used with the Move tool
        public override bool TryGetTransformFrame(out Frame frame, out Transformations transformations) {
            Face face = desFace.Shape;
            var plane = face.GetGeometry<Plane>();
            Point center = plane.ProjectPoint(face.GetBoundingBox(Matrix.Identity).Center).Point;
            Direction dirZ = plane.Frame.DirZ;
            if (face.IsReversed)
                dirZ = -dirZ;
            frame = placement * Frame.Create(center, dirZ);

            // only offer transformations in the plane
            transformations = Transformations.TranslateX | Transformations.TranslateY | Transformations.RotateZ;
            return true;
        }

        // the Move tool uses this
        public override void Transform(Matrix trans) {
            Face face = desFace.Shape;

            // only accept transformations in the plane
            var plane = face.GetGeometry<Plane>();
            if (plane.IsCoincident(trans * plane)) {
                placement = trans * placement;
                Commit();
            }
        }

        // the Move tool uses this if the Ctrl key is down
        public override SphereSet Copy() {
            SphereSet copy = Create(desFace, offset, color, count);
            copy.Placement = placement;
            return copy;
        }
#endif

        protected override bool IsAlive {
            get {
                if (desFace == null || desFace.IsDeleted)
                    return false;

                return true;
            }
        }

        public new static string DefaultDisplayName {
            get { return "UV Tool Path"; }
        }

        public new static System.Drawing.Image[] ImageList {
            get { return new[] { Resources.UVToolPath, Resources.UVToolPathDisabled }; }
        }

        protected override ICollection<IDocObject> Determinants {
            get { return new IDocObject[] { desFace }; }
        }

        protected override bool Update() {
#if false
			DisplayImage = new DisplayImage(0, 1);
			UpdateRendering(CancellationToken.None);
			return true; // update was done
#else
            return false; // update was not done - use async update
#endif
        }

        protected override void UpdateAsync(CancellationToken token) {
            DisplayImage = new DisplayImage(0, 1);
            UpdateRendering(token);
        }

        void UpdateRendering(CancellationToken token) {
            Face face = desFace.Shape;
            Graphic curveGraphic, arrowGraphic;
            cutterLocations = GetGraphics(toolPath, out curveGraphic, out arrowGraphic);
            GraphicStyle style;

            Color transparentColor = Color.FromArgb(44, color);
            Color selectedColor = Color.FromArgb(transparentColor.A, transparentColor.R / 2, transparentColor.G / 2, transparentColor.B / 2);
            Color prehighlightColor = Color.FromArgb(transparentColor.A, 255 - (255 - transparentColor.R) / 2, 255 - (255 - transparentColor.G) / 2, 255 - (255 - transparentColor.B) / 2);
            Color curveColor = Color.FromArgb(255, selectedColor);
            Color curvePrehighlightColor = Color.FromArgb(255, prehighlightColor);

            //// nucleus
            ////style = new GraphicStyle {
            ////    IsPrimarySelection = false,
            ////    IsPreselection = false,
            ////    EnableDepthBuffer = true,
            ////    FillColor = color  // BZC > DTR try replacing with transparentColor
            ////};
            ////Graphic visible = Graphic.Create(style, null, sphere);

            //style = new GraphicStyle {
            //    IsPrimarySelection = true,
            //    FillColor = selectedColor
            //};
            //Graphic selected = Graphic.Create(style, null, shadedGraphic); //visible);  //TBD David fixes bug

            //style = new GraphicStyle {
            //    IsPrimarySelection = false,
            //    IsPreselection = true,
            //    FillColor = prehighlightColor
            //};
            //Graphic prehighlighted = Graphic.Create(style, null, selected);

            // curves
            style = new GraphicStyle {
                LineColor = curveColor,
                FillColor = curveColor
            };
            Graphic selectedCurves = Graphic.Create(style, null, curveGraphic);
            Graphic selectedArrows = Graphic.Create(style, null, arrowGraphic);

            style = new GraphicStyle {
                IsPreselection = true,
                LineColor = curvePrehighlightColor,
                FillColor = curvePrehighlightColor,
                LineWidth = 3
            };
            Graphic prehighlightedCurves = Graphic.Create(style, null, curveGraphic);
            Graphic prehighlightedArrows = Graphic.Create(style, null, arrowGraphic);

            style = new GraphicStyle {
                EnableDepthBuffer = true,
            };

            //    Rendering = Graphic.Create(style, null, new[] { prehighlighted, prehighlightedShell, selectedShell });
            Rendering = Graphic.Create(style, null, new[] { prehighlightedCurves, selectedCurves });
        }

        public static IList<CutterLocation> GetGraphics(ToolPath toolPath, out Graphic curveGraphic, out Graphic arrowGraphic) {
            IList<CurveSegment> cutterCurves;
            IList<CurveSegment> rapidCurves;
            IList<CurveSegment> arrowCurves;
            IList<CutterLocation> cutterLocations = toolPath.GetCurves(out cutterCurves, out rapidCurves, out arrowCurves);

            var style = new GraphicStyle {
                LineWidth = 2
            };
            Graphic cutterGraphic = Graphic.Create(style, cutterCurves.Select(c => CurvePrimitive.Create(c)).ToArray());

            style = new GraphicStyle {
                LineWidth = 1
            };
            Graphic rapidGraphic = Graphic.Create(style, rapidCurves.Select(c => CurvePrimitive.Create(c)).ToArray());

            curveGraphic = Graphic.Create(null, null, new[] { cutterGraphic, rapidGraphic });

            style = new GraphicStyle {
                LineColor = Color.Black,
                FillColor = Color.Gray,
                //  IsFlatOn = true,
                LineWidth = 1
            };
            //arrowGraphic = Graphic.Create(style, arrowCurves.Select(c => {
            //    var point = c.StartPoint;
            //    var tangent = (c.EndPoint - c.StartPoint).Direction;
            //    var frame = Frame.Create(point, tangent);
            //    return ArrowPrimitive.Create(frame, 20, 10);
            //}).ToArray());
            arrowGraphic = null;

            return cutterLocations;
        }

        public static ToolPathObject SelectedToolPath {
            get {
                IDocObject docObject = Window.ActiveWindow.ActiveContext.SingleSelection;
                if (docObject == null)
                    return null;

                return GetWrapper(docObject as CustomObject);
            }
        }

        public static IList<ToolPathObject> AllUVPaths {
            get { return allUVPaths; }
        }

        public DesignFace DesFace {
            get { return desFace; }
        }

        public ToolPath ToolPath {
            get { return toolPath; }
            set {
                if (value == toolPath)
                    return;

                toolPath = value;
                Commit();
            }
        }

        public Color Color {
            get { return color; }
            set {
                if (value == color)
                    return;

                color = value;
                Commit();
            }
        }

        public IList<CutterLocation> CutterLocations {
            get { return cutterLocations; }
        }

    }

}
