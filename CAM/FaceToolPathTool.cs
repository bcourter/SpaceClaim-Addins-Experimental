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
    class FaceToolPathToolButtonCapsule : RibbonButtonCapsule {
        public FaceToolPathToolButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
            : base("UV", Resources.FaceToolPathToolButtonText, Resources.ToolPath32, Resources.FaceToolPathToolButtonHint, parent, buttonSize) {
        }

        protected override void OnInitialize(Command command) {
            StrategyComboBox.Initialize();
            CuttingToolComboBox.Initialize();
            StepOverTextBox.Initialize();
            ColorComboBox.Initialize();
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

    static class CuttingToolComboBox {
        const string commandName = "FaceToolPathToolCutterList";

        static readonly string[] items = BallMill.StandardSizes.Keys.ToArray();

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.ControlState = ComboBoxState.CreateFixed(items, 2);
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static CuttingTool Value {
            get {
                var state = (ComboBoxState)Command.ControlState;
                return BallMill.StandardSizes[items[state.SelectedIndex]];
            }
            set {
                var state = (ComboBoxState)Command.ControlState;
                for (int i = 0; i < state.Items.Count; i++) {
                    if (BallMill.StandardSizes.Values.ToArray()[i] == value) {
                        Command.ControlState = ComboBoxState.CreateFixed(state.Items, i);
                        return;
                    }
                }

                throw new KeyNotFoundException("Invalid tool size.");
            }
        }
    }

    static class StepOverTextBox {
        const string commandName = "FaceToolPathToolStepOverTextBox";
        const string labelCommandName = "FaceToolPathToolStepOverLabel";

        public static void Initialize() {
            Command command = Command.Create(commandName);
            Value = CuttingToolComboBox.Value.Radius;
            command = Command.Create(labelCommandName);
            command.Text = Resources.StepOver + ":";
        }

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static double Value {
            get {
                double value;
                if (!Double.TryParse(Command.Text, out value))
                    return CuttingToolComboBox.Value.Radius;

                return value / Window.ActiveWindow.Units.Length.ConversionFactor;
            }
            set {
                Command.Text = (value * Window.ActiveWindow.Units.Length.ConversionFactor).ToString();
            }
        }
    }

    static class ColorComboBox {
        const string commandName = "FaceToolPathToolColorList";

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

    class FaceToolPathTool : Tool {
        public FaceToolPathTool()
            : base(InteractionMode.Solid) {
        }

        public override string OptionsXml {
            get { return Resources.FaceToolPathToolOptions; }
        }

        protected override void OnInitialize() {
            Reset();
        }

        void Reset() {
            Rendering = null;
            SelectionTypes = new[] { typeof(DesignFace), typeof(CustomObject) };
            StatusText = Resources.FaceToolPathToolStatusText;
        }

        #region Command notifications

        protected override void OnEnable(bool enable) {
            if (enable)
                Window.PreselectionChanged += Window_PreselectionChanged;
            else
                Window.PreselectionChanged -= Window_PreselectionChanged;

            if (enable)
                Window.ActiveWindowChanged += Window_ActiveWindowChanged;
            else
                Window.ActiveWindowChanged -= Window_ActiveWindowChanged;

            if (enable) {
                StrategyComboBox.Command.TextChanged += strategyCommand_TextChanged;
            }
            else
                StrategyComboBox.Command.TextChanged -= strategyCommand_TextChanged;

            if (enable) {
                CuttingToolComboBox.Command.TextChanged += cuttingToolCommand_TextChanged;
            }
            else
                CuttingToolComboBox.Command.TextChanged -= cuttingToolCommand_TextChanged;

            if (enable) {
                 StepOverTextBox.Command.TextChanged += stepOverCommand_TextChanged;
            }
            else
                StepOverTextBox.Command.TextChanged -= stepOverCommand_TextChanged;

            if (enable) {
                ColorComboBox.Command.TextChanged += colorCommand_TextChanged;
            }
            else
                ColorComboBox.Command.TextChanged -= colorCommand_TextChanged;

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

        void Window_ActiveWindowChanged(object sender, EventArgs e) {
       ;
        }

        void strategyCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            string strategy = StrategyComboBox.Value;

            ToolPathObject toolPathObj = ToolPathObject.SelectedToolPath;
            if (toolPathObj == null)
                return;

            ToolPath toolPath = toolPathObj.ToolPath;
            if (strategy == "UV Contour")
                toolPath = new UVFacingToolPath(((FaceToolPath)toolPath).Face, toolPath.CuttingTool, toolPath.CuttingParameters);

            if (strategy == "Spiral")
                toolPath = new SpiralFacingToolPath(((FaceToolPath)toolPath).Face, toolPath.CuttingTool, toolPath.CuttingParameters);

            WriteBlock.ExecuteTask("Change toolpath strategy to " + strategy, () => { toolPathObj.ToolPath = toolPath; toolPathObj.Regenerate(); });
        }

        void cuttingToolCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            ToolPathObject toolPathObj = ToolPathObject.SelectedToolPath;
            if (toolPathObj != null)
                WriteBlock.ExecuteTask("Change cutter", () => { toolPathObj.ToolPath.CuttingTool = CuttingToolComboBox.Value; toolPathObj.Regenerate(); });
        }

        void stepOverCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            ToolPathObject toolPathObj = ToolPathObject.SelectedToolPath;
            if (toolPathObj != null) 
                WriteBlock.ExecuteTask("Update step size", () => { toolPathObj.ToolPath.CuttingParameters.StepOver = StepOverTextBox.Value; toolPathObj.Regenerate(); });
        }

        void colorCommand_TextChanged(object sender, CommandTextChangedEventArgs e) {
            ToolPathObject toolPath = ToolPathObject.SelectedToolPath;
            if (toolPath != null)
                WriteBlock.ExecuteTask("Adjust color", () => toolPath.Color = ColorComboBox.Value);
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

        #endregion

        #region Mouse Notifications

        ToolPath toolPath;
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
            Color color = ColorComboBox.Value;
            Color prehighlightColor = Color.FromArgb(33, 255 - (255 - color.R) / 2, 255 - (255 - color.G) / 2, 255 - (255 - color.B) / 2);

            GraphicStyle style = new GraphicStyle {
                EnableDepthBuffer = true,
                FillColor = prehighlightColor,
                LineColor = color
            };

            BallMill tool = CuttingToolComboBox.Value as BallMill;
            if (tool == null)
                throw new NotImplementedException("Only ball mills supported.");

            var parameters = new CuttingParameters(StepOverTextBox.Value, 15 * Const.inches, 0.25 * Const.inches);

            string strategy = StrategyComboBox.Value;
            if (strategy == "UV Contour")
                toolPath = new UVFacingToolPath(face, tool, parameters);

            if (strategy == "Spiral")
                toolPath = new SpiralFacingToolPath(face, tool, parameters);

            Debug.Assert(toolPath != null);

            Graphic curveGraphic, arrowGraphic;
            ToolPathObject.GetGraphics(toolPath, out curveGraphic, out arrowGraphic);
            Rendering = Graphic.Create(style, null, new[] { curveGraphic, arrowGraphic });

            return false; // if we return true, the preselection won't update
        }

        protected override bool OnClickStart(ScreenPoint cursorPos, Line cursorRay) {
            IDocObject selection = null;
            IDocObject preselection = InteractionContext.Preselection;
            var desFace = preselection as DesignFace;
            if (desFace != null)
                WriteBlock.ExecuteTask("Create Tool Path", () => selection = ToolPathObject.Create(desFace, toolPath, ColorComboBox.Value).Subject);
            else {
                ToolPathObject toolPathObj = ToolPathObject.GetWrapper(preselection as CustomObject);
                if (toolPathObj != null) {
                    selection = toolPathObj.Subject;


                    if (toolPathObj.ToolPath is UVFacingToolPath)
                        StrategyComboBox.Value = "UV Contour";
                    if (toolPathObj.ToolPath is SpiralFacingToolPath)
                        StrategyComboBox.Value = "Spiral";

                    StepOverTextBox.Value = toolPathObj.ToolPath.CuttingParameters.StepOver;
                    CuttingToolComboBox.Value = toolPathObj.ToolPath.CuttingTool;
                    ColorComboBox.Value = toolPathObj.Color;
                }
            }

            Window.ActiveContext.Selection = new[] { selection };
            return false;
        }

        #endregion
    }

}
