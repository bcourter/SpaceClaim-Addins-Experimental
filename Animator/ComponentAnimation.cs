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

namespace SpaceClaim.AddIn.Animator {
	public class ComponentAnimation {
		Component component;
		List<AnimateStyle> animateStyles;
		Matrix initialPosition;

		public ComponentAnimation(Component component, List<AnimateStyle> animateStyles, Matrix initialPosition) {
			this.component = component;
			this.animateStyles = animateStyles;
			this.initialPosition = initialPosition;
		}

		public Matrix InitialPosition {
			get { return initialPosition; }
			set { initialPosition = value; }
		}

		public List<AnimateStyle> AnimateStyles {
			get { return animateStyles; }
			set { animateStyles = value; }
		}

		public Matrix GetTransform(double time) {
			Matrix transform = initialPosition;
			foreach (AnimateStyle animateStyle in animateStyles) {
				transform *= animateStyle.GetTransform(time);
			}
			return transform;
		}

		public void AddAnimateStyle(AnimateStyle animateStyle) {
			animateStyles.Add(animateStyle);
		}
	}
}
