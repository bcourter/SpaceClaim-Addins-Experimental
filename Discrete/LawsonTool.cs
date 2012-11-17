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
	class LawsonToolButtonCapsule : RibbonButtonCapsule {
		public LawsonToolButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("LawsonTool", Resources.LawsonToolText, null, Resources.LawsonToolHint, parent, buttonSize) {
		}

		protected override void OnUpdate(Command command) {
			Window window = Window.ActiveWindow;
			command.IsEnabled = window != null;
			command.IsChecked = window != null && window.ActiveTool is LawsonTool;
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			Window window = Window.ActiveWindow;
			window.SetTool(new LawsonTool());
		}
	}

	class LawsonTool : Tool {
		//Profile profile;
		double p;
		double q;
		double circleAngle;
		Vector inverseOffset;

		System.Drawing.Point? lastCursorPos;
		double cursorScale = 0.01;

		public LawsonTool()
			: base(InteractionMode.Solid) {

			p = 0.5;
			q = 1;
			circleAngle = Math.PI / 2;
			inverseOffset = Vector.Create(0.5, 0, 0);
		}

		void Reset() {
			//profile = null;

			Rendering = null;
			StatusText = "Get ready for some mayhem.";
		}

		protected override void OnInitialize() {
			Reset();
			Rendering = Graphic.Create(null, null, LawsonToolGraphics.GetGraphic(p, q, circleAngle, inverseOffset));

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

			return true;
		}

		protected override void OnDragMove(System.Drawing.Point cursorPos, Line cursorRay) {
			//if (profile == null)
			//    return;

			//Point point;
			//if (!profilePlane.TryIntersectLine(cursorRay, out point))
			//    return; // plane is side-on

			p += ((double)lastCursorPos.Value.X - cursorPos.X) * cursorScale;
			q += ((double)lastCursorPos.Value.Y - cursorPos.Y) * cursorScale;

			////circleAngle += ((double)lastCursorPos.Value.X - cursorPos.X) * cursorScale;

			//inverseOffset = Vector.Create(
			//    ((double)lastCursorPos.Value.X - cursorPos.X) * cursorScale,
			//    ((double)lastCursorPos.Value.Y - cursorPos.Y) * cursorScale,
			//    inverseOffset.Z
			//);

			lastCursorPos = cursorPos;

			circleAngle = Math.PI / 4;
			Rendering = Graphic.Create(null, null, LawsonToolGraphics.GetGraphic(p, q, circleAngle, inverseOffset));
//			Rendering = Graphic.Create(null, null, Profile.GetGraphic(p, q, circleAngle, true, inverseOffset));
			//Apply(String.Format("p:{0} q:{1} circleAngle:{2} inverseOffset:{3}", p, q, circleAngle, inverseOffset.ToString()));
		}

        //protected override void OnDragEnd(System.Drawing.Point cursorPos, Line cursorRay) {
        //    Complete(); // completes the apply-loop
        ////	Reset();
        //}

        //protected override void OnDragCancel() {
        //    Cancel(); // aborts work done by the last call to Apply
        //    Reset();
        //}

		#endregion


	}

	public class LawsonToolGraphics : CustomWrapper<LawsonToolGraphics> {
		double p;
		double q;
		double circleAngle;
		Vector inverseOffset;

		// creates a wrapper for an existing custom object
		protected LawsonToolGraphics(CustomObject subject)
			: base(subject) {
		}

		// creates a new custom object and a wrapper for it
		protected LawsonToolGraphics(double p, double q, double circleAngle, Vector inverseOffset)
			: base(Window.ActiveWindow.Scene as Part) {
			this.p = p;
			this.q = q;
			this.circleAngle = circleAngle;
			this.inverseOffset = inverseOffset;
		}
		// static Create method follows the API convention and parent should be first argument
		public static LawsonToolGraphics Create(double p, double q, double circleAngle, Vector inverseOffset) {
			var widget = new LawsonToolGraphics(p, q, circleAngle, inverseOffset);
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
			Rendering = GetGraphic(p, q, circleAngle, inverseOffset);
		}

		public static Graphic GetGraphic(double p, double q, double circleAngle, Vector inverseOffset) {
			var graphics = new List<Graphic>();

			int bandCount = 2;
			int iSteps = bandCount * 32; //32
			int jSteps = 34; //34
			double uStep = 2 * Math.PI / iSteps;
			double vStep = 2 * Math.PI / jSteps;
			bool uSwap = false, vSwap = false;
			for (int j = 0; j < jSteps; j++) {
				double v = 2 * Math.PI * j / jSteps;

				for (int i = 0; i < iSteps; i++) {
					double u = 2 * Math.PI * i / iSteps;

					Direction n00, n10, n11, n01;
					Point p00 = Lawson.Evaluate(PointUV.Create(u, v), p, q, circleAngle, inverseOffset, true, out n00);
					Point p10 = Lawson.Evaluate(PointUV.Create(u + uStep, v), p, q, circleAngle, inverseOffset, true, out n10);
					Point p11 = Lawson.Evaluate(PointUV.Create(u + uStep, v + vStep), p, q, circleAngle, inverseOffset, true, out n11);
					Point p01 = Lawson.Evaluate(PointUV.Create(u, v + vStep), p, q, circleAngle, inverseOffset, true, out n01);

					var facetVertices = new List<FacetVertex>();
					facetVertices.Add(new FacetVertex(p00, n00));
					facetVertices.Add(new FacetVertex(p10, n10));
					facetVertices.Add(new FacetVertex(p11, n11));
					facetVertices.Add(new FacetVertex(p01, n01));

					var facets = new List<Facet>();
					facets.Add(new Facet(0, 1, 2));
					facets.Add(new Facet(0, 2, 3));

					HSBColor hsbFill = new HSBColor(Window.ActiveWindow.ActiveLayer.GetColor(null));
					hsbFill.H = (float)(u / 2 / Math.PI * 360);
					hsbFill.A = vSwap ? 127 : 255;

					HSBColor hsbLine = new HSBColor(System.Drawing.Color.MidnightBlue);
					hsbLine.H = (float)(u / 2 / Math.PI * 360);

					var style = new GraphicStyle {
						EnableDepthBuffer = true,
						LineColor = hsbLine.Color,
						LineWidth = 1,
						FillColor = hsbFill.Color
					};

					graphics.Add(Graphic.Create(style, MeshPrimitive.Create(facetVertices, facets)));

					uSwap = !uSwap;
				}
				vSwap = !vSwap;
			}

			return Graphic.Create(null, null, graphics);
		}

		public double P {
			get { return p; }
			set { p = value; }
		}

		public double Q {
			get { return q; }
			set { q = value; }
		}

		public double CircleAngle {
			get { return circleAngle; }
			set { circleAngle = value; }
		}

		public Vector InverseOffset {
			get { return inverseOffset; }
			set { inverseOffset = value; }
		}

	}

	class LawsonCirclesButtonCapsule : RibbonButtonCapsule {
		public LawsonCirclesButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("LawsonCircle", Resources.LawsonCircleCommandText, null, Resources.LawsonCircleCommandHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			const int count = 24;

			double p = 0.5;
			double q = 1;
			double circleAngle = Math.PI / 2;
			Vector inverseOffset = Vector.Create(0.5, 0, 0);

			Part mainPart = Window.ActiveWindow.Scene as Part;

			Point[] baseCirclePoints = new Point[count];
			for (int i = 0; i < count; i++) {
				double u = (double) i / count * 2 * Math.PI;
				Point[] vPoints = new Point[3];
				for (int j = 0; j < 3; j++) {
					vPoints[j] = Lawson.Evaluate(PointUV.Create((double) j / 3 * 2 * Math.PI, u), p, q, circleAngle, inverseOffset, true);
					if (j == 0)
						baseCirclePoints[i] = vPoints[j];
				}

				Circle circle = Circle.CreateThroughPoints(
					Plane.Create(Frame.Create(vPoints[0], Vector.Cross( vPoints[1] - vPoints[0], vPoints[2]-vPoints[0]).Direction)),
					vPoints[0], vPoints[1], vPoints[2]
				);

				DesignCurve.Create(mainPart, CurveSegment.Create(circle));

				//ShapeHelper.CreateTorus(circle.Frame.Origin, circle.Frame.DirZ, circle.Radius * 2, 
			}

			Circle baseCircle = Circle.CreateThroughPoints(
				Plane.Create(Frame.Create(baseCirclePoints[0], Vector.Cross(baseCirclePoints[1] - baseCirclePoints[0], baseCirclePoints[2] - baseCirclePoints[0]).Direction)),
				baseCirclePoints[0], baseCirclePoints[1], baseCirclePoints[2]
			);

			DesignCurve.Create(mainPart, CurveSegment.Create(baseCircle));

		}
	}
}