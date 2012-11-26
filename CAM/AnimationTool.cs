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
            TransportControls.Initialize();

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

    static class TransportControls {
        const string jumpToStartCommandName = "AnimationToolJumpToStartButton";
        const string reverseCommandName = "AnimationToolReverseButton";
        const string playCommandName = "AnimationToolPlayButton";
        const string jumpToEndCommandName = "AnimationToolJumpToEndButton";
        const string positionSliderCommandName = "AnimationToolPositionSlider";
        const string positionLabelCommandName = "AnimationToolPositionLabel";
        const string positionStartLabelCommandName = "AnimationToolPositionStartLabel";
        const string positionEndLabelCommandName = "AnimationToolPositionEndLabel";

        const int sliderStart = 0;
        const int sliderEnd = 100;
        const int sliderLength = sliderEnd - sliderStart;

        static ToolPathAnimator animator = null;

        public static void Initialize() {
            Command command;

            command = Command.Create(jumpToStartCommandName);
            command.IsWriteBlock = false;
            command.Image = Resources.TransportBackToBeginning;
            command.Hint = Resources.AnimationToolTransportJumpToStartHint;
            command.Executing += (sender, e) => Position = 0;
            command.Updating += Updating;

            command = Command.Create(reverseCommandName);
            command.IsWriteBlock = false;
            command.Image = Resources.TransportReverse;
            command.Executing += Executing;
            command.Updating += Updating;

            command = Command.Create(playCommandName);
            command.IsWriteBlock = false;
            command.Image = Resources.TransportPlay;
            command.Executing += Executing;
            command.Updating += Updating;

            command = Command.Create(jumpToEndCommandName);
            command.IsWriteBlock = false;
            command.Image = Resources.TransportForwardToEnd;
            command.Hint = Resources.AnimationToolTransportJumpToEndHint;
            command.Executing += (sender, e) => Position = 1;
            command.Updating += Updating;

            command = Command.Create(positionSliderCommandName);
            command.IsWriteBlock = false;
            command.Updating += Updating;
            command.TextChanged += (sender, e) => {
                var c = (Command)sender;
                Position = double.Parse(c.Text) / sliderLength;
                Time = StartTime + Position * (EndTime - StartTime);
            };

            command = Command.Create(positionLabelCommandName);
            command.IsWriteBlock = false;
            command.Updating += Updating;

            command = Command.Create(positionStartLabelCommandName);
            command.IsWriteBlock = false;
            command.Updating += Updating;

            command = Command.Create(positionEndLabelCommandName);
            command.IsWriteBlock = false;
            command.Updating += Updating;

            IsReversed = false;
            Position = 0;
        }

        public static void Reset(ToolPathObject toolPathObj, AnimationTool animationTool) {
            animator = new ToolPathAnimator(toolPathObj, animationTool);
            StartLabelCommand.Text = StartTime.ToString("F3");
            EndLabelCommand.Text = EndTime.ToString("F3");
        }

        public static void Update(double value) {
            var state = (SliderState)SliderCommand.ControlState;
            int v = (int)Math.Round(value * sliderLength);
            SliderCommand.ControlState = SliderState.Create(v, sliderStart, sliderEnd);
            LabelCommand.Text = Time.ToString("F3");
        }

        static void Updating(object sender, EventArgs e) {
            var command = (Command)sender;
            command.IsEnabled = AnimationTool != null && (Animation.IsAnimating || ToolPathObject.SelectedToolPath != null);

            if (command != PlayCommand && command != ReverseCommand)
                return;

            if (Animation.IsAnimating && !Animation.IsPaused) {
                if (IsReversed) {
                    PlayCommand.Image = Resources.TransportPlay;
                    PlayCommand.Hint = Resources.AnimationToolTransportPlayHint;
                    ReverseCommand.Image = Resources.TransportPause;
                    ReverseCommand.Hint = Resources.AnimationToolTransportPauseHint;
                }
                else {
                    PlayCommand.Image = Resources.TransportPause;
                    PlayCommand.Hint = Resources.AnimationToolTransportPauseHint;
                    ReverseCommand.Image = Resources.TransportReverse;
                    ReverseCommand.Hint = Resources.AnimationToolTransportReverseHint;
                }
            }
            else {
                PlayCommand.Image = Resources.TransportPlay;
                PlayCommand.Hint = Resources.AnimationToolTransportPauseHint;
                ReverseCommand.Image = Resources.TransportReverse;
                ReverseCommand.Hint = Resources.AnimationToolTransportReverseHint;
            }
        }

        static void Executing(object sender, EventArgs e) {
            var command = (Command)sender;
            if (command == PlayCommand)
                IsReversed = false;
            if (command == ReverseCommand)
                IsReversed = true;

            if (Animation.IsAnimating) {
                Animation.IsPaused = !Animation.IsPaused;
                animator.WasJustPaused = true;
            }
            else {
                Animate(ToolPathObject.SelectedToolPath);
            }
        }

        static void Animate(ToolPathObject toolPathObj) {
            //animator.Completed += (sender, e) => {
            //    if (e.Result == AnimationResult.Exhausted) {
            //        //if (animator.UndoStepAdded)
            //        //    Application.Undo(1); // rewind to start
            //        //Animation.Start(message, animator, command); // restart
            //    }
            //    //else // animation was canceled or stopped
            //    //  window.ActiveContext.SingleSelection = toolPathObj.Subject;
            //};

            animator.WasJustPaused = true;
            Animation.Start(Resources.AnimatingMessage, animator, Command.AllCommands.Where(c => c.Name.StartsWith("AnimationTool")).ToArray());
        }

        public static AnimationTool AnimationTool {
            get {
                Window window = Window.ActiveWindow;
                return window == null ? null : window.ActiveTool as AnimationTool;
            }
        }

        public static bool IsReversed { get; set; }

        public static double Position {
            get {
                var state = (SliderState)SliderCommand.ControlState;
                return (double)state.Value / sliderLength;
            }
            set {
                Update(value);

                if (animator != null) {
                    animator.Time = value * animator.TotalTime;
                    animator.Advance(-1);
                }
            }
        }

        public static double StartTime {
            get { return 0; }
        }

        public static double EndTime {
            get { return animator == null ? 1 : animator.TotalTime; }
        }

        public static double Time {
            get { return animator == null ? 0 : animator.Time; }
            set {
                if (animator != null)
                    animator.Time = value;
            }
        }

        public static Command PlayCommand {
            get { return Command.GetCommand(playCommandName); }
        }

        public static Command ReverseCommand {
            get { return Command.GetCommand(reverseCommandName); }
        }

        public static Command SliderCommand {
            get { return Command.GetCommand(positionSliderCommandName); }
        }

        public static Command LabelCommand {
            get { return Command.GetCommand(positionLabelCommandName); }
        }

        public static Command StartLabelCommand {
            get { return Command.GetCommand(positionStartLabelCommandName); }
        }

        public static Command EndLabelCommand {
            get { return Command.GetCommand(positionEndLabelCommandName); }
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
            command.TextChanged += (sender, e) => {
                var c = (Command)sender;
                Value = Math.Pow(2, double.Parse(c.Text) / 10 - 1);
            };
            
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
                if (Command == null)
                    return 0;

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
            ResetAnimation();
        }

        protected override IDocObject AdjustSelection(IDocObject docObject) {
            if (docObject as CustomObject == null)
                return null;

            ToolPathObject toolPathObj = ToolPathObject.GetWrapper(docObject as CustomObject);
            if (toolPathObj != null)
                return docObject;

            return null;
        }

        protected override bool OnClickStart(ScreenPoint cursorPos, Line cursorRay) {
            InteractionContext.SingleSelection = InteractionContext.Preselection;
            ResetAnimation(); return false;
        }

        public void ResetAnimation() {
            if (InteractionContext.SingleSelection as CustomObject == null)
                return;

            ToolPathObject toolPathObj = ToolPathObject.GetWrapper(InteractionContext.SingleSelection as CustomObject);
            if (toolPathObj != null)
                TransportControls.Reset(toolPathObj, this);
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

            if (TransportControls.IsReversed)
                deltaTime *= -1;

            time += deltaTime * SpeedSlider.Value;
            time = Math.Max(time, 0);
            time = Math.Min(time, totalTime);

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
            animationTool.StatusText = string.Format(Resources.AnimationToolMesage, time, totalTime, time / totalTime * 100);
            TransportControls.Update(time / totalTime);

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

        public double Time {
            get { return time; }
            set { time = value; }
        }

        public double TotalTime {
            get { return totalTime; }
        }

        public double TotalLength {
            get { return totalLength; }
        }
    }

}