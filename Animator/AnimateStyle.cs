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
	public abstract class AnimateStyle {
		public abstract Matrix GetTransform(double time);
	}

	public class AnimateRotateStyle : AnimateStyle {
		Plane rotatePlane;
		double speed; // rotations per minute

		public AnimateRotateStyle(Plane rotatePlane, double speed) {
			this.rotatePlane = rotatePlane;
			this.speed = speed;
		}

		public override Matrix GetTransform(double time) {
			return Matrix.CreateRotation(Line.Create(rotatePlane.Frame.Origin, rotatePlane.Frame.DirZ), time * speed / 60 * Math.PI / 2);
		}
	}

	public class AnimateTranslateStyle : AnimateStyle {
		Plane translatePlane;
		double speed;  // mm per second

		public AnimateTranslateStyle(Plane translatePlane, double speed) {
			this.translatePlane = translatePlane;
			this.speed = speed;
		}

		public override Matrix GetTransform(double time) {
			return Matrix.CreateTranslation(translatePlane.Frame.DirZ * (time * speed / 1000 * Math.PI / 2));
		}
	}

	public class AnimateParametricStyle : AnimateStyle {
		Direction direction = Direction.Zero;
		string function;
		
		public AnimateParametricStyle(string axis, string function) {
			if (axis == "x")
				direction = Direction.DirX;

			if (axis == "y")
				direction = Direction.DirY;

			if (axis == "z")
				direction = Direction.DirZ;

			this.function = function;
		}

		public override Matrix GetTransform(double time) {
						Window window = Window.ActiveWindow;

			string functionToParse = function.Replace("time", time.ToString());
			double distance = 0;

			if (window.TryParseLength(functionToParse, out distance))
				return Matrix.CreateTranslation(direction * distance); // TBD nomalize units if not in mm

			Debug.Assert(false, "Could not parse function: " + functionToParse);
			return Matrix.Identity;
		}

		//public class AnimateGravityStyle : AnimateStyle {
		//    double mass;  // kg
		//    double radius;  // m
		//    ICollection<IPart> masses;
		//    const double G = 6.67300e-11;  // m3 kg-1 s-2

		//    public AnimateGravityStyle(ICollection<IPart> masses) {
		//        this.masses = masses;
		//    }

		//    public override Matrix GetTransform(double time) {
		//        Vector force = Vector.Zero;

		//        return Matrix.CreateTranslation(translatePlane.Frame.DirZ * (time * strength / 1000 * Math.PI / 2));
		//    }

		//    public static bool tryParseGravityLine(string text) {

		//    }
		//}

		//public class AnimateMagnetStyle : AnimateStyle {
		//    double stength;  // farady
		//    ICollection<IPart> magnets;

		//    public AnimateMagnetStyle(double stength, ICollection<IPart> magnets) {
		//        this.stength = stength;
		//        this.magnets = magnets;
		//    }

		//    public override Matrix GetTransform(double time) {
		//        return Matrix.CreateTranslation(translatePlane.Frame.DirZ * (time * strength / 1000 * Math.PI / 2));
		//    }
		//}


	}
}
