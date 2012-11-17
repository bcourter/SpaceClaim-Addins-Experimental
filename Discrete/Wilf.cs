using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using System.IO;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.AddInLibrary;
using SpaceClaim.AddIn.Discrete;
using Discrete.Properties;

namespace SpaceClaim.AddIn.Discrete {
	public class WilfButtonCapsule : RibbonButtonCapsule {
		public WilfButtonCapsule(RibbonCollectionCapsule parent, ButtonSize buttonSize)
			: base("Wilf", Resources.WilfText, null, Resources.WilfHint, parent, buttonSize) {
		}

		protected override void OnExecute(Command command, ExecutionContext context, System.Drawing.Rectangle buttonRect) {
			List<List<TreeVertex>> rows = WilfTree.GetRows(16);
			Window activeWindow = Window.ActiveWindow;
			Part part = activeWindow.Document.MainPart;

			//DesignBody designBody;
			//foreach (List<TreeVertex> row in rows) {
			//    foreach (TreeVertex vertex in row) {
			//        designBody = AddInHelper.CreateSphere(Point.Create((double) vertex.I / vertex.J, 0, 0), 0.001, part);
			//        designBody.Name = string.Format("{0}/{1}", vertex.I, vertex.J);
			//    }
			//}


#if false // text output
			StreamWriter postScriptSteam = new StreamWriter(@"c:\wilf.txt");
			foreach (List<TreeVertex> row in rows) {
				foreach (TreeVertex vertex in row)
					postScriptSteam.Write(string.Format("{0}/{1} ", vertex.I, vertex.J));

				postScriptSteam.Write("\n");
			}

			postScriptSteam.Close();
#endif

#if false // Excel output
			ExcelWorksheet worksheet = new ExcelWorksheet();
			int i = 1;
			foreach (List<TreeVertex> row in rows) {
				foreach (TreeVertex vertex in row) {
					worksheet.SetCell(i, 1, vertex.Level);
					worksheet.SetCell(i, 2, string.Format("={0}/{1}", vertex.I, vertex.J));
					i++;
				}
			}
#endif

#if true // Excel output
			ExcelWorksheet worksheet = new ExcelWorksheet();
			int i = 1;
			foreach (List<TreeVertex> row in rows) {
				double max = 0;
				int index = 0;
				double value = 0;
				double maxValue = 0;
				int j = 0;
				foreach (TreeVertex vertex in row) {
					value = (double)vertex.I / (double)vertex.J;
					double result = 1 / Math.Log(value);
					max = Math.Max(max, result);
					if (result == max) {
						index = j;
						maxValue = value;
					}

					j++;
				}
				worksheet.SetCell(i, 1, (double)index);
				worksheet.SetCell(i, 2, maxValue);
				worksheet.SetCell(i, 3, max);
				i++;
			}
#endif
		}
	}

	public static class WilfTree {
		public static List<List<TreeVertex>> GetRows(int maxLevel) {
			List<List<TreeVertex>> rows = new List<List<TreeVertex>>();
			rows.Add(new List<TreeVertex>(new TreeVertex[] { new TreeVertex(null, 1, 1, 0) }));

			for (int level = 1; level <= maxLevel; level++) {
				rows.Add(new List<TreeVertex>());
				foreach (TreeVertex parent in rows[level - 1]) {
					parent.Left = new TreeVertex(parent, parent.I, parent.I + parent.J, level);
					parent.Right = new TreeVertex(parent, parent.I + parent.J, parent.J, level);
					rows[level].Add(parent.Left);
					rows[level].Add(parent.Right);
				}
			}

			return rows;
		}
	}

	public class TreeVertex {
		TreeVertex parent;
		int i, j;
		int level;
		TreeVertex left = null, right = null;

		public TreeVertex(TreeVertex parent, int i, int j, int level) {
			this.parent = parent;
			this.level = level;
			this.i = i;
			this.j = j;
		}

		public int I {
			get { return i; }
		}

		public int J {
			get { return j; }
		}

		public TreeVertex Left {
			get { return left; }
			set { left = value; }
		}

		public TreeVertex Right {
			get { return right; }
			set { right = value; }
		}

		public int Level {
			get { return level; }
		}
	}

}
