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

namespace SpaceClaim.AddIn.AETools {
	static class ColorAlter {
		const string colorsRestoreCommandName = "AEColorsRestore";
		const string colorsRandomizeColorCommandName = "AEColorsRandomizeColor";
		const string colorsVaryColorCommandName = "AEColorsVaryColor";

		const string colorsRandomizeHueCommandName = "AEColorsRandomizeHue";
		const string colorsRandomizeBrightnessCommandName = "AEColorsRandomizeBrightness";
		const string colorsRandomizeSaturationCommandName = "AEColorsRandomizeSaturation";
		const string colorsVaryHueCommandName = "AEColorsVaryHue";
		const string colorsVaryBrightnessCommandName = "AEColorsVaryBrightness";
		const string colorsVarySaturationCommandName = "AEColorsVarySaturation";

		const string colorsDecreaseHueCommandName = "AEColorsDecreaseHue";
		const string colorsDecreaseBrightnessCommandName = "AEColorsDecreaseBrightness";
		const string colorsDecreaseSaturationCommandName = "AEColorsDecreaseSaturation";
		const string colorsIncreaseHueCommandName = "AEColorsIncreaseHue";
		const string colorsIncreaseBrightnessCommandName = "AEColorsIncreaseBrightness";
		const string colorsIncreaseSaturationCommandName = "AEColorsIncreaseSaturation";

        // TBD Fix with ribboncomands or something
		static public void Initialize() {
			Command command;

			command = Command.Create(colorsRestoreCommandName);
			command.Text = "Restore Colors";
			command.Hint = "Put every body back on its layer color.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
	//		command.Tag = new RecolorRestore();

			command = Command.Create(colorsRandomizeColorCommandName);
			command.Text = "Randomize Colors";
			command.Hint = "Give every body its own random color.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
	//		command.Tag = new RecolorRandomColor();

			command = Command.Create(colorsVaryColorCommandName);
			command.Text = "Vary Colors";
			command.Hint = "Vary body colors slightly.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
	//		command.Tag = new RecolorVaryColor();

			//	Randomize Group
			command = Command.Create(colorsRandomizeHueCommandName);
			command.Text = "Randomize Hue";
			command.Hint = "Give every body its own random hue.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
	//		command.Tag = new RecolorRandomHue();

			command = Command.Create(colorsRandomizeBrightnessCommandName);
			command.Text = "Randomize Brightness";
			command.Hint = "Give every body its own random brightness.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
            //command.Tag = new RecolorRandomBrightness();

			command = Command.Create(colorsRandomizeSaturationCommandName);
			command.Text = "Randomize Saturation";
			command.Hint = "Give every body its own random saturation.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
            //command.Tag = new RecolorRandomSaturation();

			command = Command.Create(colorsVaryHueCommandName);
			command.Text = "Vary Hue";
			command.Hint = "Vary body colors' hue slightly.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
            //command.Tag = new RecolorVaryHue();

			command = Command.Create(colorsVaryBrightnessCommandName);
			command.Text = "Vary Brightness";
			command.Hint = "Vary body colors' brightness slightly.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
            //command.Tag = new RecolorVaryBrightness();

			command = Command.Create(colorsVarySaturationCommandName);
			command.Text = "Vary Saturation";
			command.Hint = "Vary body colors' saturation slightly.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
            //command.Tag = new RecolorVarySaturation();

			command = Command.Create(colorsDecreaseHueCommandName);
			command.Text = "Decrease Hue";
			command.Hint = "Decrease body hue slightly.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
            //command.Tag = new RecolorDecreaseHue();

			command = Command.Create(colorsDecreaseBrightnessCommandName);
			command.Text = "Decrease Brightness";
			command.Hint = "Decrease body colors' brightness slightly.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
            //command.Tag = new RecolorDecreaseBrightness();

			command = Command.Create(colorsDecreaseSaturationCommandName);
			command.Text = "Decrease Saturation";
			command.Hint = "Decrease body colors' saturation slightly.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
            //command.Tag = new RecolorDecreaseSaturation();

			command = Command.Create(colorsIncreaseHueCommandName);
			command.Text = "Increase Hue";
			command.Hint = "Increase body hue slightly.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
            //command.Tag = new RecolorIncreaseHue();

			command = Command.Create(colorsIncreaseBrightnessCommandName);
			command.Text = "Increase Brightness";
			command.Hint = "Increase body colors' brightness slightly.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
            //command.Tag = new RecolorIncreaseBrightness();

			command = Command.Create(colorsIncreaseSaturationCommandName);
			command.Text = "Increase Saturation";
			command.Hint = "Increase body colors' saturation slightly.";
			command.Executing += colorsChange_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
            //command.Tag = new RecolorIncreaseSaturation();
		}

		static void colorsChange_Executing(object sender, EventArgs e) {
			Command command = (Command) sender;
//			RecolorBase recolorer = (RecolorBase) command.Tag;
            RecolorBase recolorer = new RecolorVaryBrightness();

			Window activeWindow = Window.ActiveWindow;

			ICollection<DesignBody> designBodies = activeWindow.GetAllSelectedDesignBodies();
			if (designBodies.Count == 0) {
				Part part = null;
				Component component = activeWindow.ActiveContext.SingleSelection as Component;
				if (component != null)
					part = component.Template;
				else
					part = Window.ActiveWindow.Scene as Part;

				designBodies = part.GetDescendants<DesignBody>();
			}

			foreach (DesignBody designBody in designBodies)
				recolorer.ColorBody(designBody);
		}

		// Derived Recolor classes

		class RecolorRestore : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				return null;
			}
		}

		class RecolorRandomColor : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				return Color.FromArgb(RandomMiddleRGB(), RandomMiddleRGB(), RandomMiddleRGB());
			}
		}

		class RecolorVaryColor : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				Color color = designBody.GetVisibleColor();
				return Color.FromArgb(
					RandomVaryRGB(color.R),
					RandomVaryRGB(color.G),
					RandomVaryRGB(color.B)
				);
			}
		}

		// Random
		class RecolorRandomHue : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				Color color = designBody.GetVisibleColor();
				return HSBColor.ShiftHue(color, RandomHue());
			}
		}

		class RecolorRandomBrightness : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				Color color = designBody.GetVisibleColor();
				HSBColor hsbColor = HSBColor.FromColor(color);
				return HSBColor.ShiftBrightness(color, RandomMiddleRGB() - hsbColor.B);
			}
		}

		class RecolorRandomSaturation : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				Color color = designBody.GetVisibleColor();
				HSBColor hsbColor = HSBColor.FromColor(color);
				return HSBColor.ShiftSaturation(color, RandomMiddleRGB() - hsbColor.S);
			}
		}

		// Vary
		class RecolorVaryHue : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				return HSBColor.ShiftHue(designBody.GetVisibleColor(), RandomVary());
			}
		}

		class RecolorVaryBrightness : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				return HSBColor.ShiftBrightness(designBody.GetVisibleColor(), RandomVary());
			}
		}

		class RecolorVarySaturation : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				return HSBColor.ShiftSaturation(designBody.GetVisibleColor(), RandomVary());
			}
		}

		// Decrease
		class RecolorDecreaseHue : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				return HSBColor.ShiftHue(designBody.GetVisibleColor(), -VaryHueAmount);
			}
		}

		class RecolorDecreaseBrightness : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				return HSBColor.ShiftBrightness(designBody.GetVisibleColor(), -VaryAmount);
			}
		}

		class RecolorDecreaseSaturation : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				return HSBColor.ShiftSaturation(designBody.GetVisibleColor(), -VaryAmount);
			}
		}

		// Increase
		class RecolorIncreaseHue : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				return HSBColor.ShiftHue(designBody.GetVisibleColor(), VaryHueAmount);
			}
		}

		class RecolorIncreaseBrightness : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				return HSBColor.ShiftBrightness(designBody.GetVisibleColor(), VaryAmount);
			}
		}

		class RecolorIncreaseSaturation : RecolorBase {
			public override Color? ColorForBody(DesignBody designBody) {
				return HSBColor.ShiftSaturation(designBody.GetVisibleColor(), VaryAmount);
			}
		}

		abstract class RecolorBase {
			static Random random = new Random();
			const int varyAmount = 10;
			const int varyHueAmount = 15;

			public abstract Color? ColorForBody(DesignBody designBody);

			public void ColorBody(DesignBody designBody) {
				designBody.SetColor(null, ColorForBody(designBody));
			}

			protected int VaryAmount {
				get { return varyAmount; }
			}

			protected int VaryHueAmount {
				get { return varyHueAmount; }
			}

			protected static int RandomMiddleRGB() {
				return random.Next(55, 200);
			}

			protected static int RandomHue() {
				return random.Next(360);
			}

			protected static int RandomVary() {
				return random.Next(-varyAmount, varyAmount);
			}

			protected static int RandomVaryRGB(int start) {
				start += RandomVary();
				start = Math.Max(start, 0);
				start = Math.Min(start, 255);
				return start;
			}
		}
	}
}
