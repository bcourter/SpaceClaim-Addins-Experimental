using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.Threading;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.AETools {
	static class Tumble {
		static TumbleBase activeTumbler;
		static Dictionary<String, TumbleBase> tumblers = new Dictionary<string, TumbleBase>();

		const string tumbleCommandName = "AETumble";
		const string tumbleStylesCommandName = "AETumbleStyles";
		const string tumbleColorsCommandName = "AETumbleColors";
		const string tumbleAboutCenterCommandName = "AETumbleAboutCenter";
		const string tumbleSaveImagesCommandName = "AETumbleSaveImages";

		const string tumbleStyleSpinYCommandName = "tumbleStyleSpinY";
		const string tumbleStyleSpinY30CommandName = "tumbleStyleSpinY30";
		const string tumbleStyleGazeCommandName = "tumbleStyleGaze";
		const string tumbleStyleFreeFallCommandName = "tumbleStyleFreeFall";
		const string tumbleStyleDizzyCommandName = "tumbleStyleDizzy";

		static double tumbleSpeed;
		const string tumbleSpeedCommandName = "AETumbleSpeed";
		static string[] tumbleSpeedCommandNameSuffices = {
			"30",
			"15",
			"10",
			"5",
			"4",
			"3",
			"2",
			"1",
			"1_2", 
			"1_4",
			"1_10",
			"1_30",
			"1_60"
		};

		static bool tumbleIsChangingColors = false;
		static bool tumbleIsCenteredOnSelection = false;
		static bool tumbleIsSavingImages = false;

		public static void Initialize() {
			Command command;

			command = Command.Create(tumbleCommandName);
			command.Text = "Tumble";
			command.Hint = "Tumble my part on screen for the general viewing pleasure of all";
			command.Executing += Tumble_Executing;
			command.Updating += Tumble_Updating;

			command = Command.Create(tumbleSpeedCommandName);
			command.Text = "Set Tumble Speed";
			command.Hint = "Change the tumble speed to a specific RPM";

			foreach (string tumbleSpeedCommandNameSuffix in tumbleSpeedCommandNameSuffices) {
				command = Command.Create(tumbleSpeedCommandName + tumbleSpeedCommandNameSuffix);
				command.Hint = "Sets the tumble speed, in RPM";
				command.Executing += TumbleSpeed_Executing;
			}
			tumbleSpeed = 5;

			command = Command.Create(tumbleStylesCommandName);
			command.Hint = "Sets the tumble style";

			AddTumbleStyleCommand(tumbleStyleSpinYCommandName, activeTumbler = new SpinYTumbler(tumbleSpeed, tumbleIsCenteredOnSelection));
			AddTumbleStyleCommand(tumbleStyleSpinY30CommandName, activeTumbler = new SpinY30Tumbler(tumbleSpeed, tumbleIsCenteredOnSelection));
			AddTumbleStyleCommand(tumbleStyleGazeCommandName, new GazeTumbler(tumbleSpeed, tumbleIsCenteredOnSelection));
			AddTumbleStyleCommand(tumbleStyleFreeFallCommandName, new FreeFallTumbler(tumbleSpeed, tumbleIsCenteredOnSelection));
			AddTumbleStyleCommand(tumbleStyleDizzyCommandName, new DizzyTumbler(tumbleSpeed, tumbleIsCenteredOnSelection));
			activeTumbler = tumblers[tumbleStyleSpinYCommandName];

			command = Command.Create(tumbleColorsCommandName);
			command.Text = "Tumble Colors";
			command.Hint = "Changes the colors during tumbling.  Will permanately recolor your world.  You have been warned.";
			command.Executing += TumbleColors_Executing;
			command.Updating += TumbleColors_Updating;

			command = Command.Create(tumbleAboutCenterCommandName);
			command.Text = "Use Selection Center";
			command.Hint = "Spin about the center of the selection.";
			command.Executing += TumbleAboutCenter_Executing;
			command.Updating += TumbleAboutCenter_Updating;

			command = Command.Create(tumbleSaveImagesCommandName);
			command.Text = "Save Images";
			command.Hint = "Save an image in your temp directory for every frame.  Slows speed by 1/4.  Should be working even if SpaceClaim appears unresponsive.";
			command.Executing += TumbleSaveImages_Executing;
			command.Updating += TumbleSaveImages_Updating;
		}

		public static void Disconnect() {
			activeTumbler.Stop();
		}

		static void AddTumbleStyleCommand(string commandName, TumbleBase tumbler) {
			tumblers.Add(commandName, tumbler);
			Command command = Command.Create(commandName);
	//		command.Tag = tumbler;
			command.Text = "Change Tumble Style";
			command.Executing += TumbleStyle_Executing;
		}

		static void Tumble_Executing(object sender, EventArgs e) {
			if (activeTumbler.IsTumbling)
				activeTumbler.Stop();
			else
				activeTumbler.Start();
		}

		static void Tumble_Updating(object sender, EventArgs e) {
			Command command = (Command) sender;
			command.IsChecked = activeTumbler.IsTumbling;
			command.IsEnabled = true;
		}

		static void TumbleSpeed_Executing(object sender, EventArgs e) {
			activeTumbler.Speed = AddInHelper.ParseAffixedCommand(((Command) sender).Name, tumbleSpeedCommandName);
		}

		static void TumbleStyle_Executing(object sender, EventArgs e) {
			bool isTumbling = activeTumbler.IsTumbling;
			activeTumbler.Stop();
	//		activeTumbler = ((Command) sender).Tag as TumbleBase;
			if (isTumbling)
				activeTumbler.Start();
		}

		static void TumbleColors_Executing(object sender, EventArgs e) {
			tumbleIsChangingColors = !tumbleIsChangingColors;
			activeTumbler.IsChangingColors = tumbleIsChangingColors;
		}

		static void TumbleColors_Updating(object sender, EventArgs e) {
			Command command = (Command) sender;
			command.IsChecked = tumbleIsChangingColors;
			command.IsEnabled = true;
		}

		static void TumbleAboutCenter_Executing(object sender, EventArgs e) {
			tumbleIsCenteredOnSelection = !tumbleIsCenteredOnSelection;
			activeTumbler.IsCenteredOnSelection = tumbleIsCenteredOnSelection;
		}

		static void TumbleAboutCenter_Updating(object sender, EventArgs e) {
			Command command = (Command) sender;
			command.IsChecked = tumbleIsCenteredOnSelection;
			command.IsEnabled = true;
		}

		static void TumbleSaveImages_Executing(object sender, EventArgs e) {
			tumbleIsSavingImages = !tumbleIsSavingImages;
			activeTumbler.IsSavingImages = tumbleIsSavingImages;
		}

		static void TumbleSaveImages_Updating(object sender, EventArgs e) {
			Command command = (Command) sender;
			command.IsChecked = tumbleIsSavingImages;
			command.IsEnabled = true;
		}
	}

	class SpinYTumbler : TumbleBase {
		public SpinYTumbler(double speed, bool isCenteredOnSelection)
			: base(speed, isCenteredOnSelection) {
		}

		public override Matrix Iterate(double angle, int step) {
			return Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirY), angle);
		}
	}

	class SpinY30Tumbler : TumbleBase {
		public SpinY30Tumbler(double speed, bool isCenteredOnSelection)
			: base(speed, isCenteredOnSelection) {
		}

		public override Matrix Iterate(double angle, int step) {
			Line axis = Line.Create(Point.Origin, Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirX), Math.PI / 12) * Direction.DirY);
			return Matrix.CreateRotation(axis, angle);
		}
	}

	class GazeTumbler : TumbleBase {
		public GazeTumbler(double speed, bool isCenteredOnSelection)
			: base(speed, isCenteredOnSelection) {
		}

		Matrix lastIteration = Matrix.Identity;
		Matrix thisIteration = Matrix.Identity;
		Matrix compoundIteration = Matrix.Identity;
		Direction axisDir = Direction.DirX;
		public override Matrix Iterate(double angle, int step) {
			axisDir = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), angle) * axisDir;
			thisIteration = Matrix.CreateRotation(Line.Create(Point.Origin, axisDir), Math.PI / 12);
			compoundIteration = thisIteration * lastIteration.Inverse;
			lastIteration = thisIteration;
			return compoundIteration;
		}
	}

	class FreeFallTumbler : TumbleBase {
		public FreeFallTumbler(double speed, bool isCenteredOnSelection)
			: base(speed, isCenteredOnSelection) {
		}

		Direction axisDir = Direction.DirX;
		public override Matrix Iterate(double angle, int step) {
			axisDir = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), angle) * axisDir;
			return Matrix.CreateRotation(Line.Create(Point.Origin, axisDir), angle);
		}
	}

	class DizzyTumbler : TumbleBase {
		public DizzyTumbler(double speed, bool isCenteredOnSelection)
			: base(speed, isCenteredOnSelection) {
		}

		Direction axisDir = Direction.DirX;
		public override Matrix Iterate(double angle, int step) {
			axisDir = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), angle) * axisDir;
			return Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), angle) *
				Matrix.CreateRotation(Line.Create(Point.Origin, axisDir), angle);
		}
	}

	class RandomOrientTumbler : TumbleBase {
		public RandomOrientTumbler(double speed, bool isCenteredOnSelection)
			: base(speed, isCenteredOnSelection) {
		}

		Direction axisDir = Direction.DirX;
		public override Matrix Iterate(double angle, int step) {
			axisDir = Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), angle) * axisDir;
			return Matrix.CreateRotation(Line.Create(Point.Origin, Direction.DirZ), angle) *
				Matrix.CreateRotation(Line.Create(Point.Origin, axisDir), angle);
		}
	}


	abstract class TumbleBase {
		double speed;
		bool isChangingColors = false;
		bool isCenteredOnSelection = false;
		bool isSavingImages = false;
		Thread tumbleThread;
		EventWaitHandle exitThreadEvent = new ManualResetEvent(false);
		delegate Matrix TumbleMethod(Matrix projection, double angle, int step);
		Point center = Point.Origin;

		public TumbleBase(double speed, bool isCenteredOnSelection) {
			this.speed = speed;
			this.isCenteredOnSelection = isCenteredOnSelection;
		}

		public abstract Matrix Iterate(double angle, int step);

		public double Speed {
			get { return speed; }
			set { speed = value; }
		}

		public bool IsChangingColors {
			get { return isChangingColors; }
			set { isChangingColors = value; }
		}

		public bool IsCenteredOnSelection {
			get { return isCenteredOnSelection; }
			set {
				isCenteredOnSelection = value;
				SetCenter();
			}
		}

		public bool IsSavingImages {
			get { return isSavingImages; }
			set { isSavingImages = value; }
		}

		public bool IsTumbling {
			get { return tumbleThread != null; }
		}

		public Point Center {
			get { return center; }
		}

		public void Start() {
			if (!IsTumbling) {
				tumbleThread = new Thread(new ThreadStart(TumbleThread));
				tumbleThread.Start();
			}
		}

		public void Stop() {
			try {
				if (IsTumbling) {
					exitThreadEvent.Set();
					tumbleThread.Join();		// wait for thread to exit
					exitThreadEvent.Reset();
				}
			}
			finally {
				tumbleThread = null;
			}
		}

		void TumbleThread() {
			WaitHandle[] waitHandles = new WaitHandle[] { exitThreadEvent };
			int step = 0;
			int savingImagesStartStep = step;
			int sleep = 1000 / 40; // 40 frames per second
			Matrix transformStep;
			Part part = Window.ActiveWindow.Scene as Part;

			while (EventWaitHandle.WaitAny(waitHandles, 0, false) != 0) {
				// System.Windows.Forms.Application.OpenForms[0].Invoke(new MethodInvoker(delegate { // wrap to make thread-safe
				System.Windows.Forms.Application.OpenForms[0].BeginInvoke(new MethodInvoker(
						delegate { // wrap to make thread-safe
							double timeScale = speed * sleep / (1000 * 60);
							transformStep = Iterate(timeScale * 2 * Math.PI, step);
							if (!isCenteredOnSelection)
								Window.ActiveWindow.SetProjection(transformStep * Window.ActiveWindow.Projection, false, false);
							else {
								Matrix translation = Matrix.CreateTranslation(Window.ActiveWindow.Projection * Center.Vector);
								Window.ActiveWindow.SetProjection(translation * transformStep * translation.Inverse * Window.ActiveWindow.Projection, false, false);
							}
							if (IsChangingColors) {
								//	Command.GetCommand("AEColorsVaryHue").Execute();
								WriteBlock.ExecuteTask("Set Colors",
									delegate {
										foreach (IDesignBody iDesBody in part.GetDescendants<IDesignBody>()) {
											HSBColor HsbColor = new HSBColor(iDesBody.Master.GetVisibleColor());
											HsbColor.H += (float) 360 * (float) timeScale;
											iDesBody.Master.SetColor(null, HsbColor.Color);
										}
									}
								);
							}
							if (IsSavingImages) {
								if (step - savingImagesStartStep < 1 / timeScale)
									WriteBlock.ExecuteTask("Save PNG",
										delegate {
											Window.ActiveWindow.Export(WindowExportFormat.Png, System.IO.Path.GetTempPath() + step.ToString("TumbleFrame{0000.}.png"));
										}
									);
							}
							step++;
						}
				));

				Thread.Sleep(sleep);
			}
		}

		void SetCenter() {
			if (Window.ActiveWindow.ActiveContext.Selection.Count > 0) {
				Box box = Box.Empty;
				foreach (IDocObject docObject in Window.ActiveWindow.ActiveContext.Selection) {
					IBounded bounded = docObject as IBounded;
					if (bounded != null)
						box |= bounded.GetBoundingBox(Matrix.Identity);
				}
				center = box.Center;
			}
			else
				center = Point.Origin;
		}

	}
}
