using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Drawing;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;
using Application = SpaceClaim.Api.V10.Application;
using Point = SpaceClaim.Api.V10.Geometry.Point;

namespace SpaceClaim.AddIn.AETools {
	static class ImageSurface {
		const double stepSize = 0.001;

		const string notesImagePlanarCommandName = "AEImagePlanar";
		const string notesImageCylindricalCommandName = "AEImageCylindrical";
		const string notesImageSphericalCommandName = "AEImageSpherical";

        public static void Initialize() {
			Command command;

			command = Command.Create(notesImagePlanarCommandName);
			command.Text = "Create Planar Image Surfaces";
			command.Hint = "Create many surfaces to resemble a bitmap.";
			command.Executing += notesImagePlanar_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;

			command = Command.Create(notesImageCylindricalCommandName);
			command.Text = "Create Clyndrical Image Surfaces";
			command.Hint = "Create many surfaces to resemble a bitmap.";
			command.Executing += notesImageCylindrical_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;

			command = Command.Create(notesImageSphericalCommandName);
			command.Text = "Create Spherical Image Surfaces";
			command.Hint = "Create many surfaces to resemble a bitmap.";
			command.Executing += notesImageSpherical_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
        }

		static void notesImagePlanar_Executing(object sender, EventArgs e) {
			Bitmap bitmap = OpenBitmap();
            if (bitmap == null)
                return;

            Part part = CreateImagePart();

			Point[] points = new Point[4];
			for (int i = 0; i < bitmap.Width; i++) {
				for (int j = 0; j < bitmap.Height; j++) {
					points[0] = Point.Create((i + 0) * stepSize, (j + 0) * stepSize, 0);
					points[1] = Point.Create((i + 1) * stepSize, (j + 0) * stepSize, 0);
					points[2] = Point.Create((i + 1) * stepSize, (j + 1) * stepSize, 0);
					points[3] = Point.Create((i + 0) * stepSize, (j + 1) * stepSize, 0);

					DesignBody designBody = ShapeHelper.CreatePolygon(points, Plane.PlaneXY, 0, part);
                    designBody.SetColor(null, GetOpaquePixel(bitmap, i, j));
                }
			}
		}

		static void notesImageCylindrical_Executing(object sender, EventArgs e) {
            Bitmap bitmap = OpenBitmap();
            if (bitmap == null)
                return;

            Part part = CreateImagePart();
            
            int width = bitmap.Width;
			double radius = 0.5 * stepSize / Math.Sin(Math.PI / width) * Math.PI;

			Point[] points = new Point[4];
			for (int i = 0; i < width; i++) {
					double angle1 = (double) i / width * 2 * Math.PI;
					double angle2 = (double) (i + 1) / width * 2 * Math.PI;
				for (int j = 0; j < bitmap.Height; j++) {
					double x1 = Math.Sin(angle1) * radius;
					double y1 = Math.Cos(angle1) * radius;
					double z1 = j * stepSize;

					double x2 = Math.Sin(angle2) * radius;
					double y2 = Math.Cos(angle2) * radius;
					double z2 = (j + 1) * stepSize;

					points[0] = Point.Create(x1, y1, z1);
					points[1] = Point.Create(x1, y1, z2);
					points[2] = Point.Create(x2, y2, z2);
					points[3] = Point.Create(x2, y2, z1);

					DesignBody designBody = ShapeHelper.CreatePolygon(points, null, 0, part);
                    designBody.SetColor(null, GetOpaquePixel(bitmap, i, j));
                }
			}
		}

		static void notesImageSpherical_Executing(object sender, EventArgs e) {
            Bitmap bitmap = OpenBitmap();
            if (bitmap == null)
                return;

            Part part = CreateImagePart();

			int width = bitmap.Width;
			int height = bitmap.Height;
			double radius = 0.5 * stepSize / Math.Sin(Math.PI / width) * Math.PI;

			Point startPoint = Point.Create(radius, 0, 0);
			Line yAxis = Line.Create(Point.Origin, Direction.DirY);
			Line zAxis = Line.Create(Point.Origin, Direction.DirZ);

            List<Point> points = new List<Point>(4);
            for (int i = 0; i < 4; i++)
                points.Add(Point.Origin);

            for (int i = 0; i < width; i++) {
				double angle1 = (double) i / width * 2 * Math.PI;
				double angle2 = (double) (i + 1) / width * 2 * Math.PI;
                for (int j = 0; j < height; j++) {
                    double azimuth1 = ((double)j) / height * Math.PI - Math.PI / 2;
                    double azimuth2 = ((double)j + 1) / height * Math.PI - Math.PI / 2;

                    points[0] = Matrix.CreateRotation(zAxis, angle1) * Matrix.CreateRotation(yAxis, azimuth1) * startPoint;
                    points[1] = Matrix.CreateRotation(zAxis, angle1) * Matrix.CreateRotation(yAxis, azimuth2) * startPoint;
                    points[2] = Matrix.CreateRotation(zAxis, angle2) * Matrix.CreateRotation(yAxis, azimuth2) * startPoint;
                    points[3] = Matrix.CreateRotation(zAxis, angle2) * Matrix.CreateRotation(yAxis, azimuth1) * startPoint;

                    Point? extraPoint = null;
                    if (Accuracy.Equals(points[3], points[0])) {
                        extraPoint = points[0];
                        points.Remove(points[0]);
                    }
                    else {
                        for (int k = 0; k < 3; k++) {
                            if (Accuracy.Equals(points[k], points[k + 1])) {
                                extraPoint = points[k];
                                points.Remove(points[k]);
                                break;
                            }
                        }
                    }

					DesignBody designBody = ShapeHelper.CreatePolygon(points, null, 0, part);
                    designBody.SetColor(null, GetOpaquePixel(bitmap, i, j));

                    if (extraPoint != null)
                        points.Add(extraPoint.Value);
                }
			}
		}

        static Bitmap OpenBitmap() {
            Bitmap bitmap = null;
            Debug.Assert(Window.ActiveWindow != null, "Window.ActiveWindow != null");

			SpaceClaim.Api.V10.Extensibility.AddIn.ExecuteWindowsFormsCode(delegate {
                using (OpenFileDialog fileDialog = new OpenFileDialog()) {
                    fileDialog.Filter = "PNG (*.png)|*.png|JPEG *.jpg)|*.jpg";
                    fileDialog.Title = "Open Image";

                    if (fileDialog.ShowDialog(Application.MainWindow) != DialogResult.OK)
                        return;

                    bitmap = (Bitmap)Bitmap.FromFile(fileDialog.FileName);
                }
            });

            return bitmap;
		}

        static Color GetOpaquePixel(Bitmap bitmap, int x, int y) {
            Color color = bitmap.GetPixel(x, y);
            if (color.A < 222)
                color = Color.White;
            else
                color = Color.FromArgb(0, color.R, color.G, color.B);

            return color;
        }

		static Part CreateImagePart() {
			Window activeWindow = Window.ActiveWindow;
			Part mainPart = activeWindow.Scene as Part;
            Part imagePart = Part.Create(mainPart.Document, "Image Surface");
			Component imageComponent = Component.Create(mainPart, imagePart);
			return imagePart;
		}
	}
}
