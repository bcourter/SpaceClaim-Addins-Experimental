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
using Application = SpaceClaim.Api.V10.Application;

namespace SpaceClaim.AddIn.Animator {
	class RecordMovieCapsule : CommandCapsule {
		public const string CommandName = "Animator.RecordMovie";
		bool IsRecording = false;

		public RecordMovieCapsule()
			: base(CommandName, Resources.RecordMovieText, null, Resources.RecordMovieHint) {
		}

		protected override void OnUpdate(Command command) {
			Window window = Window.ActiveWindow;
			command.IsEnabled = window != null;
			command.IsChecked = IsRecording;
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			if (!IsRecording) {
				SaveFileDialog dialog = new SaveFileDialog();
				dialog.Filter = "AVI Files (*.avi)|*.avi";
				DialogResult result = dialog.ShowDialog();

				if (result != DialogResult.OK)
					return;

			//	Application.StartVideo(dialog.FileName, VideoCapture.Window);
				IsRecording = true;
			}
			else {
				Application.StopVideo();
				IsRecording = false;

			}
		}
	}



}