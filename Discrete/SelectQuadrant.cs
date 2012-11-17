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
	abstract class SelectQuadrantButtonCapsule : RibbonButtonCapsule {
		string quadrantString;
		public SelectQuadrantButtonCapsule(string quadrantString, RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("Quadrant" + quadrantString, quadrantString, null, "", parent, buttonSize) {
			this.quadrantString = quadrantString;
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			bool whichX = quadrantString.Substring(0, 1) == "0" ? false : true;
			bool whichY = quadrantString.Substring(1, 1) == "0" ? false : true;
			bool whichZ = quadrantString.Substring(2, 1) == "0" ? false : true;

			Command.Execute("Select");
			List<IDocObject> iDesBodies = new List<IDocObject>();
			foreach (IDesignBody iDesBody in MainPart.GetDescendants<IDesignBody>()) {
				Point p = iDesBody.Master.Shape.GetBoundingBox(Matrix.Identity).Center;

				if ((p.X > 0 ^ whichX) && (p.Y > 0 ^ whichY) && (p.Z > 0 ^ whichZ))
					iDesBodies.Add(iDesBody);
			}

			ActiveWindow.ActiveContext.Selection = iDesBodies;
			Command.Execute("IntersectTool");
		}
	}

	class SelectQuadrant000ButtonCapsule : SelectQuadrantButtonCapsule {
		public SelectQuadrant000ButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("000", parent, buttonSize) {
		}
	}

	class SelectQuadrant100ButtonCapsule : SelectQuadrantButtonCapsule {
		public SelectQuadrant100ButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("100", parent, buttonSize) {
		}
	}

	class SelectQuadrant010ButtonCapsule : SelectQuadrantButtonCapsule {
		public SelectQuadrant010ButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("010", parent, buttonSize) {
		}
	}

	class SelectQuadrant110ButtonCapsule : SelectQuadrantButtonCapsule {
		public SelectQuadrant110ButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("110", parent, buttonSize) {
		}
	}

	class SelectQuadrant001ButtonCapsule : SelectQuadrantButtonCapsule {
		public SelectQuadrant001ButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("001", parent, buttonSize) {
		}
	}

	class SelectQuadrant101ButtonCapsule : SelectQuadrantButtonCapsule {
		public SelectQuadrant101ButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("101", parent, buttonSize) {
		}
	}

	class SelectQuadrant011ButtonCapsule : SelectQuadrantButtonCapsule {
		public SelectQuadrant011ButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("011", parent, buttonSize) {
		}
	}

	class SelectQuadrant111ButtonCapsule : SelectQuadrantButtonCapsule {
		public SelectQuadrant111ButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("111", parent, buttonSize) {
		}
	}

}