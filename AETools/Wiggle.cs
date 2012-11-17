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
using AETools.Properties;

namespace SpaceClaim.AddIn.AETools {
	static class Wiggle {
		static Wiggler wiggler; 
		const string wiggleCommandName = "AEWiggle";

		public static void Initialize() {
			Command command;

			wiggler = new Wiggler();

			command = Command.Create(wiggleCommandName);
			command.Text = "Wiggle";
			command.Hint = "Toggle wiggling on and off";
			command.Image = Resources._2DPullTool24;
			command.Executing += wiggle_Executing;
			command.Updating += wiggle_Updating;
		}
		
		public static void Disconnect() {
			wiggler.Stop();
		}

		static void wiggle_Executing(object sender, EventArgs e) {
			if (wiggler.IsWiggling)
				wiggler.Stop();
			else
				wiggler.Start();
		}

		static void wiggle_Updating(object sender, EventArgs e) {
			Command command = (Command) sender;
			command.IsEnabled = true;
			command.IsChecked = wiggler.IsWiggling;
		}
	}

	class Wiggler {
		//Matrix startViewTrans;
		//Matrix lastTrans;
		Group wiggleGroup = null;
		String wiggleGroupName = "Wiggler";
		double wiggleInitialValue;
		double wiggleAmplitude = 0.01;
		double wiggleFrequency = 5;

		Thread wiggleThread;
		EventWaitHandle exitThreadEvent = new ManualResetEvent(false);

		public Wiggler() {
		}

		public bool IsWiggling {
			get { return wiggleThread != null; }
		}

		//public Matrix StartViewTrans {
		//    get { return startViewTrans; }
		//}

		//public Matrix LastTrans {
		//    get { return lastTrans; }
		//    set { lastTrans = value; }
		//}

		public void Start() {
			//startViewTrans = Window.ActiveWindow.Projection;
			//lastTrans = Matrix.Identity;
			foreach (Group group in Window.ActiveWindow.Groups) {
				if (group.Name == wiggleGroupName) {
					wiggleGroup = group;
					break;
				}
			}

			Debug.Assert(wiggleGroup != null, "Couldn't find a group called " + wiggleGroupName);

			DimensionType dimensionType;
			bool isValue = wiggleGroup.TryGetDimensionValue(out wiggleInitialValue, out dimensionType);
			Debug.Assert(isValue, wiggleGroupName + " does not contain a value");

			if (!IsWiggling) {
				wiggleThread = new Thread(new ThreadStart(WiggleThread));
				wiggleThread.Start();
			}
		}

		public void Stop() {
			try {
				if (IsWiggling) {
					exitThreadEvent.Set();
					wiggleThread.Join();		// wait for thread to exit
					exitThreadEvent.Reset();
				}
			}
			finally {
				wiggleThread = null;
				wiggleGroup.SetDimensionValue(wiggleInitialValue);
			}
		}

		void WiggleThread() {
			WaitHandle[] waitHandles = new WaitHandle[] { exitThreadEvent };
			int step = 0;
			DateTime startTime = DateTime.Now;

			int sleep = 200;  // 5 frames per second
			while (EventWaitHandle.WaitAny(waitHandles, 0, false) != 0) {
				System.Windows.Forms.Application.OpenForms[0].BeginInvoke(new MethodInvoker(delegate { // wrap to make thread-safe
					WriteBlock.ExecuteTask("Iterate Wiggle",
						delegate {
							double time = (DateTime.Now - startTime).TotalSeconds;
							wiggleGroup.SetDimensionValue(wiggleInitialValue + wiggleAmplitude * Math.Sin(time * 2 * Math.PI / wiggleFrequency));

							//foreach (IDocObject docObject in Window.ActiveWindow.Selection) {
							//    ITransformable geometry = docObject as ITransformable;
							//    //if (geometry == null)
							//    //    geometry = docObject.GetParent<IDesignBody>();
							//    if (geometry == null)
							//        return;
							//    Matrix newTrans = Window.ActiveWindow.Projection.Inverse * StartViewTrans;
							//    geometry.Transform(newTrans * LastTrans.Inverse);
							//    LastTrans = newTrans;
							//}
						});
				}));
				Thread.Sleep(sleep);
				step++;
			}
		}

	}
}
