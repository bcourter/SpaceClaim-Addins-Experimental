using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace SpaceClaim.AddIn.Tracker {
	public partial class VideoForm : Form {
		TrackingCamera trackingCamera;
		ControlForm controlForm;

		Thread cameraThread;
		string cameraName;

		public VideoForm(string cameraName, Size imageSize, ControlForm controlForm) {
			InitializeComponent();
			this.controlForm = controlForm;

			ImageSize = imageSize;
			this.cameraName = cameraName;

			cameraThread = new Thread(new ThreadStart(TrackingCameraThread)); // change to thread pool
			cameraThread.Name = cameraName;
			cameraThread.Start();

		}

		private void TrackingCameraThread() {
			trackingCamera = new TrackingCamera(cameraThread.Name, ImageSize, controlForm);
			trackingCamera.ImageDestination = videoPictureBox;
			trackingCamera.Start();
			timer.Enabled = true;
		}

		public Size ImageSize {
			get { return videoPictureBox.Size; }
			set {
				this.Size = this.Size - videoPictureBox.Size + value;
				videoPictureBox.Size = value;
			}
		}

		public PictureBox ImageDestination {
			get { return videoPictureBox; }
		}

		private void timer_Tick(object sender, EventArgs e) {
			if (trackingCamera != null)
				videoPictureBox.Image = trackingCamera.Image;
		}

		private void VideoForm_FormClosing(object sender, FormClosingEventArgs e) {
			if (trackingCamera != null)  // TDB why often null?
				trackingCamera.Stop();

			cameraThread.Abort();
		}

		public TrackingCamera TrackingCamera {
			get { return trackingCamera; }
			//	set { trackingCamera = value; }
		}
	}
}
