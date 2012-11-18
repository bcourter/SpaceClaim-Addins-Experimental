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
    static class StrategyComboBox {
        const string commandName = "FaceToolPathToolStrategyList";

        static readonly string[] items = {
			"UV Contour",
            "Spiral"
		};

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.ControlState = ComboBoxState.CreateFixed(items, 0);
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static string Value {
            get {
                var state = (ComboBoxState)Command.ControlState;
                return items[state.SelectedIndex];
            }
            set {
                var state = (ComboBoxState)Command.ControlState;
                for (int i = 0; i < state.Items.Count; i++) {
                    if (items[i] == value) {
                        Command.ControlState = ComboBoxState.CreateFixed(state.Items, i);
                        break;
                    }
                }
            }
        }
    }
    static class ColorComboBox {
        const string commandName = "UVPathToolColorList";

        static readonly Color[] colorList = {
			Color.Gray,
			Color.Red,
			Color.Yellow,
			Color.Green,
			Color.Cyan,
			Color.Blue,
			Color.Magenta
		};

        public static void Initialize() {
            Command command = Command.Create(commandName);

            string[] items = Array.ConvertAll(colorList, color => color.Name);
            command.ControlState = ComboBoxState.CreateFixed(items, 0);
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static Color Value {
            get {
                var state = (ComboBoxState)Command.ControlState;
                return colorList[state.SelectedIndex];
            }
            set {
                var state = (ComboBoxState)Command.ControlState;
                for (int i = 0; i < state.Items.Count; i++) {
                    if (colorList[i] == value) {
                        Command.ControlState = ComboBoxState.CreateFixed(state.Items, i);
                        break;
                    }
                }
            }
        }
    }

    static class RadiusSlider {
        const string commandName = "UVPathToolRadiusSlider";
        const double sliderScale = 0.0254 / 32;
        const int sliderTicks = 32;
        const int startRadius = 4;

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.ControlState = SliderState.Create(startRadius, 1, sliderTicks);
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static double Value {
            get {
                var state = (SliderState)Command.ControlState;
                return (double)state.Value * sliderScale;
            }
            set {
                var state = (SliderState)Command.ControlState;
                Command.ControlState = SliderState.Create((int)(value / sliderScale), state.MinimumValue, state.MaximumValue);
            }
        }
    }

    static class StepSizeSlider {
        const string commandName = "UVPathToolStepSizeSlider";
        const double sliderScale = 0.0254 / 32;
        const int sliderTicks = 32;
        const int startValue = 4;

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.ControlState = SliderState.Create(startValue, 1, sliderTicks);
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static double Value {
            get {
                var state = (SliderState)Command.ControlState;
                return (double)state.Value * sliderScale;
            }
            set {
                var state = (SliderState)Command.ControlState;
                Command.ControlState = SliderState.Create((int)(value / sliderScale), state.MinimumValue, state.MaximumValue);
            }
        }
    }

    static class AnimateStepButton {
        const string commandName = "UVPathToolAnimateStepButton";
        const string commandText = "Step";

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.Text = commandText;
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }
    }

    static class AnimatePlayButton {
        const string commandName = "UVPathToolAnimatePlayButton";
        const string commandText = "Play";

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.Text = commandText;
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }
    }

    static class CreateSpheresButton {
        const string commandName = "UVPathToolCreateSpheres";
        const string commandText = "Create Spheres";

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.Text = commandText;
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }
    }

    class FaceToolPathToolButtonCapsule : RibbonButtonCapsule {

        public FaceToolPathToolButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
            : base("UV", Resources.UVPathToolButtonText, Resources.ToolPath32, Resources.UVPathToolButtonHint, parent, buttonSize) {
        }

        protected override void OnInitialize(Command command) {
            StrategyComboBox.Initialize();
            ColorComboBox.Initialize();
            StepSizeSlider.Initialize();
            AnimateStepButton.Initialize();
            AnimatePlayButton.Initialize();
            RadiusSlider.Initialize();
            CreateSpheresButton.Initialize();
        }

        protected override void OnUpdate(Command command) {
            Window window = Window.ActiveWindow;
            command.IsEnabled = window != null;
            command.IsChecked = window != null && window.ActiveTool is FaceToolPathTool;
        }

        protected override void OnExecute(Command command, SpaceClaim.Api.V10.ExecutionContext context, Rectangle buttonRect) {
            Window window = Window.ActiveWindow;
            window.SetTool(new FaceToolPathTool());
        }
    }

    class FaceToolPathTool : Tool {
        string strategy;
        double stepSize;
        double radius;
        Color color;
        ToolPathAnimator animator;
        ToolPath toolPath = null;


        public FaceToolPathTool()
            : base(InteractionMode.Solid) {
        }

        public override string OptionsXml {
            get { return Resources.UVPathToolOptions; }
        }

        protected override void OnInitialize() {
            Reset();
            animator = new ToolPathAnimator();
        }

        void Reset() {
            Rendering = null;
            SelectionTypes = new[] { typeof(DesignFace), typeof(CustomObject) };
            StatusText = "Click on a face to create a new sphere set.";

            stepSize = StepSizeSlider.Value;
            radius = RadiusSlider.Value;
        }

        protected override IDocObject AdjustSelection(IDocObject docObject) {
            var desFace = docObject as DesignFace;
            if (desFace != null)
                return desFace;

            var custom = docObject as CustomObject;
            if (custom != null)
                return custom.Type == ToolPathObject.Type ? custom : null;

            Debug.Fail("Unexpected case");
            return null;
        }

        protected override void OnEnable(bool enable) {
            if (enable)
                Window.PreselectionChanged += Window_PreselectionChanged;
            else
                Window.PreselectionChanged -= Window_PreselectionChanged;

            if (enable) {
                strategy = StrategyComboBox.Value;
                StrategyComboBox.Command.TextChanged += strategyCommand_TextChanged;
            }
            else
                StrategyComboBox.Command.TextChanged -= strategyCommand_TextChanged;

            if (enable) {
                stepSize = StepSizeSlider.Value;
                StepSizeSlider.Command.TextChanged += stepSizeSliderCommand_TextChanged;
            }
            else
                StepSizeSlider.Command.TextChanged -= stepSizeSliderCommand_TextChanged;

            if (enable) {
                AnimateStepButton.Command.Executing += animateStepCommand_Execute;
            }
            else
                AnimateStepButton.Command.Executing -= animateStepCommand_Execute;

            if (enable) {
                AnimatePlayButton.Command.Executing += animatePlayCommand_Execute;
            }
            else
                AnimatePlayButton.Command.Executing -= animatePlayCommand_Execute;


            if (enable) {
                color = ColorComboBox.Value;
                ColorComboBox.Command.TextChanged += colorCommand_TextChanged;
            }
            else
                ColorComboBox.Command.TextChanged -= colorCommand_TextChanged;

            if (enable) {
                radius = RadiusSlider.Value;
                RadiusSlider.Command.TextChanged += radiusSliderCommand_TextChanged;
            }
            else
                RadiusSlider.Command.TextChanged -= radiusSliderCommand_TextChanged;

            if (enable) {
                CreateSpheresButton.Command.Executing += createSphereCommand_Execute;
            }
            else
                CreateSpheresButton.Command.Executing -= createSphereCommand_Execute;
        }

        void strategyCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            strategy = StrategyComboBox.Value;

            ToolPathObject toolPathObj = SelectedToolPath;
            if (toolPath == null)
                return;

            if (strategy == "UV Contour")
                toolPath = new UVFacingToolPath(((FaceToolPath)toolPath).Face, toolPath.CuttingTool, toolPath.CuttingParameters);

            if (strategy == "Spiral")
                toolPath = new SpiralFacingToolPath(((FaceToolPath)toolPath).Face, toolPath.CuttingTool, toolPath.CuttingParameters);

            WriteBlock.ExecuteTask("Change toolpath strategy to " + strategy + ".", () => { toolPathObj.ToolPath = toolPath; toolPathObj.Regenerate(); });
        }

        void radiusSliderCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            radius = RadiusSlider.Value;

            ToolPathObject toolPathObj = SelectedToolPath;
            if (toolPathObj != null)
                WriteBlock.ExecuteTask("Adjust radius", () => { toolPathObj.ToolPath.CuttingTool.Radius = radius; toolPathObj.Regenerate(); });
        }

        void stepSizeSliderCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            stepSize = StepSizeSlider.Value;

            ToolPathObject toolPathObj = SelectedToolPath;
            if (toolPathObj != null) {
                WriteBlock.ExecuteTask("Update toolpath", () => { toolPathObj.ToolPath.CuttingParameters.StepOver = stepSize; toolPathObj.Regenerate(); });

            }
        }

        void colorCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            color = ColorComboBox.Value;

            ToolPathObject toolPath = SelectedToolPath;
            if (toolPath != null)
                WriteBlock.ExecuteTask("Adjust color", () => toolPath.Color = color);
        }

        void animateStepCommand_Execute(object sender, CommandExecutingEventArgs e) {
            animator.Advance(1);
        }

        void animatePlayCommand_Execute(object sender, CommandExecutingEventArgs e) {
            Command command = (Command)sender;

            //for (int i = 0; i < 33; i++)
            //    animator.Advance(1);

            if (Animation.IsAnimating)
                Animation.IsPaused = !Animation.IsPaused; // toggle Play/Pause
            else
                Animation.Start(Resources.UVPathToolAnimationPlay, animator);

            command.IsChecked = Animation.IsAnimating;
            command.Text = Animation.IsAnimating ? Resources.UVPathToolAnimationPause : Resources.UVPathToolAnimationPlay;
        }


        void createSphereCommand_Execute(object sender, CommandExecutingEventArgs e) {
#if false
            UVToolPath sphereSet = SelectedSphereSet;
            if (sphereSet == null)
                return;

            Part sphereRootPart = Part.Create(Window.Document, "Spheres");
            Component.Create(Window.ActiveWindow.Scene as Part, sphereRootPart);
            Part innerSpherePart = Part.Create(Window.Document, "Inner Spheres");
            Part outerSpherePart = Part.Create(Window.Document, "Outer Spheres");
            Component.Create(sphereRootPart, innerSpherePart);
            Component.Create(sphereRootPart, outerSpherePart);

            Part spherePart = Part.Create(Window.Document, "Sphere");
            ShapeHelper.CreateSphere(Point.Origin, sphereSet.Radius * 2, spherePart);

            Face face = sphereSet.DesFace.Shape;
            foreach (PointUV pointUV in sphereSet.Positions) {
                Point point = face.Geometry.Evaluate(pointUV).Point;
                bool isEdge = false;
                foreach (Edge edge in face.Edges) {
                    if ((edge.ProjectPoint(point).Point - point).MagnitudeSquared() < (sphereSet.Radius * sphereSet.Radius)) {
                        isEdge = true;
                        break;
                    }
                }

                Component component = Component.Create(isEdge ? outerSpherePart : innerSpherePart, spherePart);
                component.Placement = Matrix.CreateTranslation(point.Vector);
            }
#endif
        }

        void Window_PreselectionChanged(object sender, EventArgs e) {
            if (IsDragging)
                return;

            // preselection can change without the mouse moving (e.g. just created a profile)
            Rendering = null;

            InteractionContext context = InteractionContext;
            Line cursorRay = context.CursorRay;
            if (cursorRay != null)
                OnMouseMove(context.Window.CursorPosition, cursorRay, Control.MouseButtons);
        }

        protected override bool OnMouseMove(ScreenPoint cursorPos, Line cursorRay, MouseButtons button) {
            if (button != MouseButtons.None)
                return false;

            IDocObject preselection = InteractionContext.Preselection;
            DesignFace desFace = null;

            ToolPathObject existingSphereSet = ToolPathObject.GetWrapper(preselection as CustomObject);
            if (existingSphereSet != null)
                desFace = existingSphereSet.DesFace;
            if (desFace == null)
                desFace = preselection as DesignFace;
            if (desFace == null) // selection filtering is not applied if you (pre)select in the tree
                return false;

            if (ToolPathObject.AllUVPaths.Where(s => s.DesFace == desFace).Count() > 0)
                return false;

            Face face = desFace.Shape;
            Color prehighlightColor = Color.FromArgb(33, 255 - (255 - color.R) / 2, 255 - (255 - color.G) / 2, 255 - (255 - color.B) / 2);

            GraphicStyle style = new GraphicStyle {
                EnableDepthBuffer = true,
                FillColor = prehighlightColor,
                LineColor = color
            };

            var ballMill = new BallMill(radius, 4 * radius);
            var parameters = new CuttingParameters(radius, 1, 0.5 * Const.inches);

            if (strategy == "UV Contour")
                toolPath = new UVFacingToolPath(face, ballMill, parameters);

            if (strategy == "Spiral")
                toolPath = new SpiralFacingToolPath(face, ballMill, parameters);

            Debug.Assert(toolPath != null);

            Graphic curveGraphic, arrowGraphic;
            ToolPathObject.GetGraphics(toolPath, out curveGraphic, out arrowGraphic);
            Rendering = Graphic.Create(style, null, new[] { curveGraphic, arrowGraphic });

            return false; // if we return true, the preselection won't update
        }

        #region Click-Click Notifications

        protected override bool OnClickStart(ScreenPoint cursorPos, Line cursorRay) {
            IDocObject selection = null;
            IDocObject preselection = InteractionContext.Preselection;
            var desFace = preselection as DesignFace;
            if (desFace != null)
                WriteBlock.ExecuteTask("Create Tool Path", () => selection = ToolPathObject.Create(desFace, toolPath, color).Subject);
            else {
                ToolPathObject toolPathObj = ToolPathObject.GetWrapper(preselection as CustomObject);
                if (toolPathObj != null) {
                    selection = toolPathObj.Subject;

                    StepSizeSlider.Value = toolPathObj.ToolPath.CuttingParameters.StepOver;
                    RadiusSlider.Value = toolPathObj.ToolPath.CuttingTool.Radius;
                    ColorComboBox.Value = toolPathObj.Color;
                }
            }

            Window.ActiveContext.Selection = new[] { selection };
            return false;
        }

        #endregion

#if false
        #region Drag Notifications

        protected override bool OnDragStart(ScreenPoint cursorPos, Line cursorRay) {
            sphereSet = SphereSet.GetWrapper(InteractionContext.Preselection as CustomObject);
            if (sphereSet == null)
                return false;

            Point? pointOnCustom = InteractionContext.PreselectionPoint;
            if (pointOnCustom == null) {
                sphereSet = null;
                return false;
            }

            applyLoop = new ProfileApplyLoop(sphereSet);

            Point pointInFacePlane = sphereSet.Placement.Inverse * pointOnCustom.Value;
            Face profileFace = sphereSet.Face.Shape;

            profilePlane = profileFace.GetGeometry<Plane>();
            double offset = GetOffset(profileFace, profilePlane, pointInFacePlane, out profileFin);

            SelectionTypes = new Type[0]; // disable preselection while dragging
            StatusText = "Drag to modify the profile offset.";
            return true;
        }

        protected override void OnDragMove(ScreenPoint cursorPos, Line cursorRay) {
            Debug.Assert(sphereSet != null);

            cursorRay = sphereSet.Placement.Inverse * cursorRay;

            Point point;
            if (!profilePlane.TryIntersectLine(cursorRay, out point))
                return; // plane is side-on

            double offset = GetOffset(profileFin, profilePlane.Frame.DirZ, point);
            if (!Accuracy.EqualLengths(offset, sphereSet.Offset))
                applyLoop.Apply(offset);
        }

        protected override void OnDragEnd(ScreenPoint cursorPos, Line cursorRay) {
            applyLoop.Complete();
            Reset();
        }

        protected override void OnDragCancel() {
            applyLoop.Cancel();
            Reset();
        }
        #endregion
    }

    class SphereApplyLoop : ApplyLoop {
        readonly SphereSet sphereSet;
        double radius;

        public SphereApplyLoop(SphereSet sphereSet)
            : base("Modify sphere set.") {
            this.sphereSet = sphereSet;
        }

        public void Apply(double radius) {
            this.radius = radius;
            Apply();
        }

        protected override bool OnApply() {
            sphereSet.Radius = radius;
            return true;
        }
   
#endif

        public static ToolPathObject SelectedToolPath {
            get {
                IDocObject docObject = Window.ActiveWindow.ActiveContext.SingleSelection;
                if (docObject == null)
                    return null;

                return ToolPathObject.GetWrapper(docObject as CustomObject);
            }
        }

    }

    class ToolPathAnimator : Animator {
        public ToolPathAnimator() {
        }

        public override int Advance(int frame) {
            Debug.Assert(frame >= 1);

            ToolPathObject sphereSet = FaceToolPathTool.SelectedToolPath;
            if (sphereSet == null)
                return frame;

            //        WriteBlock.ExecuteTask("Animate Spheres", () => CalculateFrame(sphereSet));

            return frame + 2;
        }


        protected override void OnCompleted(AnimationCompletedEventArgs args) {
            if (args.Result == AnimationResult.Canceled) {
                UndoStepAdded = false;
            }

            base.OnCompleted(args);
        }

        public bool UndoStepAdded { get; private set; }
    }

}
