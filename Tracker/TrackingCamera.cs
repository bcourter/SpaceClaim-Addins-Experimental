using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;

using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge.Vision.Motion;
using AForge.Video;
using AForge.Video.DirectShow;

using SpaceClaim.Api.V8;
using SpaceClaim.Api.V8.Geometry;
using Point = SpaceClaim.Api.V8.Geometry.Point;
using MatrixLibrary;

namespace SpaceClaim.AddIn.Tracker {
	public class TrackingCamera {
		ControlForm controlForm;
		string cameraName;
		VideoCaptureDevice videoSource = null;
		bool isRunning = false;
		Size imageSize;
		PictureBox imageDestination;

		const int minObjectSize = 5;
		const int maxObjectSize = 20;
		const int dotSize = 1;
	//	IMotionDetector iMotionDetector = new SimpleBackgroundModelingDetector(true);
	//	BlobCountingObjectsProcessing blobCountingObjectsProcessing = new BlobCountingObjectsProcessing();
	//	MotionDetector motionDetector;

		Vector a, b;

		static Color[] colorList = new Color[] { 
			Color.Red,
			Color.Yellow,
			Color.Green,
			Color.Cyan,
			Color.Blue,
			Color.Violet
		};

		public TrackingCamera(string cameraName, Size imageSize, ControlForm controlForm) {
			this.cameraName = cameraName;
			this.imageSize = imageSize;
			this.controlForm = controlForm;

			CalibrationPoints = new List<PointUV>();

			//blobCountingObjectsProcessing.HighlightMotionRegions = false;
			//motionDetector = new MotionDetector(iMotionDetector, blobCountingObjectsProcessing);

			Calibrate();

		}

		public void Start() {
			videoSource = new VideoCaptureDevice(cameraName);
			videoSource.NewFrame += new NewFrameEventHandler(Video_NewFrame);
			Stop();
			videoSource.DesiredFrameSize = imageSize;

			videoSource.DesiredFrameRate = 10;
			videoSource.Start();

			isRunning = true;

		}

		public void Stop() {
			if (!(videoSource == null)) {
				if (videoSource.IsRunning) {
					videoSource.SignalToStop();
					videoSource = null;
					isRunning = false;
				}
			}
		}

		public void Video_NewFrame(object sender, NewFrameEventArgs eventArgs) {
			UnmanagedImage image = UnmanagedImage.FromManagedImage((Bitmap) eventArgs.Frame.Clone());

			var extractChannel = new ExtractChannel(RGB.R);
			UnmanagedImage channel = extractChannel.Apply(image);
	//		UnmanagedImage originalRed = channel.Clone();
			if (true) {
				var threshold = new Threshold(200);
				threshold.ApplyInPlace(channel);

				////filter to convert RGB image to 8bpp gray scale for image processing 
				//IFilter gray_filter = new GrayscaleBT709();
				//gray_image = gray_filter.Apply(gray_image);

				////thresholding a image
				//Threshold th_filter = new Threshold(color_data.threshold);
				//th_filter.ApplyInPlace(gray_image);

				//erosion filter to filter out small unwanted pixels 
				Erosion3x3 erosion = new Erosion3x3();
				erosion.ApplyInPlace(channel);

				//dilation filter
				//Dilatation3x3 dilatation = new Dilatation3x3();
				//dilatation.ApplyInPlace(channel);

				//GrayscaleToRGB filter = new GrayscaleToRGB();
				//image = filter.Apply(channel);

				//ReplaceChannel replaceFilter = new ReplaceChannel(RGB.B, channel);
				//replaceFilter.ApplyInPlace(image);
			}

			BlobCounter bc = new BlobCounter();
			//arrange blobs by area
			bc.ObjectsOrder = ObjectsOrder.Area;
			bc.FilterBlobs = true;
			bc.MinHeight = minObjectSize;
			bc.MinWidth = minObjectSize;
			bc.MaxHeight = maxObjectSize;
			bc.MaxWidth = maxObjectSize;

			//process image for blobs
			bc.ProcessImage(channel);
			channel.Dispose();

			//	if (motionDetector.ProcessFrame(image) > 0.02) {
			//	for (int i = 0; i < blobCountingObjectsProcessing.ObjectRectangles.Length; i++) {
			Rectangle[] rectangles = bc.GetObjectsRectangles();
			Blob[] blobs = bc.GetObjectsInformation(); 
			for (int i = 0; i < bc.ObjectsCount; i++) {
				Rectangle rectangle = rectangles[i];
				int width = rectangle.Width;
				int height = rectangle.Height;

		//		if (width < maxObjectSize && height < maxObjectSize && width > minObjectSize && height > minObjectSize) {
					Drawing.Rectangle(image, rectangle, colorList[i % colorList.Length]);

					if (i == 0) {
						Position = GetCenterOfMass(image, rectangle);
						Drawing.FillRectangle(image, rectangle, Color.BlanchedAlmond);
						Drawing.FillRectangle(image, new Rectangle((int) Position.U - dotSize, (int) Position.V - dotSize, dotSize * 3, dotSize * 3), Color.Indigo);
					}
		//		}
			}
			//	}

			Image = image.ToManagedImage();
			//	videoForm.ImageDestination.Image = image.ToManagedImage();
		}

		public Line GetLine(PointUV pointUV) {
			var unitMatrix = new NMatrix(new double[,] { { pointUV.U }, { pointUV.V }, { 1 } });
			NMatrix solution;
			if (!NMatrix.TryGaussJordanElimination(Homography, unitMatrix, out solution))
				throw new ArgumentException();

			//a = Vector.Create(solution[0, 3], solution[1, 3], solution[2, 3]);
			//b = Vector.Create(solution[0, 4], solution[1, 4], solution[2, 4]);

			////var vector = Vector.Create(pointUV.U * a.X, pointUV.V * a.Y, a.Z);
			//return Line.Create(Point.Origin + a, b.Direction);

			return Line.Create(Point.Origin, Direction.Create(
				solution[0, 4] / (solution[0, 3] + 1),
				solution[1, 4] / (solution[1, 3] + 1),
				solution[2, 4] / (solution[2, 3] + 1)
			));
		}

		public Line GetLine() {
			return GetLine(Position);
		}

		public void RecordCalibrationPoint() {
			CalibrationPoints.Add(Position);
		}

		public void Calibrate() {
			//CalibrationPoints.Add(PointUV.Create(-100, -100));
			//CalibrationPoints.Add(PointUV.Create(-100, 100));
			//CalibrationPoints.Add(PointUV.Create(100, -100));
			//CalibrationPoints.Add(PointUV.Create(100, 100));
			//CalibrationPoints.Add(PointUV.Create(-80, -80));
			//CalibrationPoints.Add(PointUV.Create(-80, 80));
			//CalibrationPoints.Add(PointUV.Create(80, -80));
			//CalibrationPoints.Add(PointUV.Create(80, 80));

			double h = (double)imageSize.Height;
			CalibrationPoints.Add(PointUV.Create(0, 0));
			CalibrationPoints.Add(PointUV.Create(0, h));
			CalibrationPoints.Add(PointUV.Create(h, 0));
			CalibrationPoints.Add(PointUV.Create(h, h));
			h *= 2;
			CalibrationPoints.Add(PointUV.Create(0, 0));
			CalibrationPoints.Add(PointUV.Create(0, h));
			CalibrationPoints.Add(PointUV.Create(h, 0));
			CalibrationPoints.Add(PointUV.Create(h, h));

			NMatrix calibrationMatrixA = new NMatrix(2 * CalibrationPoints.Count, 11);
			NMatrix calibrationMatrixB = new NMatrix(2 * CalibrationPoints.Count, 1);
			for (int i = 0; i < CalibrationPoints.Count; i++) {
				double x = controlForm.CalibrationPoints[i].X;
				double y = controlForm.CalibrationPoints[i].Y;
				double z = controlForm.CalibrationPoints[i].Z;
				double u = CalibrationPoints[i].U;
				double v = CalibrationPoints[i].V;

				calibrationMatrixA[2 * i, 0] = x;
				calibrationMatrixA[2 * i, 1] = y;
				calibrationMatrixA[2 * i, 2] = z;
				calibrationMatrixA[2 * i, 3] = 1;
				calibrationMatrixA[2 * i, 8] = -u * x;
				calibrationMatrixA[2 * i, 9] = -u * y;
				calibrationMatrixA[2 * i, 10] = -u * z;

				calibrationMatrixA[2 * i + 1, 4] = x;
				calibrationMatrixA[2 * i + 1, 5] = y;
				calibrationMatrixA[2 * i + 1, 6] = z;
				calibrationMatrixA[2 * i + 1, 7] = 1;
				calibrationMatrixA[2 * i + 1, 8] = -v * x;
				calibrationMatrixA[2 * i + 1, 9] = -v * y;
				calibrationMatrixA[2 * i + 1, 10] = -v * z;

				calibrationMatrixB[2 * i, 0] = u;
				calibrationMatrixB[2 * i + 1, 0] = v;
			}

			NMatrix calibrationMatrixAT = NMatrix.Transpose(calibrationMatrixA);
			NMatrix homographyData = NMatrix.Inverse(calibrationMatrixAT * calibrationMatrixA) * calibrationMatrixAT * calibrationMatrixB;

			Homography = new NMatrix(new double[,]{
				{homographyData[0,0], homographyData[1,0], homographyData[2,0], homographyData[3,0]} ,
				{homographyData[4,0], homographyData[5,0], homographyData[6,0], homographyData[7,0]} ,
				{homographyData[8,0], homographyData[9,0], homographyData[10,0], 1} 
			});


		}

		private static PointUV GetCenterOfMass(UnmanagedImage image, Rectangle rectangle) {
#if false
			centerX = rectangle.X + rectangle.Width / 2;
			centerY = rectangle.Y + rectangle.Height / 2;
#else
			double xTotal = 0;
			double yTotal = 0;
			double massTotal = 0;
			Bitmap bitmap = image.ToManagedImage();
			for (int x = 0; x < rectangle.Width; x++) {
				for (int y = 0; y < rectangle.Height; y++) {
					double value = bitmap.GetPixel(rectangle.X + x, rectangle.Y + y).R;
					massTotal += value;
					xTotal += x * value;
					yTotal += y * value;
				}
			}

			return PointUV.Create(
				xTotal / massTotal + rectangle.X,
				yTotal / massTotal + rectangle.Y
			);
#endif
		}

		public Bitmap Image { get; set; }

		public bool IsRunning {
			get { return isRunning; }
		}

		public int FrameRate {
			get {
				if (videoSource == null)
					return -1;

				return videoSource.FramesReceived;
			}
		}

		public PictureBox ImageDestination {
			get { return imageDestination; }
			set { imageDestination = value; }
		}

		public IList<PointUV> CalibrationPoints { get; set; }

		public NMatrix Homography { get; set; }

		public Size ImageSize {
			get { return imageSize; }
			set { imageSize = value; }
		}

		public PointUV Position { get; set; }

		public VideoForm videoForm { get; set; }
	}

	//public class CalibrationPoint {
	//    public CalibrationPoint(Point point, PointUV pointUV) {
	//        Point = point;
	//        PointUV = pointUV;
	//    }

	//    public Point Point { get; set; }
	//    public PointUV PointUV { get; set; }
	//}
}