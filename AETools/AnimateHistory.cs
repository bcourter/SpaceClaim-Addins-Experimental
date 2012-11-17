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

namespace SpaceClaim.AddIn.AETools {
	static class AnimateHistory {
		const string animateHistoryCommandName = "AEAnimateHistory";

		public static void Initialize() {
			Command command;

			command = Command.Create(animateHistoryCommandName);
			command.Text = "AnimateHistory";
			command.Hint = "Save an image of every version of the current document";
			command.Executing += animateHistory_Executing;
			command.Updating += animateHistory_Updating;
		}

		static void animateHistory_Executing(object sender, EventArgs e) {
            Debug.Fail("Need to upgrade to API.v10");

            //Document animDocument = Window.ActiveWindow.ActiveContext.Context.Document;
            //string animDocumentPath = animDocument.Path;
            //Matrix viewTrans = Window.ActiveWindow.Projection;

            ////Dictionary<Moniker, bool> layerStates = new Dictionary<Moniker, bool>();
            ////foreach (Layer layer in animDocument.Layers)
            ////    layerStates.Add(layer.Moniker, layer.IsVisible);

            //FileInfo animDocumentInfo = new FileInfo(animDocumentPath);
            //string animDirectory = animDocumentInfo.DirectoryName;
            //string animDocumentName = animDocumentInfo.Name;
            //// TBD: fix this with a regexp so it works on .scdoc and not just .5.scdoc
            //string animDocumentBareName = animDocumentName.Substring(0, animDocumentName.LastIndexOf("."));
            //animDocumentBareName = animDocumentBareName.Substring(0, animDocumentBareName.LastIndexOf("."));

            //string imageDirectory = animDirectory + @"\History Images";
            //if (!Directory.Exists(imageDirectory))
            //    Directory.CreateDirectory(imageDirectory);

            //Window containerWindow = Document.Create();
            //containerWindow.SetProjection(viewTrans, false, false);
            //Document containerDocument = containerWindow.ActiveContext.Context.Document;

            //foreach (string animDocumentVersion in Directory.GetFiles(animDirectory, animDocumentBareName + "*", SearchOption.TopDirectoryOnly)) {
            //    Component component = Component.CreateFromFile(containerDocument.MainPart, animDocumentVersion, ImportOptions.Create());
            //    //foreach (Group group in component.Template.

            //    //foreach (Layer layer in animDocument.Layers) {
            //    //    layerStates.Add(layer.Moniker, layer.IsVisible);
            //    //    DesignFace oldFace = newFace.Moniker.Resolve(oldDoc);
            //    //}

            //    string imageFileDocumentName = (new FileInfo(animDocumentVersion)).Name;
            //    string imageFileNameBare = imageFileDocumentName.Substring(0, imageFileDocumentName.LastIndexOf("."));
            //    imageFileNameBare = imageFileNameBare.Replace(".", "-"); // work around bug where saving test.22 becomes test.png, not test.22.png

            //    containerWindow.Export(WindowExportFormat.Png, imageDirectory + @"\" + imageFileNameBare);

            //    component.Delete();
            //}

		}

		static void animateHistory_Updating(object sender, EventArgs e) {
			Command command = (Command) sender;
			command.IsEnabled = true;
		}
	}
}
