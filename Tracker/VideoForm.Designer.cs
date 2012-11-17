namespace SpaceClaim.AddIn.Tracker {
	partial class VideoForm {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if (disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.components = new System.ComponentModel.Container();
			this.videoPictureBox = new System.Windows.Forms.PictureBox();
			this.timer = new System.Windows.Forms.Timer(this.components);
			((System.ComponentModel.ISupportInitialize)(this.videoPictureBox)).BeginInit();
			this.SuspendLayout();
			// 
			// videoPictureBox
			// 
			this.videoPictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.videoPictureBox.Location = new System.Drawing.Point(0, 0);
			this.videoPictureBox.Name = "videoPictureBox";
			this.videoPictureBox.Size = new System.Drawing.Size(624, 442);
			this.videoPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.videoPictureBox.TabIndex = 0;
			this.videoPictureBox.TabStop = false;
			// 
			// timer
			// 
			this.timer.Enabled = true;
			this.timer.Tick += new System.EventHandler(this.timer_Tick);
			// 
			// VideoForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(624, 442);
			this.Controls.Add(this.videoPictureBox);
			this.Name = "VideoForm";
			this.Text = "VideoForm";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.VideoForm_FormClosing);
			((System.ComponentModel.ISupportInitialize)(this.videoPictureBox)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.PictureBox videoPictureBox;
		private System.Windows.Forms.Timer timer;
	}
}