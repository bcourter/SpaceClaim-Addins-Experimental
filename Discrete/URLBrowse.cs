/*
 * Sample add-in for the SpaceClaim API
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Display;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;
using Discrete.Properties;
using Point = SpaceClaim.Api.V10.Geometry.Point;
using Command = SpaceClaim.Api.V10.Command;

namespace SpaceClaim.AddIn.Discrete {
	class BrowseToolButtonCapsule : RibbonButtonCapsule {
		public BrowseToolButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("BrowseTool", "Browse", null, "Browse", parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			Window window = Window.ActiveWindow;
			window.SetTool(new BrowseTool());
		}
	}

	class BrowseTool : Tool {
		System.Drawing.Point? lastCursorPos;

		public BrowseTool()
			: base(InteractionMode.Solid) {
		}

		void Reset() {
			//profile = null;

			StatusText = "Click on objects for more information.";
		}

		protected override void OnInitialize() {
			Reset();
		}

		protected override IDocObject AdjustSelection(IDocObject docObject) {
#if false
			var desFace = docObject as DesignFace;
			if (desFace != null)
				return desFace.Shape.GetGeometry<Plane>() != null ? desFace : null;

			var custom = docObject as CustomObject;
			if (custom != null)
				return custom.Type == Profile.Type ? custom : null;

			Debug.Fail("Unexpected case");
#endif
			return docObject;
		}

		protected override void OnEnable(bool enable) {
			if (enable)
				Window.PreselectionChanged += Window_PreselectionChanged;
			else
				Window.PreselectionChanged -= Window_PreselectionChanged;
		}

		/*
		 * Using the keyboard or mouse wheel, we can change the preselection without moving
		 * the mouse.  We need access to the buttons and cursor ray so we can make like the
		 * mouse just moved.
		 */

		void Window_PreselectionChanged(object sender, EventArgs e) {
			// preselection can change without the mouse moving (e.g. just created a profile)
			Rendering = null;

			InteractionContext context = InteractionContext;
			Line cursorRay = context.CursorRay;
			if (cursorRay != null)
				OnMouseMove(context.Window.CursorPosition, cursorRay, Control.MouseButtons);
		}


		protected override bool OnMouseMove(System.Drawing.Point cursorPos, Line cursorRay, MouseButtons button) {
#if false

			IDocObject preselection = InteractionContext.Preselection;
			DesignFace desFace = null;

			Profile existingProfile = Profile.GetWrapper(preselection as CustomObject);
			if (existingProfile != null)
				desFace = existingProfile.Face;
			if (desFace == null)
				desFace = preselection as DesignFace;
			if (desFace == null) // selection filtering is not applied if you (pre)select in the tree
				return false;

			Face face = desFace.Shape;
			var plane = face.GetGeometry<Plane>();

			Point point;
			if (!plane.TryIntersectLine(cursorRay, out point))
				return false; // plane is side-on

			Fin fin;
			double offset = GetOffset(face, plane, point, out fin);

			var style = new GraphicStyle
			{
				LineColor = Color.DodgerBlue,
				LineWidth = 3
			};
			Graphic datumGraphic = Graphic.Create(style, CurvePrimitive.Create(fin.Edge));

			if (existingProfile != null)
				Rendering = datumGraphic;
			else {
				style = new GraphicStyle
				{
					LineColor = Color.Gray
				};
				Rendering = Graphic.Create(style, null, Profile.GetGraphic(0.5, 1, Math.PI / 2, Vector.Create(0.5, 0, 0)), datumGraphic);
			}
#endif

			return false; // if we return true, the preselection won't update
		}

		#region Click-Click Notifications

		protected override bool OnClickStart(System.Drawing.Point cursorPos, Line cursorRay) {
			string urlString = "URL";
			IDocObject docObject = InteractionContext.Preselection;
			if (docObject == null)
				return false;

			Document doc = docObject.Document;
			CustomProperty customProp;
			if (doc.CustomProperties.TryGetValue(urlString, out customProp))
				OpenURL(customProp.Value as string);

			return false;
		}

		#endregion

		#region Drag Notifications

		//	Line lastCursorRay;

		protected override bool OnDragStart(System.Drawing.Point cursorPos, Line cursorRay) {
			//			profile = Profile.GetWrapper(InteractionContext.Preselection as CustomObject);
			//if (profile == null)
			//    return;

			//Point? pointOnCustom = InteractionContext.PreselectionPoint;
			//if (pointOnCustom == null) {
			//    profile = null;
			//    return;
			//}

			lastCursorPos = cursorPos;

			StatusText = "Adjust.";

			return false;
		}

		protected override void OnDragMove(System.Drawing.Point cursorPos, Line cursorRay) {
			//if (profile == null)
			//    return;

			//Point point;
			//if (!profilePlane.TryIntersectLine(cursorRay, out point))
			//    return; // plane is side-on

			//p += ((double)lastCursorPos.Value.X - cursorPos.X) * cursorScale;
			//q += ((double)lastCursorPos.Value.Y - cursorPos.Y) * cursorScale;

			////circleAngle += ((double)lastCursorPos.Value.X - cursorPos.X) * cursorScale;

			//inverseOffset = Vector.Create(
			//    ((double)lastCursorPos.Value.X - cursorPos.X) * cursorScale,
			//    ((double)lastCursorPos.Value.Y - cursorPos.Y) * cursorScale,
			//    inverseOffset.Z
			//);

			lastCursorPos = cursorPos;

			//circleAngle = Math.PI / 4;
			//Rendering = Graphic.Create(null, null, BrowseToolGraphics.GetGraphic());
			//			Rendering = Graphic.Create(null, null, Profile.GetGraphic(p, q, circleAngle, true, inverseOffset));
			//Apply(String.Format("BrowseTool"));
		}

        //protected override void OnDragEnd(System.Drawing.Point cursorPos, Line cursorRay) {
        //    Complete(); // completes the apply-loop
        //    //	Reset();
        //}

        //protected override void OnDragCancel() {
        //    Cancel(); // aborts work done by the last call to Apply
        //    Reset();
        //}

		#endregion

		protected void OpenURL(string url) {
			// From http://www.devtoolshed.com/content/launch-url-default-browser-using-c
			try {
				System.Diagnostics.Process.Start(url);
			}

			catch (Exception exception) {
				// System.ComponentModel.Win32Exception is a known exception that occurs when Firefox is default browser.  
				// It actually opens the browser but STILL throws this exception so we can just ignore it.  If not this exception,
				// then attempt to open the URL in IE instead.
				if (exception.GetType().ToString() != "System.ComponentModel.Win32Exception") {
					// sometimes throws exception so we have to just ignore
					// this is a common .NET bug that no one online really has a great reason for so now we just need to try to open
					// the URL using IE if we can.
					try {
						System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo("IExplore.exe", url);
						System.Diagnostics.Process.Start(startInfo);
						startInfo = null;
					}

					catch (Exception) {
						// still nothing we can do so just show the error to the user here.
					}
				}
			}
		}

	}
}