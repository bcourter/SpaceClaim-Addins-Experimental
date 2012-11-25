/*
 * Sample add-in for the SpaceClaim API
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
//using System.Threading;
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
    class AnimationToolButtonCapsule : RibbonButtonCapsule {
        public AnimationToolButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
            : base("Animate", Resources.AnimationToolButtonText, Resources.Animate32, Resources.AnimationToolButtonHint, parent, buttonSize) {
        }

        protected override void OnInitialize(Command command) {
            new AnimateCapsule().Initialize();

            SpeedSlider.Initialize();
        }

        protected override void OnUpdate(Command command) {
            Window window = Window.ActiveWindow;
            command.IsEnabled = ToolPathObject.SelectedToolPath != null;
            command.IsChecked = window != null && window.ActiveTool is AnimationTool;
        }

        protected override void OnExecute(Command command, SpaceClaim.Api.V10.ExecutionContext context, Rectangle buttonRect) {
            Window window = Window.ActiveWindow;
            window.SetTool(new AnimationTool());
        }
    }

    static class SpeedSlider {
        const string commandName = "AnimationToolSpeedSlider";
        const string labelCommandName = "AnimationToolSpeedLabel";
        const int minValue = 0;
        const int maxValue = 40;

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.IsWriteBlock = false;
            Command.Create(labelCommandName);
            Value = 1;
        }

#if false  // for abstract
        public static void SetEnable(bool enable) {
            if (enable)
                Command.TextChanged += TextChanged;
            else
                Command.TextChanged -= TextChanged;
        }

        static void TextChanged(object sender, CommandTextChangedEventArgs e) {
            color = ColorComboBox.SelectedColor;
        }
#endif

        public static Command Command {
            get { return Command.GetCommand(commandName); }
        }

        public static Command LabelCommand {
            get { return Command.GetCommand(labelCommandName); }
        }

        public static double Value {
            get {
                var state = (SliderState)Command.ControlState;
                return Math.Pow(2, (double)state.Value / 10 - 1);
            }
            set {
                var state = (SliderState)Command.ControlState;
                int v = (int)Math.Round((Math.Log(value) / Math.Log(2) + 1) * 10);
                Command.ControlState = SliderState.Create(v, minValue, maxValue);
                LabelCommand.Text = string.Format(Resources.AnimationToolSpeedLabel, value);
            }
        }
    }


    class AnimationTool : Tool {
        public AnimationTool()
            : base(InteractionMode.Solid) {
        }

        public override string OptionsXml {
            get { return Resources.AnimationToolOptions; }
        }

        protected override void OnInitialize() {
            var layout = new ToolGuideLayout();
            layout.AddButton(Command.GetCommand(AnimateCapsule.CommandName));
            SetToolGuideLayout(layout);
        }

        protected override IDocObject AdjustSelection(IDocObject docObject) {
            return docObject as CustomObject;
        }

        protected override void OnEnable(bool enable) {


        }

        protected override bool OnClickStart(ScreenPoint cursorPos, Line cursorRay) {
            InteractionContext.SingleSelection = InteractionContext.Preselection;
            return false;
        }
    }

    class AnimateCapsule : CommandCapsule {
        public const string CommandName = "ToolPathAnimate.Animate";

        public AnimateCapsule()
            : base(CommandName, Resources.AnimationToolPlay, Resources.TransportPlay, Resources.AnimationToolPlay) {
        }

        protected override void OnInitialize(Command command) {
            command.IsWriteBlock = false;
        }

        protected override void OnUpdate(Command command) {
            command.IsEnabled = AnimationTool != null && (Animation.IsAnimating || ToolPathObject.SelectedToolPath != null);

            if (Animation.IsAnimating && !Animation.IsPaused) {
                command.Image = Resources.TransportPause;
                command.Text = Resources.AnimationToolPause;
                command.Hint = Resources.AnimationToolPause;
            }
            else {
                command.Image = Resources.TransportPlay;
                command.Text = Resources.AnimationToolPlay;
                command.Hint = Resources.AnimationToolPlay;
            }
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect) {
            if (Animation.IsAnimating) {
                Animation.IsPaused = !Animation.IsPaused;
                animator.WasJustPaused = true;
            }
            else
                Animate(command, ToolPathObject.SelectedToolPath);
        }

        static ToolPathAnimator animator = null;
        static void Animate(Command command, ToolPathObject toolPathObj) {

            //     Window window = Window.ActiveWindow;
            //      window.ActiveContext.SingleSelection = null; // clear selection

            animator = new ToolPathAnimator(toolPathObj, AnimationTool);

            animator.Completed += (sender, e) => {
                if (e.Result == AnimationResult.Exhausted) {
                    //if (animator.UndoStepAdded)
                    //    Application.Undo(1); // rewind to start
                    //Animation.Start(message, animator, command); // restart
                }
                //else // animation was canceled or stopped
                //  window.ActiveContext.SingleSelection = toolPathObj.Subject;
            };

            Animation.Start(Resources.AnimatingMessage, animator, command);
        }

        public static AnimationTool AnimationTool {
            get {
                Window window = Window.ActiveWindow;
                return window == null ? null : window.ActiveTool as AnimationTool;
            }
        }
    }

    class ToolPathAnimator : Animator {
        ToolPathObject toolPathObj;
        ToolPath toolPath;
        AnimationTool animationTool;

        IList<CutterLocation> locations;
        double totalLength = 0;
        double totalTime = 0;
        double[] timeToLocation;

        public bool WasJustPaused { get; set; }
        DateTime lastTime;

        public ToolPathAnimator(ToolPathObject toolPathObj, AnimationTool animationTool) {
            this.toolPathObj = toolPathObj;
            this.toolPath = toolPathObj.ToolPath;
            this.animationTool = animationTool;

            locations = toolPathObj.CutterLocations;
            timeToLocation = new double[locations.Count];

            timeToLocation[0] = 0;
            for (int i = 0; i < locations.Count - 1; i++) {
                double length = (locations[i + 1].Point - locations[i].Point).Magnitude;
                totalLength += length;
                double rate = locations[i + 1].IsRapid ? toolPath.CuttingParameters.FeedRateRapid : toolPath.CuttingParameters.FeedRate;
                double time = length / rate;
                totalTime += time;
                timeToLocation[i + 1] = totalTime;
            }

            lastTime = DateTime.Now;
            WasJustPaused = false;
        }

        int index;
        double ratio;
        double time = 0;
        public override int Advance(int frame) {
            double deltaTime = (double)(DateTime.Now - lastTime).Ticks / TimeSpan.TicksPerMinute;
            if (WasJustPaused) {
                deltaTime = 0;
                WasJustPaused = false;
            }

            time += deltaTime * SpeedSlider.Value;
            if (time > totalTime)
                return 0;

            if (!TryGetLocationFromTime(time, out  index, out  ratio))
                throw new IndexOutOfRangeException();

            Point location = Interpolation.Interpolate(locations[index].Point, locations[index + 1].Point, ratio);

            Primitive toolPrimitive = toolPath.CuttingTool.GetPrimitive();
            Color color = toolPathObj.Color;
            color = Color.FromArgb(255 - (255 - color.R) / 2, 255 - (255 - color.G) / 2, 255 - (255 - color.B) / 2);

            GraphicStyle style = new GraphicStyle {
                EnableDepthBuffer = true,
                FillColor = color,
                LineColor = color
            };

            animationTool.Rendering = Graphic.Create(style, new[] { toolPrimitive }, null, Matrix.CreateMapping(toolPath.Csys) * Matrix.CreateTranslation(location.Vector));

            AnimateCapsule.AnimationTool.StatusText = string.Format(Resources.AnimationToolMesage, time, totalTime, time / totalTime * 100);

            lastTime = DateTime.Now;
            return frame + 1;
        }

        protected override void OnCompleted(AnimationCompletedEventArgs args) {
            base.OnCompleted(args);
        }

        bool TryGetLocationFromTime(double time, out int index, out double ratio) {
            index = 0;
            ratio = 0;
            if (time < 0 || time > timeToLocation.Last())
                return false;

            for (int i = 1; i < timeToLocation.Length; i++) {
                if (timeToLocation[i] < time)
                    continue;

                index = i - 1;
                ratio = (time - timeToLocation[i - 1]) / (timeToLocation[i] - timeToLocation[i - 1]);
                return true;
            }

            throw new NotImplementedException();
            return false;
        }
    }

}