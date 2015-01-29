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
    public delegate void ToolPathChangedEventHandler(object sender, EventArgs e);

    public class FaceToolPathObject : CustomWrapper<FaceToolPathObject> {
        IDesignFace iDesFace;
        Color color;
        FaceToolPath toolPath;

        static List<FaceToolPathObject> allUVPaths = new List<FaceToolPathObject>();
        public static FaceToolPath DefaultToolPath { get; set; }
        public static Color DefaultColor { get; set; }
        public static int DefaultStrategy { get; set; }

        public static readonly Dictionary<FaceToolPath.StrategyType, string> TypeNames = new Dictionary<FaceToolPath.StrategyType, string> {
            {FaceToolPath.StrategyType.UV, Resources.ToolPathFaceUV},
            {FaceToolPath.StrategyType.Spiral, Resources.ToolPathFaceSpiral},
            {FaceToolPath.StrategyType.Contour, Resources.ToolPathFaceContour} 
        };

        public event ToolPathChangedEventHandler ToolPathChanged;

        // creates a wrapper for an existing custom object
        protected FaceToolPathObject(CustomObject subject)
            : base(subject) {
            allUVPaths.Add(this);
        }

        // creates a new custom object and a wrapper for it
        protected FaceToolPathObject(IDesignFace iDesFace, FaceToolPath toolPath, Color color)
            : base(Window.ActiveWindow.Scene as Part) {
            this.toolPath = toolPath;
            this.iDesFace = iDesFace;
            this.color = color;

            if (iDesFace != null) {
                IDesFace = iDesFace;
                iDesFace.KeepAlive(true);
            }

            allUVPaths.Add(this);
        }

        ~FaceToolPathObject() {
            allUVPaths.Remove(this);
        }

        // static Create method follows the API convention and parent should be first argument
        public static FaceToolPathObject Create(IDesignFace desFace, FaceToolPath toolPath, Color color) {
            Debug.Assert(desFace == null || desFace.Master.Shape == toolPath.Face);
            FaceToolPathObject toolPathObj = null;
            WriteBlock.ExecuteTask("New toolpath", () => toolPathObj = new FaceToolPathObject(desFace, toolPath, color));
            toolPathObj.Initialize();

        //    IList<CutterLocation> cutterLocations;
        //    toolPath.TryGetCutterLocations(out cutterLocations);
        //    toolPathObj.cutterLocations = cutterLocations;
            return toolPathObj;
        }

        public static FaceToolPathObject Create(IDesignFace desFace) {
            return Create(desFace, DefaultToolPath, DefaultColor);
        }

        public void Regenerate() {
            this.Initialize();
 //           ToolPath = toolPath;
            if (IDesFace == null)
                return;

            ToolPath.UpdateCutterLocations();
            ToolPath = ToolPath; // make sure we have serialized
            UpdateRendering(new CancellationToken());

            DefaultToolPath = ToolPath;
            DefaultColor = Color;

            if (ToolPathChanged != null)
                ToolPathChanged(this, new EventArgs());
        }

        protected override bool IsAlive {
            get {
                if (iDesFace != null && iDesFace.IsDeleted)
                    return false;

                return true;
            }
        }

#if true
        public new static string DefaultDisplayName {
            get { return Resources.FaceToolPath; }
        }
#endif

        public new static System.Drawing.Image[] ImageList {
            get { return new[] { Resources.UVToolPath, Resources.UVToolPathDisabled }; }
        }

        protected override ICollection<IDocObject> Determinants {
            get { return new IDocObject[] { iDesFace }; }
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
            if (iDesFace == null)
                return;

            Face face = iDesFace.Master.Shape;
            Graphic curveGraphic, arrowGraphic;
            GetGraphics(ToolPath, out curveGraphic, out arrowGraphic);
            GraphicStyle style;

            Color transparentColor = Color.FromArgb(44, color);
            Color selectedColor = Color.FromArgb(transparentColor.A, transparentColor.R / 2, transparentColor.G / 2, transparentColor.B / 2);
            Color prehighlightColor = Color.FromArgb(transparentColor.A, 255 - (255 - transparentColor.R) / 2, 255 - (255 - transparentColor.G) / 2, 255 - (255 - transparentColor.B) / 2);
            Color curveColor = Color.FromArgb(255, selectedColor);
            Color curvePrehighlightColor = Color.FromArgb(255, prehighlightColor);

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

        public static void GetGraphics(ToolPath toolPath, out Graphic curveGraphic, out Graphic arrowGraphic) {
            IList<CurveSegment> cutterCurves;
            IList<CurveSegment> rapidCurves;
            IList<CurveSegment> arrowCurves;
            toolPath.GetCurves(out cutterCurves, out rapidCurves, out arrowCurves);

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
        }

        public static FaceToolPathObject SelectedToolPath {
            get {
                IDocObject docObject = Window.ActiveWindow.ActiveContext.SingleSelection;
                if (docObject == null)
                    return null;

                ICustomObject iCustomObject = docObject as ICustomObject;
                if (iCustomObject == null)
                    return null;

                return GetWrapper(iCustomObject.Master);
            }
        }

        public static FaceToolPathObject DefaultToolPathObject {
            get {
                Debug.Assert(DefaultToolPath != null);
                return FaceToolPathObject.Create(null, DefaultToolPath, DefaultColor);
            }
        }

        public static IList<FaceToolPathObject> AllUVPaths {
            get { return allUVPaths; }
        }

        public static ICollection<PropertyDisplay> Properties {
            get {
                return new PropertyDisplay[] {
                    new ToolPathStrategyProperty(),
                    new ToolPathColorProperty(),

                    new ToolPathPropertyDisplay(Resources.CuttingToolParameters, Resources.CuttingHeight, (toolPathObj) => toolPathObj.ToolPath.CuttingTool.Radius * 2, (toolPathObj, value) => toolPathObj.ToolPath.CuttingTool.Radius = value/2),
                    new ToolPathPropertyDisplay(Resources.CuttingToolParameters, Resources.Diameter, (toolPathObj) => toolPathObj.ToolPath.CuttingTool.CuttingHeight, (toolPathObj, value) => toolPathObj.ToolPath.CuttingTool.CuttingHeight = value),
              
                    new ToolPathPropertyDisplay(Resources.CuttingParameters, Resources.StepOver, (toolPathObj) => toolPathObj.ToolPath.CuttingParameters.StepOver, (toolPathObj, value) => toolPathObj.ToolPath.CuttingParameters.StepOver = value),
                    new ToolPathPropertyDisplay(Resources.CuttingParameters, Resources.CutDepth, (toolPathObj) => toolPathObj.ToolPath.CuttingParameters.CutDepth, (toolPathObj, value) => toolPathObj.ToolPath.CuttingParameters.CutDepth = value),
                    new ToolPathPropertyDisplay(Resources.CuttingParameters, Resources.FeedRate, (toolPathObj) => toolPathObj.ToolPath.CuttingParameters.FeedRate, (toolPathObj, value) => toolPathObj.ToolPath.CuttingParameters.FeedRate = value),
                    new ToolPathPropertyDisplay(Resources.CuttingParameters, Resources.FeedRateRapid, (toolPathObj) => toolPathObj.ToolPath.CuttingParameters.FeedRateRapid, (toolPathObj, value) => toolPathObj.ToolPath.CuttingParameters.FeedRateRapid = value), 
                    new ToolPathPropertyDisplay(Resources.CuttingParameters, Resources.RestZ, (toolPathObj) => toolPathObj.ToolPath.CuttingParameters.RestZ, (toolPathObj, value) => toolPathObj.ToolPath.CuttingParameters.RestZ = value),
                    new ToolPathPropertyDisplay(Resources.CuttingParameters, Resources.Increment, (toolPathObj) => toolPathObj.ToolPath.CuttingParameters.Increment, (toolPathObj, value) => toolPathObj.ToolPath.CuttingParameters.Increment = value)   
                };
            }
        }

        private new void Commit() {
            WriteBlock.ExecuteTask("Commit", () => base.Commit());
        }

        public IDesignFace IDesFace {
            get { return iDesFace; }
            set {
                iDesFace = value;
                ToolPath.Face = IDesFace.Master.Shape;
                iDesFace.KeepAlive(true);

                //     WriteBlock.ExecuteTask("Set face of tool path", () => Subject.Name = iDesFace != null ? Resources.FaceToolPath : /*Subject.Name = null*/ " - ");

                Regenerate();
                Commit();
            }
        }

#if true
        public string toolPathSerialization = String.Empty;
        public FaceToolPath ToolPath {
            get {
                if (toolPath == null) {
                    toolPath = (FaceToolPath)FaceToolPath.FromString<FaceToolPath>(toolPathSerialization);
                    if (toolPath != null)
                        toolPath.Face = IDesFace == null ? null : IDesFace.Master.Shape;
                    else
                        toolPath = DefaultToolPath;
                }

                return toolPath;
            }
            set {
                if (value == toolPath && toolPathSerialization != string.Empty)
                    return;

                toolPath = value;
                toolPathSerialization = toolPath.ToString();
                Regenerate();
                Commit();
            }
        }

        public bool HasToolPath { get { return toolPath != null; } }
#else
        public FaceToolPath ToolPath {
            get { return toolPath; }
            set {
                if (value == toolPath)
                    return;

                toolPath = value;
                Commit();
            }
        }
#endif

        public Color Color {
            get { return color; }
            set {
                if (value == color)
                    return;

                color = value;
                Commit();
            }
        }

    }

    public class ToolPathPropertyDisplay : SimplePropertyDisplay {
        Func<FaceToolPathObject, double> getValue;
        Action<FaceToolPathObject, double> setValue;

        public ToolPathPropertyDisplay(string category, string name, Func<FaceToolPathObject, double> getValue, Action<FaceToolPathObject, double> setValue)
            : base(category, name) {
            this.getValue = getValue;
            this.setValue = setValue;
        }

        public override string GetValue(IDocObject obj) {
            Debug.Assert(obj != null);

            var iCustomObj = obj as ICustomObject;
            if (iCustomObj != null) {
                var toolPathObj = FaceToolPathObject.GetWrapper(iCustomObj.Master);
                if (toolPathObj != null) { // && toolPathObj.HasToolPath) {
                    return Window.ActiveWindow.Units.Length.Format(getValue(toolPathObj));
                }
            }
            return null;
        }

        public override bool SetValue(IDocObject obj, string value) {
            Debug.Assert(obj != null);
            Debug.Assert(!string.IsNullOrEmpty(value));

            double val;
            if (!Window.ActiveWindow.Units.Length.TryParse(value, out val))
                return false;
            if (Accuracy.LengthIsNegative(val))
                return false;

            var iCustomObj = (ICustomObject)obj;
            var toolPathObj = FaceToolPathObject.GetWrapper(iCustomObj.Master);
            setValue(toolPathObj, val);
            toolPathObj.Regenerate();
            return true;
        }
    }

    class ToolPathStrategyProperty : SimplePropertyDisplay {
        public ToolPathStrategyProperty()
            : base(Resources.Strategy, Resources.Strategy) {
        }

        public override ICollection<string> AllowableValues { get { return FaceToolPathObject.TypeNames.Values; } }

        public override string GetValue(IDocObject obj) {
            Debug.Assert(obj != null);

            var iCustomObj = obj as ICustomObject;
            if (iCustomObj != null) {
                var toolPathObj = FaceToolPathObject.GetWrapper(iCustomObj.Master);
                if (toolPathObj != null) { // && toolPathObj.HasToolPath) {
                    return FaceToolPathObject.TypeNames[toolPathObj.ToolPath.Strategy];
                }
            }
            return null;
        }

        public override bool SetValue(IDocObject obj, string value) {
            Debug.Assert(obj != null);
            Debug.Assert(!string.IsNullOrEmpty(value));

            var iCustomObj = (ICustomObject)obj;
            var toolPathObj = FaceToolPathObject.GetWrapper(iCustomObj.Master);
            toolPathObj.ToolPath.Strategy = FaceToolPathObject.TypeNames.Keys.Where(s => FaceToolPathObject.TypeNames[s] == value).First();
            toolPathObj.Regenerate();
            return true;
        }
    }

    class ToolPathColorProperty : SimplePropertyDisplay {
        public static readonly Color[] ColorList = {
			Color.Gray,
			Color.Red,
			Color.Yellow,
			Color.Green,
			Color.Cyan,
			Color.Blue,
			Color.Magenta
		};

        public ToolPathColorProperty()
            : base(Resources.Vizualization, Resources.Color) {
        }

        public override ICollection<string> AllowableValues { get { return Array.ConvertAll(ColorList, c => c.Name); } }

        public override string GetValue(IDocObject obj) {
            Debug.Assert(obj != null);

            var iCustomObj = obj as ICustomObject;
            if (iCustomObj != null) {
                var toolPathObj = FaceToolPathObject.GetWrapper(iCustomObj.Master);
                if (toolPathObj != null) { 
                    return toolPathObj.Color.Name;
                }
            }
            return null;
        }

        public override bool SetValue(IDocObject obj, string value) {
            Debug.Assert(obj != null);
            Debug.Assert(!string.IsNullOrEmpty(value));

            var iCustomObj = (ICustomObject)obj;
            var toolPathObj = FaceToolPathObject.GetWrapper(iCustomObj.Master);
            for (int i = 0; i < ColorList.Length; i++) {
                if (AllowableValues.ToArray()[i] == value) {
                    toolPathObj.Color = ColorList[i];
                    break;
                }
            }
            //           toolPathObj.Regenerate();
            return true;
        }
    }

}
