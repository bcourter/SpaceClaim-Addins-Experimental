using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Drawing;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.AETools {
	static class Colors {
		const string colorsGrowSelectionCommandName = "AEColorsGrowSelection";

		static double threshold = 0;
		const string growSelectionThresholdCommandName = "AEColorsGrowSelectionThreshold";
		const string growSelectionThresholdCommandText = "Threshold ";
		static string[] growSelectionThresholdCommandNameSuffixes = {
			"0",
			"4",
			"16",
			"32",
			"64"
		};

		static public void Initialize() {
			Command command;

			command = Command.Create(colorsGrowSelectionCommandName);
			command.Text = "Grow Selection";
			command.Hint = "Select bodies similar in color to the selected one.";
			command.Executing += growSelection_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;

			command = Command.Create(growSelectionThresholdCommandName);
			command.Text = growSelectionThresholdCommandText + threshold.ToString();
			command.Hint = "Change the tumble speed to a specific RPM";

			foreach (string suffix in growSelectionThresholdCommandNameSuffixes) {
				command = Command.Create(growSelectionThresholdCommandName + suffix);
				command.Hint = "Sets the threshold for color selection";
				command.Executing += growSelectionThreshold_Executing;
			}


			ColorAlter.Initialize();
		}

		static void growSelection_Executing(object sender, EventArgs e) {
			Window activeWindow = Window.ActiveWindow;

			ICollection<DesignBody> selectedDesignBodies = activeWindow.GetAllSelectedDesignBodies();
			ICollection<IDesignBody> allIDesignBodies = (activeWindow.Scene as Part).GetDescendants<IDesignBody>();
			int variance;
			Color selectedBodyColor, bodyColor;
			List<IDocObject> matchingIDesignBodies = new List<IDocObject>();
			foreach (DesignBody selectedDesignBody in selectedDesignBodies) {
				selectedBodyColor = selectedDesignBody.GetVisibleColor();
				foreach (IDesignBody iDesignBody in allIDesignBodies){
					bodyColor = iDesignBody.Master.GetVisibleColor();
					variance =
						(selectedBodyColor.R - bodyColor.R) * (selectedBodyColor.R - bodyColor.R) +
						(selectedBodyColor.G - bodyColor.G) * (selectedBodyColor.G - bodyColor.G) +
						(selectedBodyColor.B - bodyColor.B) * (selectedBodyColor.B - bodyColor.B);
					if (variance < threshold * threshold)
						matchingIDesignBodies.Add(iDesignBody);
				}
			}

			activeWindow.ActiveContext.Selection = matchingIDesignBodies;
		}

		static void growSelectionThreshold_Executing(object sender, EventArgs e) {
			threshold = AddInHelper.ParseAffixedCommand(((Command) sender).Name, growSelectionThresholdCommandName);
			Command.GetCommand(growSelectionThresholdCommandName).Text = growSelectionThresholdCommandText + threshold.ToString();
			AddInHelper.RefreshMainform();
		}

	}
}
