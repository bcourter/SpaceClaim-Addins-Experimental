using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.AETools {
	static class Forstnerize {
		const double inchesToMeters = 25.4 / 1000;

		static double diameter = 0.25 * inchesToMeters;
		const string forstnerizeDiameterCommandName = "AEForstnerDiameter";
		const string forstnerizeDiameterCommandText = "";
		static string[] forstnerizeDiameterCommandNameSuffixes = {
			"1_8",
			"3_16",
			"1_4",
			"5_16",
			"3_8",
			"7_16",
			"1_2",
			"5_8",
			"3_4",
			"7_8",
			"1"
		};

		public static void Initialize() {
			Command command;

			command = Command.Create("AEForstnerize");
			command.Text = "Forstnerize";
			command.Hint = "Pick a surface and a plane to create notes that correspond to depths to rough out using a fostner bit.";
			command.Executing += Forstnerize_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;

			command = Command.Create(forstnerizeDiameterCommandName);
			command.Text = diameter.ToString();
			command.Hint = "Sets the diameter of the drill.";
			command.Updating += AddInHelper.EnabledCommand_Updating;

			foreach (string suffix in forstnerizeDiameterCommandNameSuffixes) {
				command = Command.Create(forstnerizeDiameterCommandName + suffix);
				command.Hint = "Sets the diameter of the the drill.";
				command.Executing += ForstnerizeDiameter_Executing;
				command.Updating += AddInHelper.EnabledCommand_Updating;
			}
		}

		static void Forstnerize_Executing(object sender, EventArgs e) {
			Window activeWindow = Window.ActiveWindow;

			IDesignBody forstnerBody = null;
			Box faceBoundingBox = Box.Empty;
			foreach (IDesignFace iDesignFace in activeWindow.ActiveContext.GetSelection<IDesignFace>()) {
				forstnerBody = iDesignFace.GetAncestor<IDesignBody>();
				faceBoundingBox |= iDesignFace.Shape.GetBoundingBox(Matrix.Identity);
			}
			if (forstnerBody == null || faceBoundingBox == Box.Empty)
				return;

			Part part = Part.Create(activeWindow.Document, "Forstner Bottoms");
			Component component = Component.Create(activeWindow.Scene as Part, part);

			Box bodyBoundingBox = forstnerBody.Shape.GetBoundingBox(Matrix.Identity);
			Plane topPlane = Plane.Create(Frame.Create(bodyBoundingBox.MaxCorner, Direction.DirX, Direction.DirY));
			double xSpacing = diameter * Math.Sqrt(3) / 2;
			double ySpacing = diameter * 3 / 4;
			bool shortRow = false;
			for (double y = faceBoundingBox.MinCorner.Y; y < faceBoundingBox.MaxCorner.Y; y += ySpacing) {
				for (double x = shortRow ? faceBoundingBox.MinCorner.X + xSpacing / 2 : faceBoundingBox.MinCorner.X; x < faceBoundingBox.MaxCorner.X; x += xSpacing) {
					List<IDesignBody> referenceBodies = new List<IDesignBody>();
					referenceBodies.Add(DesignBody.Create(
						Part.Create(activeWindow.Document, "Temp"), 
						"Target Copy", 	
						forstnerBody.Master.Shape.Copy()
					));

					Point lowerPoint = Point.Create(x, y, bodyBoundingBox.MinCorner.Z);
					Point upperPoint = Point.Create(x, y, bodyBoundingBox.MaxCorner.Z);
					IDesignBody drillBody = ShapeHelper.CreateCylinder(lowerPoint, upperPoint, diameter, activeWindow.Scene as Part);

					ICollection<IDesignBody> outputBodies = new List<IDesignBody>();
					try {
	//XXX					outputBodies = drillBody.Subtract(referenceBodies);
					}
					finally {
					    //outputBodies = new List<IDesignBody>();
					}
						
					// Find the top of the faces created by the intersection of the cylinder and the target.  The top of the bounding box of all faces except the top face and the cylinder of the drill are perfect.
					Box bottomBox = Box.Empty;
					Cylinder drillCylinder = Cylinder.Create(
						Frame.Create(lowerPoint, Direction.DirX, Direction.DirY),
						diameter / 2
					);

					bool hasTop = false;
					foreach (IDesignBody iDesignBody in outputBodies) {
						foreach (IDesignFace iDesignFace in iDesignBody.Faces) {
							Plane plane = iDesignFace.Shape.Geometry as Plane;
							if (plane != null) {
								if (AddInHelper.isCooincident(plane, topPlane)) {
									hasTop = true;
									continue;
								}
							}

							Cylinder cylinder = iDesignFace.Shape.Geometry as Cylinder;
							if (cylinder != null) {
								if (AddInHelper.isCooincident(cylinder, drillCylinder))
									continue;
							}

							bottomBox |= iDesignFace.Shape.GetBoundingBox(Matrix.Identity);
						}
						iDesignBody.Delete();
					}

					if (!bottomBox.IsEmpty && hasTop) {
						Point bottomPoint = Point.Create(lowerPoint.X, lowerPoint.Y, bottomBox.MaxCorner.Z);
						DesignBody bottomBody = ShapeHelper.CreateCircle(Frame.Create(bottomPoint, Direction.DirX, Direction.DirY), diameter, part);
						//AddInHelper.CreateCircle(Frame.Create(bottomPoint, Direction.DirX, Direction.DirY), 0.001, part);
						foreach (DesignFace designFace in bottomBody.Faces)
							NoteHelper.AnnotateFace(designFace.GetAncestor<Part>(), designFace, activeWindow.Units.Length.Format(-bottomPoint.Z), diameter / 5, Direction.DirZ);
					}
				}
				shortRow = !shortRow;
			}

		}

		static void ForstnerizeDiameter_Executing(object sender, EventArgs e) {
			diameter = AddInHelper.ParseAffixedCommand(((Command)sender).Name, forstnerizeDiameterCommandName);
			Command.GetCommand(forstnerizeDiameterCommandName).Text = diameter.ToString();
			diameter *= inchesToMeters;
		}
	}
}
