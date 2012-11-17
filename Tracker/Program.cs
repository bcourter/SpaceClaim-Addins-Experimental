using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SpaceClaim.AddIn.Tracker
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
	//		SpaceClaim.Api.V8.Api.Initialize();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ControlForm());
        }
    }
}