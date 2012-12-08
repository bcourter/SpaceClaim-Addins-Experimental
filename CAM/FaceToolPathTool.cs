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
        FaceToolPathObject prototypeObj;

        public FaceToolPathTool()
            : base(InteractionMode.Solid) {
        }

        public override string OptionsXml {
            get { return Resources.FaceToolPathToolOptions; }
        }

        protected override void OnInitialize() {
            Rendering = null;
            SelectionTypes = new[] { typeof(DesignFace), typeof(CustomObject) };
            StatusText = Resources.FaceToolPathToolStatusText;

            var tool = BallMill.StandardSizes.Values.ToArray()[4];
            var parameters = new CuttingParameters(tool.Radius, 10, tool.Radius * 2);
            FaceToolPathObject.DefaultToolPath = new UVFacingToolPath(null, tool, parameters);
            FaceToolPathObject.DefaultColor = ToolPathColorProperty.ColorList[0];
            FaceToolPathObject.DefaultStrategy = 0;
        }

        #region Command notifications

        protected override bool OnClickStart(ScreenPoint cursorPos, Line cursorRay) {
            InteractionContext.SingleSelection = InteractionContext.Preselection;
            return false;
        }

        protected override void OnEnable(bool enable) {
            if (enable) {
                Window.SelectionChanged += Window_SelectionChanged;
                HandleSelectionChanged();
            }
            else {
                Window.SelectionChanged -= Window_SelectionChanged;
                if (prototypeObj != null && !prototypeObj.IsDeleted)
                    WriteBlock.ExecuteTask("Delete path", () => prototypeObj.Delete());

            }
        }

        void Window_SelectionChanged(object sender, EventArgs e) {
            HandleSelectionChanged();
        }

        public void HandleSelectionChanged() {
            IDocObject iDocObj = InteractionContext.SingleSelection;
            if (iDocObj != null && FaceToolPathObject.SelectedToolPath != null) {
                return;
            }

            UpdatePrototypeObject();
            if (iDocObj == null)
                return;

            var iDesFace = iDocObj as IDesignFace;
            if (iDesFace != null) {
                prototypeObj.IDesFace = iDesFace;
                prototypeObj = null;
                return;
            }
        }

        private void UpdatePrototypeObject() {
            if (prototypeObj == null || prototypeObj.IsDeleted)
                WriteBlock.ExecuteTask("Create preselection", () => prototypeObj = FaceToolPathObject.DefaultToolPathObject);

            InteractionContext.SingleSelection = prototypeObj.Subject;
        }

        protected override IDocObject AdjustSelection(IDocObject docObject) {
            var desFace = docObject as DesignFace;
            if (desFace != null)
                return desFace;

            var custom = docObject as CustomObject;
            if (custom != null)
                return custom.Type == FaceToolPathObject.Type ? custom : null;

            Debug.Fail("Unexpected case");
            return null;
        }

        #endregion

    }

}
