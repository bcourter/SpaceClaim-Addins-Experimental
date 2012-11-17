using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;

namespace SpaceClaim.AddIn.AETools {
	static class ZBitmap {
		const double inchesToMeters = 25.4 / 1000;
		static double resolution = (double)1 / 150 * inchesToMeters;

		public static void Initialize() {
			Command command;

			command = Command.Create("AEZBitmap");
			command.Text = "Z Bitmap";
			command.Hint = "Create an object whose bounding box will be used to extract Z height information of visible bodies and save that to a bitmap.";
			command.Executing += ZBitmap_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
		}

		static void ZBitmap_Executing(object sender, EventArgs e) {
			Window activeWindow = Window.ActiveWindow;
			IDesignBody boxIDesBody = activeWindow.GetAllSelectedIDesignBodies().FirstOrDefault();
			if (boxIDesBody == null)
				return;
	
			SaveFileDialog dialog = new SaveFileDialog();
			dialog.Filter = "PNG Files (*.png)|*.png";
			DialogResult result = dialog.ShowDialog();

			if (result != DialogResult.OK)
				return;

			string fileName = dialog.FileName;

			Box box = boxIDesBody.GetBoundingBox(Matrix.Identity);
			int xCount = (int)(box.Size.X / resolution);
			int yCount = (int)(box.Size.Y / resolution);

			double min = box.MinCorner.Z;
			double max = box.MaxCorner.Z;

			System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(xCount, yCount);
			for (int i = 0; i < xCount; i++) {
				double x = (double)i * resolution;

				for (int j = 0; j < yCount; j++) {
					double y = (double)j * resolution;
					CurveSegment ray = CurveSegment.Create(
						Line.Create(Point.Create(x, y, 0), Direction.DirZ),
						Interval.Create(-1000, 1000) // Interval.Create(double.MinValue, double.MaxValue) throws an exception when we calculate the intersections
					);

					foreach (IPart iPart in (activeWindow.Scene.Root as Part).WalkParts()) {
						foreach (IDesignBody iDesBody in iPart.Bodies) {
							if (iDesBody.IsVisible(null) == false)
								continue;

							if (iDesBody.Master == boxIDesBody)
								continue;

							foreach (IDesignFace iDesFace in iDesBody.Faces) {
								ICollection<IntPoint<SurfaceEvaluation, CurveEvaluation>> intersections = iDesFace.Shape.IntersectCurve(ray);
								double maxZ = double.MinValue;
								foreach (IntPoint<SurfaceEvaluation, CurveEvaluation> intersection in intersections)
									maxZ = Math.Max(intersection.Point.Z, maxZ);

								int intensity = (int)(255 * Interpolation.Clamp(min, max, maxZ, 0, 1));
								bitmap.SetPixel(i, yCount - j - 1, System.Drawing.Color.FromArgb(intensity, intensity, intensity));
							}

						}
					}
				}
			}

			bitmap.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
		}

	}
}
