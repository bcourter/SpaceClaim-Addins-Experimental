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
using Animator.Properties;

namespace SpaceClaim.AddIn.Animator {
	public partial class AnimatorAddIn : SpaceClaim.Api.V10.Extensibility.AddIn, IExtensibility, IRibbonExtensibility, ICommandExtensibility {
		readonly CommandCapsule[] capsules = new CommandCapsule[] {
			new OscillatorToolCapsule(),
			new RecordMovieCapsule()
		};

		#region IExtensibility Members

		public bool Connect() {
			return true;
		}

		public void Disconnect() {
			Ribbon.Disconnect();
		}

		#endregion

		#region IRibbonExtensibility Members

		public string GetCustomUI() {
			return Resources.Ribbon;
		}

		#endregion

		#region ICommandExtensibility Members

		 public void Initialize() {
			 Ribbon.Initialize();

			 foreach (CommandCapsule capsule in capsules)
				 capsule.Initialize();
		 }

		#endregion

		#region Event handlers
		#endregion
	}
}
