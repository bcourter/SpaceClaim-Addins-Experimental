using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.Animator {
	static class Ribbon {
		static Animator animator;

		public static void Initialize() {
			Command command;

			animator = new Animator();

			command = Command.Create("Animator.Play");
			command.Text = "Play";
			command.Hint = "Toggle between playing and pausing.";
			command.Executing += Play_Executing;
			command.Updating += AddInHelper.BooleanCommand_Updating;

			command = Command.Create("Animator.Reset");
			command.Text = "Reset";
			command.Hint = "Reset components to initial positions.";
			command.Executing += Reset_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
		}

		public static void Disconnect() {
			animator.Stop();
		}

		static void Play_Executing(object sender, EventArgs e) {
			if (animator.IsPlaying)
				animator.Stop();
			else
				animator.Start();
		}

		static void Reset_Executing(object sender, EventArgs e) {
			animator.Reset();
		}


	}
}
