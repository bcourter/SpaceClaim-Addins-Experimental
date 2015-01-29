using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;
using AETools.Properties;

namespace SpaceClaim.AddIn.AETools {
    static class Options {
        const string forceNewVersionCommandName = "AEForceNewVersion";
        const string showOptimizedPanelCommandName = "AEShowOptimizedPanel";
        const string playSoundsCommandName = "AEPlaySounds";
        const string optimizedPanelCommandName = "AEOptimizedPanel";
        const string twitterPanelCommandName = "twitterPanel";

        static bool isOptimizedPanelVisible = false;
        static bool isForcingNewVersion = false;

        static List<Document> openDocuments = new List<Document>();
        static System.Media.SoundPlayer soundPlayer = new System.Media.SoundPlayer();

        public static void Initialize() {
            Command command;

            // Custom Shortcuts
            Command.GetCommand("ThreePointCylinder").ShortcutMnemonic = 'W';
            Command.GetCommand("Sphere").ShortcutMnemonic = 'Q';

            command = Command.GetCommand("PullBlend");
            command.ShortcutMnemonic = default(char);

            //command = Command.Create("GoToPullBlend");
            //command.ShortcutMnemonic = 'B';
            //command.Executing += delegate(object sender, CommandExecutingEventArgs e) {
            //    Command.Execute("PullTool");
            //    Thread.Sleep(100);
            //    Command.Execute("PullBlend");
            //};
            //command.Updating += AddInHelper.EnabledCommand_Updating;

            command = Command.Create("DeleteBetter");
            command.Executing += DeleteBetter_Executing;
            command.Shortcuts = new Keys[] { Keys.Delete };

            // Twitter Stuff
            command = Command.Create("twitterIsOn");
            command.Text = "Enable";
            command.Hint = "Retrieves tweets when enabled.";
            command.Image = Resources.TwitterOn;
            command.Executing += delegate(object sender, CommandExecutingEventArgs e) {
                Command cmd = (Command)sender;
                cmd.IsChecked = !cmd.IsChecked;
            };
            command.Updating += AddInHelper.EnabledCommand_Updating;

            command = Command.Create("twitterPost");
            command.Text = "Post Tweet";
            command.Hint = "Post a tweet to Twitter.";
            command.Image = Resources.PostTweet;
            command.Executing += delegate(object sender, CommandExecutingEventArgs e) { };
            command.Updating += AddInHelper.EnabledCommand_Updating;

            command = Command.Create("twitterPostImage");
            command.Text = "Post Image";
            command.Hint = "Post tweet while attached an image using TwitPic.";
            command.Image = Resources.PostImage;
            command.Executing += delegate(object sender, CommandExecutingEventArgs e) { };
            command.Updating += AddInHelper.EnabledCommand_Updating;

            command = Command.Create("twitterPostMovie");
            command.Text = "Post Movie";
            command.Hint = "Post tweet while attached an image using TwitPic.";
            command.Image = Resources.PostMovie;
            command.Executing += delegate(object sender, CommandExecutingEventArgs e) { };
            command.Updating += AddInHelper.EnabledCommand_Updating;

            // Twitter Options
            command = Command.Create("twitterIsShowingOwnTweets");
            command.Text = "Retrieve own tweets";
            command.Hint = "Retrieve your own tweets";
            command.IsChecked = true;
            command.Executing += delegate(object sender, CommandExecutingEventArgs e) { };
            command.Updating += AddInHelper.EnabledCommand_Updating;

            command = Command.Create("twitterIsCheckingDirectMessages");
            command.Text = "Retrieve direct messages";
            command.Hint = "Retrieve direct messages";
            command.IsChecked = true;
            command.Executing += delegate(object sender, CommandExecutingEventArgs e) { };
            command.Updating += AddInHelper.EnabledCommand_Updating;

            command = Command.Create("twitterIsAnnoying");
            command.Text = "Twitter is annoying";
            command.Hint = "Come on, don't you have a life?";
            command.IsChecked = true;
            command.Executing += delegate(object sender, CommandExecutingEventArgs e) { };
            command.Updating += AddInHelper.EnabledCommand_Updating;

            command = Command.Create(twitterPanelCommandName);
            command.Text = "Twitter";
            command.Hint = "This is a prank.";
            command.IsVisible = isOptimizedPanelVisible;


            // Optimized Panel
            command = Command.Create(showOptimizedPanelCommandName);
            command.Text = "Show optimized tab";
            command.Hint = "Show the AE optimized ribbon bar";
            command.Executing += ShowOptimizedPanel_Executing;
            command.Updating += ShowOptimizedPanel_Updating;

            command = Command.Create(optimizedPanelCommandName);
            command.Text = "Tab of condensed commands";
            command.Hint = "An advanced layout for a sophisticated user like you";
            command.IsVisible = isOptimizedPanelVisible;

            // Force New Version
            command = Command.Create(forceNewVersionCommandName);
            command.Text = "Always save new version";
            command.Hint = "Locks files so save behaves like a deep saves as new version";
            command.Executing += ForceNewVersion_Executing;
            command.Updating += ForceNewVersion_Updating;

            Document.DocumentSaved += Document_DocumentSaved;
            Document.DocumentAdded += Document_DocumentAdded;

            //xxx test for customer
       //     Window.WindowAdded += windowAdded;

        }

        static void Document_DocumentAdded(object sender, SubjectEventArgs<Document> e) {
            if (isForcingNewVersion)
                LockDocument(e.Subject as Document);
        }

        static void Document_DocumentSaved(object sender, SaveDocumentEventArgs e) {
            if (isForcingNewVersion)
                LockDocument(e.Document);
        }

        //static void windowAdded(object sender, SubjectEventArgs<Window> e) {
        //    if (e.Subject is Window) {
        //        Window win = (Window)e.Subject;
        //        if (win == null)
        //            return;
        //        Document doc = win.Document;
        //    }
        //}


        static void ShowOptimizedPanel_Executing(object sender, EventArgs e) {
            isOptimizedPanelVisible = !isOptimizedPanelVisible;
            Command.GetCommand(optimizedPanelCommandName).IsVisible = isOptimizedPanelVisible;
            Command.GetCommand(twitterPanelCommandName).IsVisible = isOptimizedPanelVisible;
            AddInHelper.RefreshMainform();
        }

        static void ShowOptimizedPanel_Updating(object sender, EventArgs e) {
            Command command = (Command)sender;
            command.IsChecked = isOptimizedPanelVisible;
            command.IsEnabled = true;
        }

        static void ForceNewVersion_Executing(object sender, CommandExecutingEventArgs e) {
            Command command = (Command)sender;
            isForcingNewVersion = !isForcingNewVersion;
            //if (!isForcingNewVersion)
            //    return;

            //foreach (Document doc in Window.AllWindows.Select(w => w.Document).Distinct())
            //    LockDocument(doc);
        }

        static void ForceNewVersion_Updating(object sender, EventArgs e) {
            Command command = (Command)sender;
            command.IsChecked = isForcingNewVersion;
        }

        static void LockDocument(Document document) {
            if (document.Path != "" && isForcingNewVersion)
                System.IO.File.SetAttributes(document.Path, System.IO.FileAttributes.ReadOnly);
        }

        static void DeleteBetter_Executing(object sender, EventArgs e) {
            Dictionary<Body, List<Face>> deleteFaces = new Dictionary<Body, List<Face>>();

            foreach (Face face in Window.ActiveWindow.ActiveContext.GetSelection<IDesignFace>().Select(f => f.Master.Shape)) {
                if (!deleteFaces.ContainsKey(face.Body))
                    deleteFaces[face.Body] = new List<Face>();

                if (!deleteFaces[face.Body].Contains(face))
                    deleteFaces[face.Body].Add(face);
            }

            foreach (Body body in deleteFaces.Keys)
                body.DeleteFaces(deleteFaces[body], RepairAction.None);

            Command.Execute("Delete");
        }

    }
}
