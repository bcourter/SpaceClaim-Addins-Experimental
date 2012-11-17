using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Linq;

using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge.Vision.Motion;
using AForge.Video;
using AForge.Video.DirectShow;

using SpaceClaim.Api.V8;
using SpaceClaim.Api.V8.Geometry;
using Point = SpaceClaim.Api.V8.Geometry.Point;

namespace SpaceClaim.AddIn.Tracker {
	public partial class ControlForm : Form {
		bool DeviceExist = false;
		bool isRunning = false;
		FilterInfoCollection videoDevices;

		Size imageSize = new Size(640, 480);

		public ControlForm() {
			InitializeComponent();

			VideoForms = new List<VideoForm>();
			CalibrationPoints = new List<Point>();

			//CalibrationPoints.Add(Point.Create(-1, -1, -1));
			//CalibrationPoints.Add(Point.Create(-1, -1, 1));
			//CalibrationPoints.Add(Point.Create(-1, 1, -1));
			//CalibrationPoints.Add(Point.Create(-1, 1, 1));
			//CalibrationPoints.Add(Point.Create(1 , -1, -1));
			//CalibrationPoints.Add(Point.Create(1 , -1, 1));
			//CalibrationPoints.Add(Point.Create(1 , 1, -1));
			//CalibrationPoints.Add(Point.Create(1 , 1, 1));

			double h = 1;
			CalibrationPoints.Add(Point.Create(-h, -h, 2));
			CalibrationPoints.Add(Point.Create(-h, h, 2));
			CalibrationPoints.Add(Point.Create(h, -h, 2));
			CalibrationPoints.Add(Point.Create(h, h, 2));
			//	h *= 0.5;
			CalibrationPoints.Add(Point.Create(-h, -h, 1));
			CalibrationPoints.Add(Point.Create(-h, h, 1));
			CalibrationPoints.Add(Point.Create(h, -h, 1));
			CalibrationPoints.Add(Point.Create(h, h, 1));
		}

		private void ContolForm_Load(object sender, EventArgs e) {
			getCamList();
		}

		private void getCamList() {
			try {
				videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
				comboBox1.Items.Clear();
				if (videoDevices.Count == 0)
					throw new ApplicationException();

				DeviceExist = true;
				foreach (FilterInfo device in videoDevices) {
					comboBox1.Items.Add(device.Name);
				}
				comboBox1.SelectedIndex = 0; //make dafault to first cam
			}
			catch (ApplicationException) {
				DeviceExist = false;
				comboBox1.Items.Add("No capture device on your system");
			}
		}

		private void rfsh_Click(object sender, EventArgs e) {
			getCamList();
		}

		private void start_Click(object sender, EventArgs e) {
			if (!isRunning) {
				if (DeviceExist) {
					for (int i = 0; i < comboBox1.Items.Count; i++) {
						var videoForm = new VideoForm(videoDevices[i].MonikerString, imageSize, this);
						videoForm.Show();
						VideoForms.Add(videoForm);
					}

					label2.Text = "Device running...";
					start.Text = "&Stop";
					timer.Enabled = true;
					isRunning = true;
				}
				else {
					label2.Text = "Error: No Device selected.";
				}
			}
			else {
				timer.Enabled = false;
				foreach (VideoForm videoForm in VideoForms)
					videoForm.Close();

				label2.Text = "Device stopped.";
				start.Text = "&Start";
				isRunning = false;
			}
		}

		public void AddCalibrationPoint(Point point) {
			if (CalibrationPoints == null)
				CalibrationPoints = new List<Point>();

			CalibrationPoints.Add(point);
			foreach (VideoForm videoForm in VideoForms)
				videoForm.TrackingCamera.RecordCalibrationPoint();
		}

		private void timer_Tick(object sender, EventArgs e) {
			TrackingCamera trackingCamera = VideoForms[0].TrackingCamera;
			if (trackingCamera != null)
				label2.Text = string.Format("{0} FPS. ({1}, {2})", trackingCamera.FrameRate / timer.Interval * 1000, trackingCamera.Position.U, trackingCamera.Position.V);
		}

		private void Form1_FormClosed(object sender, FormClosedEventArgs e) {
			foreach (VideoForm videoForm in VideoForms)
				videoForm.Close();
		}

		public IList<Point> CalibrationPoints { get; set; }
		public IList<VideoForm> VideoForms { get; set; }

	}
}