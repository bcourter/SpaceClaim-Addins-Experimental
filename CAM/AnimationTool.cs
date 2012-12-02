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

        static AnimationTool animationTool;

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
                AnimationTool.Time = StartTime + Position * (EndTime - StartTime);
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

        public static void Reset(AnimationTool animationTool) {
            TransportControls.animationTool = animationTool;
            StartLabelCommand.Text = StartTime.ToString("F3");
            EndLabelCommand.Text = EndTime.ToString("F3");
            Update(animationTool.Time / animationTool.TotalTime);
        }

        public static void Update(double value) {
            var state = (SliderState)SliderCommand.ControlState;
            int v = (int)Math.Round(value * sliderLength);
            v = Math.Max(v, sliderStart);
            v = Math.Min(v, sliderEnd);
            SliderCommand.ControlState = SliderState.Create(v, sliderStart, sliderEnd);
            LabelCommand.Text = AnimationTool.Time.ToString("F3");
        }

        static void Updating(object sender, EventArgs e) {
            var command = (Command)sender;
            command.IsEnabled = AnimationTool != null && IsEnabled && (Animation.IsAnimating || ToolPathObject.SelectedToolPath != null);

            if (command != PlayCommand && command != ReverseCommand)
                return;

            if (Animation.IsAnimating /* && !Animation.IsPaused */ ) {
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

            if (Animation.IsAnimating)
                Animation.Stop();
            else
                AnimationTool.Animate();
        }

        public static AnimationTool AnimationTool {
            get {
                return animationTool;

                //Window window = Window.ActiveWindow;
                // return window == null ? null : window.ActiveTool as AnimationTool;
            }
        }

        public static bool IsReversed { get; set; }

        public static bool IsEnabled { get; set; }

        public static double Position {
            get {
                var state = (SliderState)SliderCommand.ControlState;
                return (double)state.Value / sliderLength;
            }
            set {
                if (AnimationTool == null)
                    return;

                Update(value);
                AnimationTool.Time = value * AnimationTool.TotalTime;
            }

        }

        public static double StartTime {
            get { return 0; }
        }

        public static double EndTime {
            get { return AnimationTool.TotalTime; }
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
        const int maxValue = 60;

        public static void Initialize() {
            Command command = Command.Create(commandName);
            command.IsWriteBlock = false;
            command.TextChanged += (sender, e) => {
                var c = (Command)sender;
                Value = Math.Pow(2, double.Parse(c.Text) / 10 - 2);
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
                return Math.Pow(2, (double)state.Value / 10 - 2);
            }
            set {
                var state = (SliderState)Command.ControlState;
                int v = (int)Math.Round((Math.Log(value) / Math.Log(2) + 2) * 10);
                Command.ControlState = SliderState.Create(v, minValue, maxValue);
                LabelCommand.Text = string.Format(Resources.AnimationToolSpeedLabel, value);
            }
        }
    }

    class AnimationTool : Tool {
        IList<CutterLocation> locations;
        double totalLength = 0;
        double totalTime = 0;
        double[] timeToLocation;

        ToolPathObject toolPathObj;
        ToolPath toolPath;

        int index;
        double ratio;
        double time = 0;

        public AnimationTool()
            : base(InteractionMode.Solid) {
        }

        public override string OptionsXml {
            get { return Resources.AnimationToolOptions; }
        }

        protected override void OnInitialize() {
            ResetFromSelection();
        }

        protected override IDocObject AdjustSelection(IDocObject docObject) {
            if (docObject as ICustomObject == null)
                return null;

            ToolPathObject toolPathObj = ToolPathObject.GetWrapper((docObject as ICustomObject).Master);
            if (toolPathObj != null)
                return docObject;

            return null;
        }

        protected override void OnEnable(bool enable) {
            if (enable) {
                Window.SelectionChanged += Window_SelectionChanged;
                toolPathObj.Changed += Window_SelectionChanged;
            }
            else {
                Window.SelectionChanged -= Window_SelectionChanged;
                toolPathObj.Changed -= Window_SelectionChanged;
            }
        }

        void Window_SelectionChanged(object sender, EventArgs e) {
            ResetFromSelection();
        }

        protected override bool OnClickStart(ScreenPoint cursorPos, Line cursorRay) {
            InteractionContext.SingleSelection = InteractionContext.Preselection;
            return false;
        }

        public void ResetFromSelection() {
            if (InteractionContext.SingleSelection as ICustomObject == null)
                return;

            toolPathObj = ToolPathObject.GetWrapper((InteractionContext.SingleSelection as ICustomObject).Master);
            if (toolPathObj == null) {
                TransportControls.IsEnabled = false;
                return;
            }

            toolPath = toolPathObj.ToolPath;
            locations = toolPathObj.CutterLocations;
            timeToLocation = new double[locations.Count];

            time = 0;
            totalTime = 0;
            timeToLocation[0] = 0;
            for (int i = 0; i < locations.Count - 1; i++) {
                double length = (locations[i + 1].Point - locations[i].Point).Magnitude;
                totalLength += length;
                double rate = locations[i + 1].IsRapid ? toolPath.CuttingParameters.FeedRateRapid : toolPath.CuttingParameters.FeedRate;
                double dtime = length / rate;
                totalTime += dtime;
                timeToLocation[i + 1] = totalTime;
            }
            timeToLocation = timeToLocation.Distinct().ToArray();

            SetGraphics();
            TransportControls.IsEnabled = true;
            TransportControls.Reset(this);
        }

        public void SetGraphics() {
            double time = Math.Max(this.time, 0);
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

            Rendering = Graphic.Create(style, new[] { toolPrimitive }, null, Matrix.CreateMapping(toolPath.Csys) * Matrix.CreateTranslation(location.Vector));
            StatusText = string.Format(Resources.AnimationToolMesage, time, totalTime, time / totalTime * 100);
        }

        public void Animate() {
            Animation.Start(Resources.AnimatingMessage, new ToolPathAnimator(this), Command.AllCommands.Where(c => c.Name.StartsWith("AnimationTool")).ToArray());
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
        }

        public double Time {
            get { return time; }
            set {
                time = value;
                SetGraphics();
                TransportControls.Update(time / totalTime);

                //   if (animator != null)
                //       animator.Advance(-1);
            }
        }

        public double TotalTime { get { return totalTime; } }

        public double TotalLength { get { return totalLength; } }
    }

    class ToolPathAnimator : Animator {
        AnimationTool animationTool;
        DateTime lastTime;

        public ToolPathAnimator(AnimationTool animationTool) {
            this.animationTool = animationTool;
            lastTime = DateTime.Now;
        }

        public override int Advance(int frame) {
            double deltaTime = (double)(DateTime.Now - lastTime).Ticks / TimeSpan.TicksPerMinute;

            if (TransportControls.IsReversed)
                deltaTime *= -1;

            animationTool.Time += deltaTime * SpeedSlider.Value;

            lastTime = DateTime.Now;
            return frame + 1;
        }
    }

}