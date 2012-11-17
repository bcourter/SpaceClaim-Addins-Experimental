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
	static class Skeletize {
		const string skeletizeCommandName = "AESkeletize";

		const string optionCylindersCommandName = "AESkeletizeOptionCylinders";
		const string optionSpheresCommandName = "AESkeletizeOptionSpheres";
		const string optionSausagesCommandName = "AESkeletizeOptionSausages";

		static bool isCreatingCylinders = false;
		static bool isCreatingSpheres = false;
		static bool isCreatingSausages = true;

		static double cylinderDiameter = 0.001;

		const string cylinderDiameterCommandName = "AESkeletizeCylinderDiameter";
		const string cylinderDiameterCommandText = "";
		static string[] cylinderDiameterCommandNameSuffixes = {
			"1_10",
			"1_100",
			"1_1000",
			"1_10000",
			"1_100000"
		};

		const string cylinderDiameterAdjustUpCommandName = "AESkeletizeCylinderDiameterAdjustUp";
		const string cylinderDiameterAdjustDownCommandName = "AESkeletizeCylinderDiameterAdjustDown";
		static double cylinderDiameterAdjustment = Math.Pow(10, 0.1);

		public static void Initialize() {
			Command command;

			command = Command.Create(skeletizeCommandName);
			command.Text = "Skeletize";
			command.Hint = "Create a skeleton using the edges and vertices of your model.";
			command.Executing += Skeletize_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;

			command = Command.Create(optionCylindersCommandName);
			command.Text = "Cylinders";
			command.Hint = "Create cylinders on the skeleton.";
			command.Executing += SkeletizeOptionCylinders_Executing;
			command.Updating += AddInHelper.BooleanCommand_Updating;
	//		command.Tag = isCreatingCylinders;

			command = Command.Create(optionSpheresCommandName);
			command.Text = "Spheres";
			command.Hint = "Create spheres on the skeleton.";
			command.Executing += SkeletizeOptionSpheres_Executing;
			command.Updating += AddInHelper.BooleanCommand_Updating;
		//	command.Tag = isCreatingSpheres;

			command = Command.Create(optionSausagesCommandName);
			command.Text = "Sausages";
			command.Hint = "Create sausages on the skeleton.";
			command.Executing += SkeletizeOptionSausages_Executing;
			command.Updating += AddInHelper.BooleanCommand_Updating;
	//		command.Tag = isCreatingSausages;

			command = Command.Create(cylinderDiameterCommandName);
			command.Text = "Set Diameter";
			command.Hint = "Sets the diameter of the skeleton links.";
			command.Updating += AddInHelper.EnabledCommand_Updating;

			foreach (string suffix in cylinderDiameterCommandNameSuffixes) {
				command = Command.Create(cylinderDiameterCommandName + suffix);
				command.Hint = "Sets the diameter of the skeleton links.";
				command.Executing += CylinderDiameter_Executing;
				command.Updating += AddInHelper.EnabledCommand_Updating;
			}

			command = Command.Create(cylinderDiameterAdjustUpCommandName);
			command.Text = "Increase";
			command.Hint = "Increase the size of the link diameter.";
			command.Executing += CylinderDiameterAdjust_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
	//		command.Tag = cylinderDiameterAdjustment;

			command = Command.Create(cylinderDiameterAdjustDownCommandName);
			command.Text = "Decrease";
			command.Hint = "Decrease the size of the link diameter.";
			command.Executing += CylinderDiameterAdjust_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
		//	command.Tag = 1 / cylinderDiameterAdjustment;

			command = Command.Create("AEFaceCenter");
			command.Text = "Face Center";
			command.Hint = "Create Objects on the center of faces.";
			command.Executing += FaceCenter_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
		}

		static void UpdateTags() { // TBD remove once we figure out the right way to attached booleans to tags
//			Command.GetCommand(optionCylindersCommandName).Tag = isCreatingCylinders;
//			Command.GetCommand(optionSpheresCommandName).Tag = isCreatingSpheres;
//			Command.GetCommand(optionSausagesCommandName).Tag = isCreatingSausages;
		}

		static void Skeletize_Executing(object sender, EventArgs e) {
			double sphereDiameter = cylinderDiameter * 2;

			Window activeWindow = Window.ActiveWindow;
			ICollection<ITrimmedCurve> iTrimmedCurves = activeWindow.GetAllSelectedITrimmedCurves();
			if (iTrimmedCurves.Count == 0)
				return;

			Part parent = activeWindow.ActiveContext.Context as Part;
			if (parent == null)
				return;

            Part part = Part.Create(parent.Document, "Skeleton");
			Component component = Component.Create(parent, part);

			List<Point> allPoints = new List<Point>();
			foreach (ITrimmedCurve iTrimmedCurve in iTrimmedCurves) {
				if (isCreatingSpheres && !Accuracy.Equals(iTrimmedCurve.StartPoint, iTrimmedCurve.EndPoint)) {
					allPoints.Add(iTrimmedCurve.StartPoint);
					allPoints.Add(iTrimmedCurve.EndPoint);
				}

				if (iTrimmedCurve.Geometry.GetType().Name == "Line") {
					if (isCreatingCylinders)
						ShapeHelper.CreateCylinder(iTrimmedCurve.StartPoint, iTrimmedCurve.EndPoint, cylinderDiameter, part);
					if (isCreatingSausages)
						ShapeHelper.CreateSausage(iTrimmedCurve.StartPoint, iTrimmedCurve.EndPoint, cylinderDiameter, part);
				}
				else {
					if (isCreatingCylinders || isCreatingSausages)
						ShapeHelper.CreateCable(iTrimmedCurve, cylinderDiameter, part);
					if (isCreatingSausages  && !Accuracy.Equals(iTrimmedCurve.StartPoint, iTrimmedCurve.EndPoint)) {
						ShapeHelper.CreateSphere(iTrimmedCurve.StartPoint, cylinderDiameter, part);
						ShapeHelper.CreateSphere(iTrimmedCurve.EndPoint, cylinderDiameter, part);
						// TBD boolean these with cable
					}
				}

			}

			if (isCreatingSpheres) {
				List<Point> points = new List<Point>();
				while (allPoints.Count > 0) {
					Point testPoint = allPoints[0];
					allPoints.Remove(allPoints[0]);
					bool isDuplicate = false;

					foreach (Point point in allPoints) {
						if (Accuracy.Equals(testPoint, point)) {
							isDuplicate = true;
							break;
						}
					}

					if (!isDuplicate)
						points.Add(testPoint);
				}

				foreach (Point point in points)
					ShapeHelper.CreateSphere(point, sphereDiameter, part);
			}
		}

		static void SkeletizeOptionCylinders_Executing(object sender, EventArgs e) {
			isCreatingCylinders = !isCreatingCylinders;
			if (isCreatingCylinders)
				isCreatingSausages = false;

			UpdateTags();
		}

		static void SkeletizeOptionSpheres_Executing(object sender, EventArgs e) {
			isCreatingSpheres = !isCreatingSpheres;
			if (isCreatingSpheres)
				isCreatingSausages = false;

			UpdateTags();
		}

		static void SkeletizeOptionSausages_Executing(object sender, EventArgs e) {
			isCreatingSausages = !isCreatingSausages;
			if (isCreatingSausages) {
				isCreatingCylinders = false;
				isCreatingSpheres = false;
			}

			UpdateTags();
		}

		static void CylinderDiameter_Executing(object sender, EventArgs e) {
			SetCylinderDiameter(AddInHelper.ParseAffixedCommand(((Command)sender).Name, cylinderDiameterCommandName));
		}

		static void CylinderDiameterAdjust_Executing(object sender, EventArgs e) {
//			SetCylinderDiameter(cylinderDiameter * (double)((Command)sender).Tag);
		}

		static void SetCylinderDiameter(double diameter) {
			cylinderDiameter = diameter;
			Command.GetCommand(cylinderDiameterCommandName).Text = string.Format("{0}{1:0.00e+0} mm", cylinderDiameterCommandText, cylinderDiameter * 1000);
			AddInHelper.RefreshMainform();
		}

		static void FaceCenter_Executing(object sender, EventArgs e) {
			Window activeWindow = Window.ActiveWindow;
			ICollection<IDesignFace> iDesignFaces = activeWindow.GetAllSelectedIDesignFaces();
			if (iDesignFaces.Count == 0)
				return;

			Part parent = activeWindow.ActiveContext.Context as Part;
			if (parent == null)
				return;

			Part part = Part.Create(parent.Document, "Face Centers");
			Component component = Component.Create(parent, part);

			foreach (IDesignFace iDesignFace in iDesignFaces) {
				List<Point> points = new List<Point>();
				foreach (IDesignEdge iDesignEdge in iDesignFace.Edges) {
					points.Add(iDesignEdge.Shape.StartPoint);
					points.Add(iDesignEdge.Shape.EndPoint);
				}
			
				Point center = points.Average();
				SurfaceEvaluation surfaceEvaluation = iDesignFace.Shape.ProjectPoint(center);
				center = surfaceEvaluation.Point;

				if (isCreatingSpheres)
					ShapeHelper.CreateSphere(center, cylinderDiameter, part);

				if (isCreatingCylinders)
					ShapeHelper.CreateCylinder(center + surfaceEvaluation.Normal * cylinderDiameter, center - surfaceEvaluation.Normal * cylinderDiameter, cylinderDiameter, part);
				
			}

		}
	}
}
