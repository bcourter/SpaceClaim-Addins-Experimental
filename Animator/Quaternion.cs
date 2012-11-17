using System;
using System.Collections.Generic;
using System.Text;
using SpaceClaim.Api.V3.Geometry;

namespace AETools {
	class Quaternion {
		double t, x, y, z;

		public Quaternion(double t, double x, double y, double z) {
			this.t = t;
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public Quaternion(double s, Vector v)
			: this(s, v.X, v.Y, v.Z) {
		}

		public static Quaternion Create(double t, double x, double y, double z) {
			return new Quaternion(t, x, y, z);
		}

		public static Quaternion Create(double s, Vector v) {
			return new Quaternion(s, v);
		}

		public static Quaternion CreateRotation(double angle, Direction axis) {
			return new Quaternion(Math.Cos(angle / 2), axis.UnitVector * Math.Sin(angle / 2));
		}

		//public static Quaternion CreateRotation(Matrix matrix) {
		//        return CreateRotation(matrix.RotationElement.);
		//}

		public double T {
			get { return t; }
			set { t = value; }
		}

		public double X {
			get { return x; }
			set { x = value; }
		}

		public double Y {
			get { return y; }
			set { y = value; }
		}

		public double Z {
			get { return z; }
			set { z = value; }
		}

		public double Scalar {
			get { return t; }
			set { t = value; }
		}

		public Vector Vector {
			get { return Vector.Create(x, y, z); }
			set {
				x = value.X;
				y = value.Y;
				z = value.Z;
			}
		}

		public static Quaternion operator -(Quaternion a) {
			return new Quaternion(-a.T, -a.X, -a.Y, -a.Z);
		}

		public static Quaternion operator +(Quaternion a, Quaternion b) {
			return new Quaternion(a.T + b.T, a.X + b.X, a.Y + b.Y, a.Z + b.Z);
		}

		public static Quaternion operator *(Quaternion a, Quaternion b) {
			return new Quaternion(
				a.T * b.T - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
				a.T * b.X + a.X * b.T + a.Y * b.Z - a.Z * b.Y,
				a.T * b.Y - a.X * b.Z + a.Y * b.T + a.Z * b.X,
				a.T * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.T
				);
		}

		public static Quaternion operator *(double s, Quaternion a) {
			return new Quaternion(a.T * s, a.X * s, a.Y * s, a.Z * s);
		}

		public static Quaternion operator *(Quaternion a, double s) {
			return new Quaternion(a.T * s, a.X * s, a.Y * s, a.Z * s);
		}

		public static Quaternion operator /(Quaternion a, double divisor) {
			return a * (double) 1 / divisor;
		}

		public Quaternion Conjugate {
			get { return new Quaternion(t, -x, -y, -z); }
		}

		public Quaternion Inverse {
			get { return Conjugate / (this * Conjugate).Scalar; }
		}

		public double Magnitude {
			get { return Math.Sqrt(t * t + x * x + y * y + z * z); }
		}

		public override string ToString() {
			return T.ToString() + " + " + X.ToString() + "i + " + Y.ToString() + "j + " + Z.ToString() + "k";
		}

	}
}
