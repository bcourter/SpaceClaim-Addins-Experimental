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
using Animator.Properties;
using SpaceClaim.AddInLibrary;
using Point = SpaceClaim.Api.V10.Geometry.Point;
using ScreenPoint = System.Drawing.Point;

namespace SpaceClaim.AddIn.Animator {
	class OscillatorToolCapsule : CommandCapsule {
		public const string CommandName = "Animator.OscillatorTool";


		public OscillatorToolCapsule()
			: base(CommandName, Resources.OscillatorToolText, null, Resources.OscillatorToolHint) {
		}

		protected override void OnUpdate(Command command) {
			Window window = Window.ActiveWindow;
			command.IsEnabled = window != null;
			command.IsChecked = window != null && window.ActiveTool is OscillatorTool;
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			Window window = Window.ActiveWindow;
			window.SetTool(new OscillatorTool());
		}
	}

	class OscillatorTool : Tool {
		List<OscillatorPart> oscillatorParts;
		IDesignFace oscillatorIDesignFace;
		PointUV oscillatorPointUV;

		public OscillatorTool()
			: base(InteractionMode.Solid) {
		}

		void Reset() {
			oscillatorParts = new List<OscillatorPart>();
			oscillatorIDesignFace = null;

			Rendering = null;
			SelectionTypes = new[] { typeof(DesignFace), typeof(CustomObject) };
			StatusText = "Place a new oscillator handle.";
		}

		protected override void OnInitialize() {
			Reset();
		}

		protected override IDocObject AdjustSelection(IDocObject docObject) {
			var iDesignFace = docObject as IDesignFace;
			if (iDesignFace != null)
				return iDesignFace;

			var custom = docObject as CustomObject;
			if (custom != null)
				return custom.Type == OscillatorPart.Type ? custom : null;

			Debug.Fail("Unexpected case");
			return null;
		}

		protected override void OnEnable(bool enable) {
			if (enable)
				Window.PreselectionChanged += Window_PreselectionChanged;
			else
				Window.PreselectionChanged -= Window_PreselectionChanged;
		}

		void Window_PreselectionChanged(object sender, EventArgs e) {
			//if (IsDragging)
			//    return;

			// preselection can change without the mouse moving (e.g. just created a profile)
			Rendering = null;

			InteractionContext context = InteractionContext;
			Line cursorRay = context.CursorRay;
			if (cursorRay != null)
				OnMouseMove(context.Window.CursorPosition, cursorRay, Control.MouseButtons);
		}

		protected override bool OnMouseMove(ScreenPoint cursorPos, Line cursorRay, MouseButtons button) {
			if (button != MouseButtons.None)
				return false;

			IDocObject preselection = InteractionContext.Preselection;
			IDesignFace iDesignFace = null;

			OscillatorPart existingOscillatorHandle = OscillatorPart.GetWrapper(preselection as CustomObject);
			if (existingOscillatorHandle != null)
				iDesignFace = existingOscillatorHandle.IDesignFace;
			if (iDesignFace == null)
				iDesignFace = preselection as DesignFace;
			if (iDesignFace == null) // selection filtering is not applied if you (pre)select in the tree
				return false;
			
			SurfaceEvaluation eval = iDesignFace.Shape.GetSingleRayIntersection(cursorRay);
			if (eval == null)
				return false;

			Rendering = Graphic.Create(null, null, OscillatorPart.GetGraphics(iDesignFace, eval.Param, 0.05, 0, HandleTypeEnum.All));

			return false; // if we return true, the preselection won't update
		}

		#region Click-Click Notifications

		protected override bool OnClickStart(ScreenPoint cursorPos, Line cursorRay) {
			var iDesignFace = InteractionContext.Preselection as IDesignFace;
			if (iDesignFace == null)
				return false;

			SurfaceEvaluation eval = iDesignFace.Shape.GetSingleRayIntersection(cursorRay);
			if (eval == null)
				return false;

			double defaultHeight = 0.02;
			WriteBlock.ExecuteTask("Create Oscillator", () => {
				OscillatorPart.Create(HandleTypeEnum.Shaft, iDesignFace, eval.Param, defaultHeight, 0);
				OscillatorPart.Create(HandleTypeEnum.Base, iDesignFace, eval.Param, defaultHeight, 0);
				OscillatorPart.Create(HandleTypeEnum.Start, iDesignFace, eval.Param, defaultHeight, 0);
				OscillatorPart.Create(HandleTypeEnum.End, iDesignFace, eval.Param, defaultHeight, 0);
			});

			return false;
		}

		#endregion

		#region Drag Notifications

		protected override bool OnDragStart(ScreenPoint cursorPos, Line cursorRay) {
			//SelectionTypes = new Type[0]; // disable preselection while dragging

			OscillatorPart oscillatorPart = OscillatorPart.GetWrapper(InteractionContext.Preselection as CustomObject);
			if (oscillatorPart == null)
				return false;

			Point? pointOnCustom = InteractionContext.PreselectionPoint;
			if (pointOnCustom == null) {
				oscillatorPart = null;
				return false;
			}

			switch (oscillatorPart.HandleType) {
				case HandleTypeEnum.Base:
					var iDesignFace = InteractionContext.Preselection as IDesignFace;
					if (iDesignFace == null)
						return false;

					SurfaceEvaluation eval = iDesignFace.Shape.GetSingleRayIntersection(cursorRay);
					if (eval == null)
						return false;

					oscillatorIDesignFace = iDesignFace;
					oscillatorPointUV = eval.Param;

					StatusText = "Drag to move the oscillator.";
					break;

				case HandleTypeEnum.Start:
				case HandleTypeEnum.End:
					break;
			}

			return false;
		}

		protected override void OnDragMove(ScreenPoint cursorPos, Line cursorRay) {
			if (oscillatorParts == null || oscillatorParts.Count == 0)
				return;

			var iDesignFace = InteractionContext.Preselection as IDesignFace;
			if (iDesignFace == null)
				return;

			oscillatorIDesignFace = iDesignFace;
			oscillatorPointUV = oscillatorIDesignFace.Shape.GetSingleRayIntersection(cursorRay).Param;

			Apply("Modify Oscillator");
		}

		protected override void OnDragEnd(ScreenPoint cursorPos, Line cursorRay) {
			Complete(); // completes the apply-loop
			Reset();
		}

		protected override void OnDragCancel() {
			Cancel(); // aborts work done by the last call to Apply
			Reset();
		}

		#endregion

		protected override bool OnApply() {
			Debug.Assert(oscillatorParts != null && oscillatorParts.Count > 0);

			foreach (OscillatorPart oscillatorPart in oscillatorParts) {
				oscillatorPart.IDesignFace = oscillatorIDesignFace;
				oscillatorPart.PointUV = oscillatorPointUV;
			}
			return true;
		}
	}

	public enum HandleTypeEnum {
		All = -1,
		Shaft = 0,
		Base = 1,
		Start = 2,
		End = 3
	}

	public class OscillatorPart : CustomWrapper<OscillatorPart> {
		int handleTypeInt;
		IDesignFace iDesignFace;
		PointUV pointUV;
		double span;
		double position;

		// creates a wrapper for an existing custom object
		protected OscillatorPart(CustomObject subject)
			: base(subject) {
		}

		// creates a new custom object and a wrapper for it
		protected OscillatorPart(HandleTypeEnum handleType, IDesignFace iDesignFace, PointUV pointUV, double span, double position)
			: base(iDesignFace.GetAncestor<Part>()) {
			this.handleTypeInt = (int)handleType;
			this.iDesignFace = iDesignFace;
			this.pointUV = pointUV;
			this.span = span;
			this.position = position;
		}

		// static Create method follows the API convention and parent should be first argument
		public static OscillatorPart Create(HandleTypeEnum handleType, IDesignFace iDesignFace, PointUV pointUV, double span, double position) {
			var widget = new OscillatorPart(handleType, iDesignFace, pointUV, span, position);
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
			get {
				if (iDesignFace == null || iDesignFace.IsDeleted)
					return false;

				return true;
			}
		}

		protected override void Update() {
			if (IsChanged(iDesignFace))
				UpdateRendering();
		}

		void UpdateRendering() {
			Graphic shape = Graphic.Create(null, null, GetGraphics(iDesignFace, pointUV, span, position, HandleType));

			GraphicStyle style;

			Graphic halo; // for prehighlighting
			{
				// show halo when preselected
				style = new GraphicStyle {
					IsPreselection = true,
					IsVisible = true
				};
				Graphic haloWhenPreselected = Graphic.Create(style, null, shape);

				// when selected make the halo width 7 instead of 5
				style = new GraphicStyle {
					IsPrimarySelection = true,
					LineWidth = 7
				};
				Graphic haloWhenSelected = Graphic.Create(style, null, haloWhenPreselected);

				// set halo color and width, but do not show
				style = new GraphicStyle {
					IsVisible = false,
					LineColor = Color.Pink,
					LineWidth = 5
				};
				halo = Graphic.Create(style, null, haloWhenSelected);
			}

			// when selected make the width 3
			style = new GraphicStyle {
				IsPrimarySelection = true,
				LineWidth = 3
			};
			Graphic whenSelected = Graphic.Create(style, null, shape);

			// set regular width and color
			style = new GraphicStyle {
				LineWidth = 1,
				LineColor = Color.Red,
				FillColor = Color.WhiteSmoke
			};
			Graphic root = Graphic.Create(style, null, halo, whenSelected);

			Rendering = root;
		}

		public static ICollection<Graphic> GetGraphics(IDesignFace iDesignFace, PointUV pointUV, double span, double position, HandleTypeEnum handleType) {
			Debug.Assert(iDesignFace != null);

			Window activeWindow = Window.ActiveWindow;
			var graphics = new List<Graphic>();

			var primitives = new List<Primitive>();

			SurfaceEvaluation eval = iDesignFace.Shape.Geometry.Evaluate(pointUV);
			Point point = eval.Point;
			Direction normal = eval.Normal;

			double pixelSize = activeWindow.ActiveContext.GetPixelSize(point);
			double shaftDiameter = 4 * pixelSize;
			double handleDiameter = 8 * pixelSize;
			double handleHeight = 4 * pixelSize;

			Point startPoint = point - (normal * (span * (position + 1) / 2 - handleHeight));
			Point endPoint = startPoint + normal * (span + 2 * handleHeight);
			Vector handleHalfThickness = normal * handleHeight / 2;

			bool isDrawingAll = handleType == HandleTypeEnum.All;
			var mesh = new List<Primitive>();
			var style = new GraphicStyle
				{
					EnableDepthBuffer = true,
					LineColor = Color.DimGray,
					LineWidth = 1,
					FillColor = Color.DimGray
				};

			switch (handleType) {
				case HandleTypeEnum.All:
				case HandleTypeEnum.Shaft:
					mesh.AddRange(ShapeHelper.CreateCylinderMesh(startPoint, endPoint, shaftDiameter, 8));
					graphics.Add(Graphic.Create(style, mesh));

					if (isDrawingAll)
						goto case HandleTypeEnum.Base;
					else
						break;

				case HandleTypeEnum.Base:
					mesh.AddRange(ShapeHelper.CreateCylinderMesh(point - handleHalfThickness, point + handleHalfThickness, handleDiameter, 12));
					style.LineColor = Color.DodgerBlue;
					style.FillColor = Color.DodgerBlue;
					graphics.Add(Graphic.Create(style, mesh));

					if (isDrawingAll)
						goto case HandleTypeEnum.Start;
					else 
						break;

				case HandleTypeEnum.Start:
					mesh.AddRange(ShapeHelper.CreateCylinderMesh(startPoint - handleHalfThickness, startPoint + handleHalfThickness, handleDiameter, 12));
					style.LineColor = Color.DarkViolet;
					style.FillColor = Color.DarkViolet;
					graphics.Add(Graphic.Create(style, mesh));

					if (isDrawingAll)
						goto case HandleTypeEnum.End;
					else 
						break;

				case HandleTypeEnum.End:
					mesh.AddRange(ShapeHelper.CreateCylinderMesh(endPoint - handleHalfThickness, endPoint + handleHalfThickness, handleDiameter, 12));
					style.LineColor = Color.DarkViolet;
					style.FillColor = Color.DarkViolet;
					graphics.Add(Graphic.Create(style, mesh));

					break;
			}

			return graphics;
		}

		public HandleTypeEnum HandleType {
			get { return (HandleTypeEnum)handleTypeInt; }
			set { 
				Debug.Assert(value != HandleTypeEnum.All, "HandleTypeEnum.All can only be used to get graphics.");
				handleTypeInt = (int)value; 
			}
		}

		public IDesignFace IDesignFace {
			get { return iDesignFace; }
			set { iDesignFace = value; }
		}

		public PointUV PointUV {
			get { return pointUV; }
			set { pointUV = value; }
		}

		public double Span {
			get { return span; }
			set {
				if (Accuracy.EqualLengths(value, span))
					return;

				span = value;
				Commit();
			}
		}

		public double Position {
			get { return position; }
			set {
				if (Accuracy.EqualLengths(value, position))
					return;

				position = value;
				Commit();
			}
		}
	}
}