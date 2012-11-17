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

namespace SpaceClaim.AddIn.Animator {
	class Animator {
		Thread animateThread;
		EventWaitHandle exitThreadEvent = new ManualResetEvent(false);
		DateTime startTime;

		List<string> animatePropertyNames = new List<string>();
		Dictionary<Component, ComponentAnimation> componentAnimations = new Dictionary<Component, ComponentAnimation>();



		public Animator() {
			animatePropertyNames.Add("Rotate");
			animatePropertyNames.Add("Translate");
			animatePropertyNames.Add("X");
			animatePropertyNames.Add("Y");
			animatePropertyNames.Add("Z");
			animatePropertyNames.Add("Gravity");
		}

		public bool IsPlaying {
			get { return animateThread != null; }
		}

		public void Start() {
			UpdateAnimationStyles();

			if (!IsPlaying) {
				animateThread = new Thread(new ThreadStart(AnimateThread));
				animateThread.Start();
			}
		}

		public void UpdateAnimationStyles() {
			foreach (Component component in (Window.ActiveWindow.Scene as Part).Components) {
				Part template = component.Template;
				if (template == template.Document.MainPart) {

					AnimateStyle animateStyle = null;
					List<AnimateStyle> animateStyles = new List<AnimateStyle>();
					foreach (string name in template.Document.CustomProperties.Keys) {
						if (animatePropertyNames.Contains(name)) {
							animateStyle = ParseAnimateProperty(name, template.Document.CustomProperties[name].Value as string, component);
							if (animateStyle != null)
								animateStyles.Add(animateStyle);
						}
					}

					if (animateStyles.Count > 0) 
						componentAnimations[component] = new ComponentAnimation(component, animateStyles, component.Placement); 
				}
			}
		}

		AnimateStyle ParseAnimateProperty(string style, string value, Component component) {
			style = style.ToLowerInvariant();
			switch (style) {
				case "rotate":
				case "translate": 
						Match match = Regex.Match(value, @"^(\w*)\s*([-\d\.]*)");
						if (match.Success) {
							string planeString = match.Groups[1].Value;
							string speedString = match.Groups[2].Value;

							Plane plane = null;
							foreach (DatumPlane datumPlane in component.Template.DatumPlanes) {
								if (datumPlane.Name == planeString)
									plane = datumPlane.Shape.Geometry;
							}
							if (plane == null)
								return null;

							double speed = 0;
							if (!double.TryParse(speedString, out speed))
								return null;

							if (style == "rotate")
								return new AnimateRotateStyle(plane, speed);

							if (style == "translate")
								return new AnimateTranslateStyle(plane, speed);

							return null;
						}
						break;
					

				case "x":
				case "y":
				case "z":
					return new AnimateParametricStyle(style, value);

				//case "gravity":
				//    match = Regex.Match(value, @"^\s*([-\d\.]*)\s*([-\d\.]*)");
				//    if (match.Success) {
				//        string massString = match.Groups[1].Value;
				//        string radiusString = match.Groups[2].Value;

				//        double mass = 0;
				//        if (!double.TryParse(massString, out mass))
				//            return null;

				//        double radius = 0;
				//        if (!double.TryParse(radiusString, out radius))
				//            return null;

				//        if (style == "gravity")
				//            return new AnimateGravityStyle(plane, speed);

				//        return null;
				//    }
				//    break;
			}

			return null;
		}

		public void Stop() {
			try {
				if (IsPlaying) {
					exitThreadEvent.Set();
					animateThread.Join();		// wait for thread to exit
					exitThreadEvent.Reset();
				}
			}
			finally {
				animateThread = null;
			}
		}

		public void Reset() {
			WriteBlock.ExecuteTask("Reset Animation",
								delegate {
									startTime = DateTime.Now;

									foreach (Component component in componentAnimations.Keys) {
										component.Placement = componentAnimations[component].InitialPosition;
									}

								});
		}

		void AnimateThread() {
			WaitHandle[] waitHandles = new WaitHandle[] { exitThreadEvent };
			int step = 0;
			startTime = DateTime.Now;

			int sleep = 100;  // 10 frames per second
			while (EventWaitHandle.WaitAny(waitHandles, 0, false) != 0) {
				System.Windows.Forms.Application.OpenForms[0].BeginInvoke(new MethodInvoker(delegate { // wrap to make thread-safe
					WriteBlock.ExecuteTask("Iterate Animation",
						delegate {
							double time = (DateTime.Now - startTime).TotalSeconds;

							foreach (Component component in componentAnimations.Keys) {
								component.Placement = Matrix.Identity;
								component.Transform(componentAnimations[component].GetTransform(time));
							}

						});
				}));
				Thread.Sleep(sleep);
				step++;
			}
		}

	}
}
