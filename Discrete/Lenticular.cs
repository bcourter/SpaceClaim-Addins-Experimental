using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.Api.V10.Display;
using SpaceClaim.AddInLibrary;
using Discrete.Properties;
using Color = System.Drawing.Color;
using Application = SpaceClaim.Api.V10.Application;
using Point = SpaceClaim.Api.V10.Geometry.Point;

namespace SpaceClaim.AddIn.Discrete {
	public abstract class LenticularPropertiesCapsule : RibbonButtonCapsule {
		protected double sweepAngle = (double) 6 * Math.PI / 180;
		protected int interlaceCount = 6;
		protected int interlaceWidth = 1;

		protected string fileName;
		protected Window activeWindow;
		protected Matrix originalWindowTrans;
		protected Line screenY;
		protected Bitmap interlaced;
		protected int width;
		protected int height;

		public LenticularPropertiesCapsule(string name, string text, System.Drawing.Image image, string hint, RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base(name, text, image, hint, parent, buttonSize) {

			Values[Resources.LenticularSweepAngle] = new RibbonCommandValue(sweepAngle * 180 / Math.PI);
			Values[Resources.LenticularInterlaceCount] = new RibbonCommandValue(interlaceCount);
			Values[Resources.LenticularInterlaceWidth] = new RibbonCommandValue(interlaceWidth);
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			sweepAngle = Values[Resources.LenticularSweepAngle].Value * Math.PI / 180;
			interlaceCount = (int) Values[Resources.LenticularInterlaceCount].Value;
			interlaceWidth = (int) Values[Resources.LenticularInterlaceWidth].Value;

			SaveFileDialog dialog = new SaveFileDialog();
			dialog.Filter = "PNG Files (*.png)|*.png";
			DialogResult result = dialog.ShowDialog();

			if (result != DialogResult.OK)
				return;

			fileName = dialog.FileName;

			activeWindow = Window.ActiveWindow;
			originalWindowTrans = activeWindow.Projection;
			screenY = Line.Create(Point.Origin, originalWindowTrans.Inverse * Direction.DirY);

			string file = GetEnumeratedFileName(0);
			activeWindow.Export(WindowExportFormat.Png, file);
			Bitmap bitmap = (Bitmap) Bitmap.FromFile(file);

			width = bitmap.Width;
			height = bitmap.Height;
		}

		protected void EndExecute() {
			interlaced.Save(fileName);
			activeWindow.SetProjection(originalWindowTrans, false, false);
		}

		protected string GetEnumeratedFileName(int i) {
			return String.Format("{0}-{1}.png", Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName)), i);
		}
	}

	class LenticularPlanarCommandCapsule : LenticularPropertiesCapsule {
		public LenticularPlanarCommandCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("LenticularPlanar", Resources.LenticularPlanarCommandText, null, Resources.LenticularPlanarCommandHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			base.OnExecute(command, context, buttonRect);
			if (fileName == null)
				return;

	//		interlaced = new Bitmap(width * interlaceCount, height * interlaceCount);
			interlaced = new Bitmap(width * interlaceCount, height);

			Bitmap[] bitmaps = new Bitmap[interlaceCount];
			for (int i = 0; i < interlaceCount; i++) {
				double angle = (double) i / (interlaceCount - 1) * sweepAngle - sweepAngle / 2;
				Matrix rotation = Matrix.CreateRotation(screenY, angle);
				activeWindow.SetProjection(originalWindowTrans * rotation, false, false);

				string file = GetEnumeratedFileName(i+1);
				activeWindow.Export(WindowExportFormat.Png, file);
				bitmaps[i] = (Bitmap) Bitmap.FromFile(file);
			}

			for (int i = 0; i < width * interlaceCount; i++) {
				for (int j = 0; j < height ; j++) {
//				for (int j = 0; j < height * interlaceCount; j++) {
					if (i / (interlaceWidth * interlaceCount) * interlaceWidth + i % interlaceWidth < width)
						interlaced.SetPixel(i, j, bitmaps[(i / interlaceWidth) % interlaceCount].GetPixel(i / (interlaceWidth * interlaceCount) * interlaceWidth + i % interlaceWidth, j));
					//interlaced.SetPixel(i, j, bitmaps[(i / interlaceWidth) % interlaceCount].GetPixel(i / (interlaceWidth * interlaceCount) * interlaceWidth + i % interlaceWidth, j / interlaceCount));
				}
			}

			EndExecute();
		}
	}

	class LenticularCylindricalCommandCapsule : LenticularPropertiesCapsule {
		public LenticularCylindricalCommandCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("LenticularCylindrial", Resources.LenticularCylindricalCommandText, null, Resources.LenticularCylindricalCommandHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			base.OnExecute(command, context, buttonRect);
			if (fileName == null)
				return;

			int lenticules = (int) Math.Ceiling(Math.PI * width / interlaceWidth);
			int finalWidth = lenticules * interlaceCount * interlaceWidth;
			int finalHeight = height;
			//int finalHeight = height * interlaceCount;
			interlaced = new Bitmap(finalWidth, finalHeight);

			string file = GetEnumeratedFileName(1);
			for (int i = 0; i < finalWidth; i++) {
				int lenticule = i / (interlaceWidth * interlaceCount);
				double alpha = (double) lenticule * 2 * Math.PI / lenticules;

				int lenticuleIndex = i % (interlaceWidth * interlaceCount);
				int interlaceIndex = lenticuleIndex / interlaceWidth;
				int widthIndex = lenticuleIndex % interlaceWidth;

				double beta = (double) interlaceIndex / (interlaceCount - 1) * sweepAngle - sweepAngle / 2;
				beta *= -1;
				double h = (double) width / 2 * Math.Sin(beta);
				int xPixel = width / 2 + (int) (h + (double) widthIndex - (double) interlaceWidth / 2);
				xPixel = Math.Max(0, Math.Min(finalWidth - 1, xPixel));

				Matrix rotation = Matrix.CreateRotation(screenY, alpha + beta);
				activeWindow.SetProjection(originalWindowTrans * rotation, false, false);
				activeWindow.Export(WindowExportFormat.Png, file);

				Bitmap bitmap = (Bitmap) Bitmap.FromFile(file);

				for (int j = 0; j < finalHeight; j++) {
					interlaced.SetPixel(i, j, bitmap.GetPixel(xPixel, j));
				//	interlaced.SetPixel(i, j, bitmap.GetPixel(xPixel, j / interlaceCount));
				}

				bitmap.Dispose();
			}

			EndExecute();
		}
	}

}