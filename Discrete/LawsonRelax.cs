using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
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
	abstract class LawsonPropertiesButtonCapsule : RibbonButtonCapsule {
		protected static double angleForce = 0.01;
		protected static double vForce = 0.02;
		protected static double uAngleForce = 0.006;
		protected static double uTouchForce = 0.000;
		protected static double averageVForce = 0.1;
		protected static double averageTabAngleForce = 0.1;

		protected static Lawson lawson = null;

		public LawsonPropertiesButtonCapsule(string name, string text, System.Drawing.Image image, string hint, RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base(name, text, image, hint, parent, buttonSize) {

			Values[Resources.LawsonTabAngleForce] = new RibbonCommandValue(angleForce);
			Values[Resources.LawsonVAlignmentForce] = new RibbonCommandValue(vForce);
			Values[Resources.LawsonUAngleForce] = new RibbonCommandValue(uAngleForce);
			Values[Resources.LawsonUTouchForce] = new RibbonCommandValue(uTouchForce);
			Values[Resources.LawsonVAverageForce] = new RibbonCommandValue(averageVForce);
			Values[Resources.LawsonTabAngleAverageForce] = new RibbonCommandValue(averageTabAngleForce);

			if (lawson == null)
				lawson = new Lawson(angleForce, vForce, uAngleForce, uTouchForce, averageVForce, averageTabAngleForce);
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			UpdateProperties();
		}

		public void UpdateProperties() {
			angleForce = Values[Resources.LawsonTabAngleForce].Value;
			vForce = Values[Resources.LawsonVAlignmentForce].Value;
			uAngleForce = Values[Resources.LawsonUAngleForce].Value;
			uTouchForce = Values[Resources.LawsonUTouchForce].Value;
			averageVForce = Values[Resources.LawsonVAverageForce].Value;
			averageTabAngleForce = Values[Resources.LawsonTabAngleAverageForce].Value;

			lawson.AngleForce = angleForce;
			lawson.VForce = vForce;
			lawson.UAngleForce = uAngleForce;
			lawson.UTouchForce = uTouchForce;
			lawson.AverageVForce = averageVForce;
			lawson.AverageTabAngleForce = averageTabAngleForce;
		}
	}

	class LawsonRelaxButtonCapsule : LawsonPropertiesButtonCapsule {
		public LawsonRelaxButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("LawsonRelax", Resources.LawsonRelaxText, null, Resources.LawsonRelaxHint, parent, buttonSize) {
		}

		protected override void OnUpdate(Command command) {
			Window window = Window.ActiveWindow;
			command.IsEnabled = window != null;
			command.IsChecked = window != null && window.ActiveTool is LawsonRelax;
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			base.OnExecute(command, context, buttonRect);
			lawson = new Lawson(angleForce, vForce, uAngleForce, uTouchForce, averageVForce, averageTabAngleForce);
			Window window = Window.ActiveWindow;
			window.SetTool(new LawsonRelax(lawson, this));
		}
	}

	class CreateLawsonButtonCapsule : LawsonPropertiesButtonCapsule {
		public CreateLawsonButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("Lawson", Resources.LawsonCreateCommandText, null, Resources.LawsonCreateCommandHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			Window activeWindow = Window.ActiveWindow;
			Part part = activeWindow.Scene as Part;

			//	lawson.CreateSolid(part);
			lawson.CreateCircularTabsOnly(part);

			activeWindow.SetProjection(Matrix.CreateMapping(Frame.Create(Point.Origin, -Direction.DirY, Direction.DirX)), true, true);
			activeWindow.ZoomExtents();
			activeWindow.InteractionMode = InteractionMode.Solid;

			Command.GetCommand("TangentEdge").IsEnabled = true;
			Command.GetCommand("SolidEdges").IsEnabled = true;
			Command.GetCommand("TangentEdge").IsChecked = false;
			Command.GetCommand("SolidEdges").IsChecked = false;
		}
	}

	class LawsonRelax : Tool {
		Lawson lawson;
		LawsonRelaxButtonCapsule buttonCapsule;

		System.Drawing.Point? lastCursorPos;
		//		double cursorScale = 0.01;

		public LawsonRelax(Lawson lawson, LawsonRelaxButtonCapsule buttonCapsule)
			: base(InteractionMode.Solid) {

			this.lawson = lawson;
			this.buttonCapsule = buttonCapsule;
		}

		System.Threading.Thread thread;
		System.Threading.ManualResetEvent exitThreadEvent = new System.Threading.ManualResetEvent(false);
		bool isThreadRunning = false;

		private void StartThread() {
			isThreadRunning = true;
			thread = new System.Threading.Thread((System.Threading.ThreadStart) delegate {
				while (isThreadRunning) {
					buttonCapsule.UpdateProperties();
					for (int i = 0; i < 20; i++)
						lawson.Iterate();

					Rendering = Graphic.Create(null, null, LawsonRelaxGraphics.GetGraphic(lawson));
					StatusText = string.Format("Iteration {0}  Error {1}  Max {2}", lawson.Iteration, lawson.CumulativeError, lawson.MaxError);
				}
			});
			thread.Start();
		}

		private void StartThreadImages() {
			isThreadRunning = true;
			Part mainPart = Window.ActiveWindow.Scene as Part;
			thread = new System.Threading.Thread((System.Threading.ThreadStart) delegate {
				while (isThreadRunning) {
					buttonCapsule.UpdateProperties();
					//	Window.ActiveWindow.SetProjection(Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirY), 2 * Math.PI / 300) * Window.ActiveWindow.Projection, false, false);


					WriteBlock.ExecuteTask("Save JPEG",
						delegate {
							lawson.CreateCircularTabsOnly(mainPart);
						
							Window.ActiveWindow.Export(WindowExportFormat.Jpeg, System.IO.Path.GetTempPath() + lawson.Iteration.ToString("TumbleFrame{0000.}.jpg"));
					
							foreach (Component component in mainPart.Components)
								component.Delete();
						}
					);

					lawson.Iterate();

		//			Rendering = Graphic.Create(null, null, LawsonRelaxGraphics.GetGraphic(lawson));
					StatusText = string.Format("Iteration {0}  Error {1}  Max {2}", lawson.Iteration, lawson.CumulativeError, lawson.MaxError);

				}
			});
			thread.Start();
		}

		private void StopThread() {
			if (isThreadRunning) {
				isThreadRunning = false;
				try {
					exitThreadEvent.Set();
					thread.Join();		// wait for thread to exit
					exitThreadEvent.Reset();
				}
				finally {
					thread = null;
				}
			}
		}

		protected override bool OnKeyUp(Keys modifiers, Keys code) {
			if (code == Keys.Space) {
				if (thread != null && thread.ThreadState == System.Threading.ThreadState.Running) {
					StopThread();
				}
				else {
										StartThread();
					//StartThreadImages();
				}
				return true;
			}

			return base.OnKeyUp(modifiers, code);
		}

		void Reset() {
			StopThread();
			Rendering = null;
			StatusText = "Get ready for some mayhem.";


			DesignBody desBody = ShapeHelper.CreateCircle(Frame.World, 11, Window.ActiveWindow.Scene as Part);
			desBody.SetVisibility(null, false);

			lawson.Reset();
		}

		protected override void OnInitialize() {
			Reset();

			Rendering = Graphic.Create(null, null, LawsonRelaxGraphics.GetGraphic(lawson));

			//	SelectionTypes = new[] { typeof(DesignFace), typeof(CustomObject) }; bzc
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
			return null;
		}

		protected override void OnEnable(bool enable) {
			if (enable)
				Window.PreselectionChanged += Window_PreselectionChanged;
			else {
				Window.PreselectionChanged -= Window_PreselectionChanged;
				StopThread();
			}
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
#if false
			var desFace = InteractionContext.Preselection as DesignFace;
			if (desFace != null) {
				Face face = desFace.Shape;
				var plane = face.GetGeometry<Plane>();

				Point point;
				if (!plane.TryIntersectLine(cursorRay, out point))
					return false; // plane is side-on
#endif
			//	WriteBlock.ExecuteTask("Create Profile", () => Profile.Create(p, q, circleAngle,  inverseOffset));


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

			lastCursorPos = cursorPos;

			//for (int i = 0; i < 20; i++)
			//    lawson.Iterate();

			//Rendering = Graphic.Create(null, null, LawsonRelaxGraphics.GetGraphic(lawson));
			//Apply(String.Format("Iteration {0}", lawson.Iteration));
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

	}

	public class LawsonRelaxGraphics : CustomWrapper<LawsonToolGraphics> {
		Lawson lawson;

		// creates a wrapper for an existing custom object
		protected LawsonRelaxGraphics(CustomObject subject)
			: base(subject) {
		}

		// creates a new custom object and a wrapper for it
		protected LawsonRelaxGraphics(Lawson lawson)
			: base(Window.ActiveWindow.Scene as Part) {
			this.lawson = lawson;
		}

		// static Create method follows the API convention and parent should be first argument
		public static LawsonRelaxGraphics Create(Lawson lawson) {
			var widget = new LawsonRelaxGraphics(lawson);
			widget.Initialize();
			return widget;
		}

		/*
		 * Automatic update of a custom object happens in two stages.
		 * 
		 *  (1)	IsAlive is called to see if the custom object should continue to exist.
		 *  
		 *		If a custom object requires references to other doc objects for its definition,
		 *		it can return false if any of these objects no longer exists.
		 *		
		 *		The data for a custom wrapper is stored in the custom object, and references to
		 *		doc objects are stored as monikers.  When a custom wrapper is obtained, a moniker
		 *		to a deleted object will resolve as a null reference.  If the custom wrapper already
		 *		held the reference, and the doc object has since been deleted, then the reference
		 *		will not be null, but IsDeleted will be true.  Therefore both cases must be checked.
		 *		
		 *		In our example, if the design face no longer exists, or the design face is no longer
		 *		planar, then the custom object should be deleted.
		 *		
		 *		If the custom object does not depend on other objects for its continued existence,
		 *		then there is no need to override IsAlive.  The default implementation returns true.
		 *		
		 *		In some cases, you might decide that rather than being deleted, the custom object
		 *		should become 'invalid' and render itself so as to indicate this to the user.  You
		 *		might provide a command to repair the custom object.  In this case, there is no need
		 *		to override IsAlive, but you would override Update so that the Rendering indicates
		 *		the invalid state, perhaps using a special color.
		 *		
		 *  (2)	Update is called to potentially update the custom object.
		 *  
		 *		A custom object needs updating if it has any information which is evaluated from
		 *		its 'determinants', i.e. those objects on which its evaluated state depends.
		 *		
		 *		The Rendering is an example of evaluated data.
		 *		
		 *		You should call IsChanged with the determinants.  Determinants are often references
		 *		to other doc objects, but they do not have to be.  Determinants can be obtained by
		 *		traversals, e.g. the Parent might be a determinant.
		 *		
		 *		If IsChanged returns true, you should update evaluated data, e.g. the Rendering.
		 *		You must not change the definition of the custom object during update; you can only
		 *		changed evaluated data.  For example, you cannot call Commit during Update.
		 *		
		 *		The custom object itself is implicitly a determinant.  You do not need to supply
		 *		'this' as one of the determinants with IsChanged.  This is useful, since if the rendering
		 *		depends on data in the custom object itself (which is likely), the fact that the custom
		 *		object was changed when Commit was called after the data was changed means that IsChanged
		 *		will return true and you will then proceed to update the Rendering.
		 *		
		 *		Internally, the state of update of all the determinants, including the custom object itself,
		 *		is recorded each time IsChanged is called.  Each time IsChanged is called, if this combined
		 *		update state has changed, IsChanged returns true.  This can happen because objects have
		 *		been modified, or an undo/redo has occurred, or a referenced document has been replaced.
		 */

		protected override bool IsAlive {
			get { return true; }
		}

		protected override bool Update() {
			//if (IsChanged(desFace))
			//    UpdateRendering();
            return false;
		}

		void UpdateRendering() {
			Rendering = GetGraphic(lawson);
		}

		public static Graphic GetGraphic(Lawson lawson) {
			return lawson.GetGraphic();
		}

	}
}