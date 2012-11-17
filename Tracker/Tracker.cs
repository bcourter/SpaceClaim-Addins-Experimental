using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Linq;
using SpaceClaim.Api.V8;
using SpaceClaim.Api.V8.Extensibility;
using SpaceClaim.Api.V8.Geometry;
using SpaceClaim.Api.V8.Modeler;
using SpaceClaim.Api.V8.Display;
using SpaceClaim.AddInLibrary;
using SpaceClaim.Svg;
using SpaceClaim.AddIn.Tracker.Properties;
using Color = System.Drawing.Color;
using Application = SpaceClaim.Api.V8.Application;

namespace SpaceClaim.AddIn.Tracker {
	public abstract class TrackerPropertiesButtonCapsule : RibbonButtonCapsule {
		protected Window activeWindow;
		protected Layer referenceLayer;
		protected Part part;
		protected static ControlForm controlForm;

		public TrackerPropertiesButtonCapsule(string name, string text, System.Drawing.Image image, string hint, RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base(name, text, image, hint, parent, buttonSize) {

			//		Values.Add(Resources.DashSizeText, new RibbonCommandValue(dashMinSize, null));
		}

		protected override void OnExecute(Command command, System.Drawing.Rectangle buttonRect) {
			activeWindow = Window.ActiveWindow;
			part = activeWindow.ActiveContext.Context as Part;
			referenceLayer = NoteHelper.CreateOrGetLayer(activeWindow.ActiveContext.Context.Document, "Layout", System.Drawing.Color.Black);
		}
	}

	public class TrackerLaunchButtonCapsule : TrackerPropertiesButtonCapsule {
		public TrackerLaunchButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("Launch", Resources.LaunchCommandText, null, Resources.LaunchCommandHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, System.Drawing.Rectangle buttonRect) {
			base.OnExecute(command, buttonRect);

			controlForm = new ControlForm();
			controlForm.Show();
		}
	}

	public class TrackerGetEnvironmentButtonCapsule : TrackerPropertiesButtonCapsule {
		public TrackerGetEnvironmentButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("GetEnvironment", Resources.GetEnvironmentCommandText, null, Resources.GetEnvironmentCommandHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, System.Drawing.Rectangle buttonRect) {
			base.OnExecute(command, buttonRect);

			foreach (Point point in controlForm.CalibrationPoints) {
				DesignBody desBody = ShapeHelper.CreateSphere(point, 0.1, part);
				desBody.Layer = referenceLayer;
			}

			foreach (VideoForm videoForm in controlForm.VideoForms) {
				Point point = videoForm.TrackingCamera.GetLine(PointUV.Origin).Evaluate(0.2).Point;
				DesignBody desBody = ShapeHelper.CreateSphere(point, 0.1, part);
				desBody.Layer = referenceLayer;

				foreach (PointUV pointUV in videoForm.TrackingCamera.CalibrationPoints) {
					point = videoForm.TrackingCamera.GetLine(pointUV).Evaluate(0.2).Point;
					//desBody = ShapeHelper.CreateSphere(point, 0.1, part);
					//desBody.Layer = referenceLayer;
					var desCurve = DesignCurve.Create(part, CurveSegment.Create(PointCurve.Create(point)));
					desCurve.Layer = referenceLayer;
				}


				for (int i = 0; i < videoForm.Size.Width; i += videoForm.Size.Width / 12) {
					for (int j = 0; j < videoForm.Size.Height; j += videoForm.Size.Height / 12) {
						Line line = videoForm.TrackingCamera.GetLine(PointUV.Create(i, j));
						var curveSegment = CurveSegment.Create(line, Interval.Create(-3, 6));
						var desCurve = DesignCurve.Create(part, curveSegment);
						desCurve.Layer = referenceLayer;
					}
				}
			}
		}
	}

	public class TrackerGetRayButtonCapsule : TrackerPropertiesButtonCapsule {
		public TrackerGetRayButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("GetRay", Resources.GetRayCommandText, null, Resources.GetRayCommandHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, System.Drawing.Rectangle buttonRect) {
			base.OnExecute(command, buttonRect);

			foreach (VideoForm videoForm in controlForm.VideoForms) {
				Line line = videoForm.TrackingCamera.GetLine();
				var curveSegment = CurveSegment.Create(line, Interval.Create(-10, 10));
				var desCurve = DesignCurve.Create(part, curveSegment);
				desCurve.Layer = referenceLayer;

				Point point = line.Origin;
				DesignBody desBody = ShapeHelper.CreateSphere(point, 0.1, part);
				desBody.Layer = referenceLayer;
			}
		}
	}
}