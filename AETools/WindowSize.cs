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
	static class WindowSize {
		const string windowSizeCommandName = "AEWindowsSize";
		static string[] windowSizeCommandNameSuffixes = {
			"800x600",
			"1024x768",
			"1280x960",
			"1600x1200"
		};

		static public void Initialize() {
			Command command;

			command = Command.Create(windowSizeCommandName);
			command.Text = "Window Size";
			command.Hint = "Change the tumble window size.";
			command.Updating += AddInHelper.EnabledCommand_Updating;

			command = Command.Create("AEAnisotropicScale");
			command.Text = "Scale";
			command.Hint = "Scale Anisotropically.";
			command.Updating += AddInHelper.EnabledCommand_Updating;
			command.Executing += AnisotropicScale_Executing;

			string suffixWithSpace;
			foreach (string suffix in windowSizeCommandNameSuffixes) {
				command = Command.Create(windowSizeCommandName + suffix);
				suffixWithSpace = suffix.Replace("x", " x ");
				command.Text = suffixWithSpace;
				command.Hint = String.Format("Change the window size to {0}.", suffixWithSpace);
				command.Executing += WindowSize_Executing;
				command.Updating += AddInHelper.EnabledCommand_Updating;
			}
		}

		static void AnisotropicScale_Executing(object sender, EventArgs e) {
			DesignBody designBody = Window.ActiveWindow.ActiveContext.SingleSelection as DesignBody;
			designBody.Scale(
				Frame.Create(designBody.GetBoundingBox(Matrix.Identity).Center, Direction.DirX, Direction.DirY),
				1,
				1.5,
				2
			);
		}


		static void WindowSize_Executing(object sender, EventArgs e) {
			string commandName = ((Command)sender).Name;
			string sizeString = commandName.Substring(windowSizeCommandName.Length, commandName.Length - windowSizeCommandName.Length);
			sizeString = sizeString.Replace(" x ", "x");
			string[] sizes = sizeString.Split('x');

			Debug.Assert(sizes.Length == 2);
			int width, height;
			bool success = int.TryParse(sizes[0], out width);
			success &= int.TryParse(sizes[1], out height);
			Debug.Assert(success);

			AddInHelper.MainForm.Size = new Size(width, height);
		}

	}
}
