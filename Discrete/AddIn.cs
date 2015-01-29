using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
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
using SpaceClaim.Svg;
using Discrete.Properties;
using Color = System.Drawing.Color;
using Application = SpaceClaim.Api.V10.Application;

namespace SpaceClaim.AddIn.Discrete {
	public class AddIn : SpaceClaim.Api.V10.Extensibility.AddIn, IExtensibility, IRibbonExtensibility, ICommandExtensibility {

		#region IExtensibility Members

		public bool Connect() {
			return true;
		}

		public void Disconnect() {
		}

		#endregion

		#region IRibbonExtensibility Members

		public string GetCustomUI() {
			return ribbonRoot.GetUI();
		}

		#endregion

		#region ICommandExtensibility Members

		RibbonRoot ribbonRoot = new RibbonRoot();
		public void Initialize() {
			var tab = new RibbonTabCapsule("UnfoldMore", Resources.TabText, ribbonRoot);
			RibbonGroupCapsule group;
			RibbonContainerCapsule container;
			RibbonButtonCapsule button;

			group = new RibbonGroupCapsule("Tabs", Resources.TabsGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
			container = new RibbonContainerCapsule("Buttons", group, RibbonCollectionCapsule.LayoutOrientation.vertical, false);
			button = new EdgeTabButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			new OffsetEdgesButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			new MakeTabsButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			group.CreateOptionsUI();

            /*
			group = new RibbonGroupCapsule("Excel", Resources.ExcelGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.vertical);
			new ExcelResetButtonCapsule(group, RibbonButtonCapsule.ButtonSize.small);
			new ExcelLengthButtonCapsule(group, RibbonButtonCapsule.ButtonSize.small);
			new ExcelAngleButtonCapsule(group, RibbonButtonCapsule.ButtonSize.small);
*/

			tab = new RibbonTabCapsule("Discrete", Resources.DiscreteTabText, ribbonRoot);
			group = new RibbonGroupCapsule("Procedural", Resources.ProceduralGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
			container = new RibbonContainerCapsule("Buttons", group, RibbonCollectionCapsule.LayoutOrientation.vertical, false);
			new CreateFigure8ButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			new CreateAnimateButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			container = new RibbonContainerCapsule("Tools", group, RibbonCollectionCapsule.LayoutOrientation.vertical, false);
			new LawsonToolButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			new LawsonRelaxButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			new CreateLawsonButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			group.CreateOptionsUI();
			container = new RibbonContainerCapsule("More", group, RibbonCollectionCapsule.LayoutOrientation.vertical, false);
			new LawsonCirclesButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);

			group = new RibbonGroupCapsule("Gyroid", Resources.GyroidGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
			container = new RibbonContainerCapsule("Tools", group, RibbonCollectionCapsule.LayoutOrientation.vertical, false);
			new GyroidRelaxButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			new BoyToolButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);

			new BrowseToolButtonCapsule(group, RibbonButtonCapsule.ButtonSize.large);

			group = new RibbonGroupCapsule("Quadrant", "Octant", tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
			container = new RibbonContainerCapsule("0", group, RibbonCollectionCapsule.LayoutOrientation.vertical, false);
			new SelectQuadrant000ButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			new SelectQuadrant100ButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			new SelectQuadrant010ButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			new SelectQuadrant110ButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			container = new RibbonContainerCapsule("1", group, RibbonCollectionCapsule.LayoutOrientation.vertical, false);
			new SelectQuadrant001ButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			new SelectQuadrant101ButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			new SelectQuadrant011ButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			new SelectQuadrant111ButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);

			tab = new RibbonTabCapsule("Lenticular", Resources.Lenticular, ribbonRoot);
			group = new RibbonGroupCapsule("Lenticular", Resources.Lenticular, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
			new LenticularPlanarCommandCapsule(group, RibbonButtonCapsule.ButtonSize.large);
			new LenticularCylindricalCommandCapsule(group, RibbonButtonCapsule.ButtonSize.large);
			group.CreateOptionsUI();


	//		group = new RibbonGroupCapsule("Wilf", Resources.WilfGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
	//		new WilfButtonCapsule(group, RibbonButtonCapsule.ButtonSize.small);

			Application.AddFileHandler(new SvgFileSaveHandler());
			Application.AddFileHandler(new DxfFileSaveHandler());
		}

		#endregion
	}
}
