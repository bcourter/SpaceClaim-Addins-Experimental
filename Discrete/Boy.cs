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
	class BoyToolButtonCapsule : RibbonButtonCapsule {
		public BoyToolButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("BoyToolX", Resources.BoyToolText, null, Resources.BoyToolHint, parent, buttonSize) {
		}

		protected override void OnUpdate(Command command) {
			Window window = Window.ActiveWindow;
			command.IsEnabled = window != null;
			command.IsChecked = window != null && window.ActiveTool is BoyTool;
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			Window window = Window.ActiveWindow;
			window.SetTool(new BoyTool());
		}
	}

	class BoyTool : Tool {
		System.Drawing.Point? lastCursorPos;

		public BoyTool()
			: base(InteractionMode.Solid) {
		}

		void Reset() {
			//profile = null;

			Rendering = null;
			StatusText = "Get ready for some mayhem.";
		}

		protected override void OnInitialize() {
			Reset();
			Rendering = Graphic.Create(null, null, BoyToolGraphics.GetGraphic());

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
			Rendering = Graphic.Create(null, null, BoyToolGraphics.GetGraphic());
			//			Rendering = Graphic.Create(null, null, Profile.GetGraphic(p, q, circleAngle, true, inverseOffset));
			//Apply(String.Format("BoyTool"));
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

	public class BoyToolGraphics : CustomWrapper<LawsonToolGraphics> {
		// creates a wrapper for an existing custom object
		protected BoyToolGraphics(CustomObject subject)
			: base(subject) {
		}

		// creates a new custom object and a wrapper for it
		protected BoyToolGraphics()
			: base(Window.ActiveWindow.Scene as Part) {
		}
		// static Create method follows the API convention and parent should be first argument
		public static BoyToolGraphics Create() {
			var widget = new BoyToolGraphics();
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
			Rendering = GetGraphic();
		}

		public static Graphic GetGraphic() {
			var graphics = new List<Graphic>();

			int iSteps = 64; //32
			int jSteps = 64; //34
			double uStep = (double) 1 / iSteps;
			double vStep = (double) 2 * Math.PI / jSteps;
			bool uSwap = false, vSwap = false;
			for (int i = 0; i < iSteps; i++) {
				double u = (double) i * uStep;

				for (int j = 0; j < jSteps; j++) {
					double v = (double) j * vStep;

					Direction n00, n10, n11, n01;
					Point p00 = BoySurface.Evaluate(Complex.CreatePolar(u, v), out n00);
					Point p10 = BoySurface.Evaluate(Complex.CreatePolar(u + uStep, v), out n10);
					Point p11 = BoySurface.Evaluate(Complex.CreatePolar(u + uStep, v + vStep), out n11);
					Point p01 = BoySurface.Evaluate(Complex.CreatePolar(u, v + vStep), out n01);

					var facetVertices = new List<FacetVertex>();
					facetVertices.Add(new FacetVertex(p00, n00));
					facetVertices.Add(new FacetVertex(p10, n10));
					facetVertices.Add(new FacetVertex(p11, n11));
					facetVertices.Add(new FacetVertex(p01, n01));

					var facets = new List<Facet>();
					facets.Add(new Facet(0, 1, 2));
					facets.Add(new Facet(0, 2, 3));

					HSBColor hsbFill = new HSBColor(Window.ActiveWindow.ActiveLayer.GetColor(null));
					hsbFill.H = (float) (u / 2 / Math.PI * 360);
					hsbFill.A = vSwap ? 127 : 255;

					HSBColor hsbLine = new HSBColor(System.Drawing.Color.MidnightBlue);
					hsbLine.H = (float) (u / 2 / Math.PI * 360);

					var style = new GraphicStyle {
						EnableDepthBuffer = true,
						LineColor = hsbLine.Color,
						LineWidth = 1,
						FillColor = hsbFill.Color
					};

					graphics.Add(Graphic.Create(style, MeshPrimitive.Create(facetVertices, facets)));

					vSwap = !vSwap;
				}
				uSwap = !uSwap;
			}

			return Graphic.Create(null, null, graphics);
		}

	}

	public class BoySurface {
		// http://en.wikipedia.org/wiki/Boy%27s_surface
		public static Point Evaluate(Complex z) {
			Complex denom = z.Pow(6) + Math.Sqrt(5) * z.Pow(3) - 1;
			double g1 = -1.5 * (z * (1 - z.Pow(4)) / denom).Im;
			double g2 = -1.5 * (z * (1 + z.Pow(4)) / denom).Re;
			double g3 = ((1 + z.Pow(6)) / denom).Im - 0.5;
			double g = g1 * g1 + g2 * g2 + g3 * g3;

			return Point.Create(g1 / g, g2 / g, g3 / g);
		}

		public static Point Evaluate(Complex z, out Direction n) {
			double delta = 0.00001;

			n = Direction.Cross(
				(Evaluate(z + delta) - Evaluate(z - delta)).Direction,
				(Evaluate(z + delta * Complex.I) - Evaluate(z - delta * Complex.I)).Direction
			);

			return Evaluate(z);
		}
	}
}