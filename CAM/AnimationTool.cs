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
            //       StrategyComboBox.Initialize();

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

    class AnimationTool : Tool {
        public AnimationTool()
            : base(InteractionMode.Solid) {
        }

        protected override void OnInitialize() {
            var layout = new ToolGuideLayout();
            layout.AddButton(Command.GetCommand(AnimateCapsule.CommandName));
            SetToolGuideLayout(layout);
        }

        protected override IDocObject AdjustSelection(IDocObject docObject) {
            return docObject as IComponent ?? docObject.GetAncestor<IComponent>();
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
            if (Animation.IsAnimating)
                Animation.IsPaused = !Animation.IsPaused;
            else
                Animate(command, ToolPathObject.SelectedToolPath);
        }

        static void Animate(Command command, ToolPathObject toolPathObj) {

            Window window = Window.ActiveWindow;
            window.ActiveContext.SingleSelection = null; // clear selection

            var animator = new ToolPathAnimator(toolPathObj, AnimationTool);

            animator.Completed += (sender, e) => {
                if (e.Result == AnimationResult.Exhausted) {
                    //if (animator.UndoStepAdded)
                    //    Application.Undo(1); // rewind to start
                    //Animation.Start(message, animator, command); // restart
                }
                else // animation was canceled or stopped
                    window.ActiveContext.SingleSelection = toolPathObj.Subject;
            };

            string message = string.Format(Resources.AnimationToolMesage, 0, toolPathObj.CutterLocations.Count);
            Animation.Start(message, animator, command);
        }

        static AnimationTool AnimationTool {
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

        double position = 0;
        IList<CutterLocation> locations;
        double totalLength = 0;
        double totalTime = 0;
        double[] timeToLocation;

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
        }

        public override int Advance(int frame) {
            position += 1;

            Primitive toolPrimitive = toolPath.CuttingTool.GetPrimitive();
            Color color = toolPathObj.Color;
            color = Color.FromArgb(255 - (255 - color.R) / 2, 255 - (255 - color.G) / 2, 255 - (255 - color.B) / 2);

            GraphicStyle style = new GraphicStyle {
                EnableDepthBuffer = true,
                FillColor = color,
                LineColor = color
            };

            Point location = toolPathObj.CutterLocations[(int)position].Point;

            animationTool.Rendering = Graphic.Create(style, new[] { toolPrimitive }, null, Matrix.CreateMapping(toolPath.Csys) * Matrix.CreateTranslation(location.Vector));

            return (int)(toolPathObj.CutterLocations.Count - position);
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