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
	static class Freeze {
		static Freezer freezer; 
		const string freezeCommandName = "AEFreeze";

		public static void Initialize() {
			Command command;

			freezer = new Freezer();

			command = Command.Create(freezeCommandName);
			command.Text = "Freeze";
			command.Image = Resources._2DMoveTool24;
			command.Hint = "Make the transformation of the selected bodies view-invariant.  Warning: zooming may blow your world apart.";
			command.Executing += freeze_Executing;
			command.Updating += freeze_Updating;
		}
		
		public static void Disconnect() {
			freezer.Stop();
		}

		static void freeze_Executing(object sender, EventArgs e) {
			if (freezer.IsFrozen)
				freezer.Stop();
			else
				freezer.Start();
		}

		static void freeze_Updating(object sender, EventArgs e) {
			Command command = (Command) sender;
			command.IsChecked = freezer.IsFrozen;
			command.IsEnabled = true;
		}
	}

	class Freezer {
		Matrix startViewTrans;
		Matrix lastTrans;
		Thread freezeThread;
		EventWaitHandle exitThreadEvent = new ManualResetEvent(false);

		public Freezer() {
		}

		public bool IsFrozen {
			get { return freezeThread != null; }
		}

		public Matrix StartViewTrans {
			get { return startViewTrans; }
		}

		public Matrix LastTrans {
			get { return lastTrans; }
			set { lastTrans = value; }
		}

		public void Start() {
			startViewTrans = Window.ActiveWindow.Projection;
			lastTrans = Matrix.Identity;
			if (!IsFrozen) {
				freezeThread = new Thread(new ThreadStart(FreezeThread));
				freezeThread.Start();
			}
		}

		public void Stop() {
			try {
				if (IsFrozen) {
					exitThreadEvent.Set();
					freezeThread.Join();		// wait for thread to exit
					exitThreadEvent.Reset();
				}
			}
			finally {
				freezeThread = null;
			}
		}

		void FreezeThread() {
			WaitHandle[] waitHandles = new WaitHandle[] { exitThreadEvent };
			int step = 0;
			int sleep = 100;  // 10 frames per second
			while (EventWaitHandle.WaitAny(waitHandles, 0, false) != 0) {
				System.Windows.Forms.Application.OpenForms[0].BeginInvoke(new MethodInvoker(delegate { // wrap to make thread-safe
					WriteBlock.ExecuteTask("Iterate Freeze",
						delegate {
							foreach (IDocObject docObject in Window.ActiveWindow.ActiveContext.Selection) {
								ITransformable geometry = docObject as ITransformable;
								//if (geometry == null)
								//    geometry = docObject.GetParent<IDesignBody>();
								if (geometry == null)
									return;
								Matrix newTrans = Window.ActiveWindow.Projection.Inverse * StartViewTrans;
								geometry.Transform(newTrans * LastTrans.Inverse);
								LastTrans = newTrans;
							}
						});
				}));
				Thread.Sleep(sleep);
				step++;
			}
		}

	}
}
