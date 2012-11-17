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
	static class Rounds {
		public static void Initialize() {
			Command command;

			command = Command.Create("AE.Rounds.Remove");
			command.Text = "Remove";
			command.Hint = "Remove rounds from the group named \"Rounds\".";
			command.Executing += Remove_Executing;
			command.Updating += AddInHelper.EnabledCommand_Updating;
		}

		static void Remove_Executing(object sender, EventArgs e) {
			Window activeWindow = Window.ActiveWindow;

			Group roundGroup = null;
			foreach (Group group in activeWindow.Groups) {
				if (group.Name == "Rounds") {
					roundGroup = group;
					break;
				}
			}

			List<DesignFace> roundFaces = new List<DesignFace>();
			foreach (IDocObject iDocObject in roundGroup.Members) {
				if (iDocObject is DesignFace)
					roundFaces.Add((DesignFace)iDocObject);
			}

			List<DesignFace> originalRoundFaces = new List<DesignFace>(roundFaces);

			// 1. find contiguous groups
			// 2. try removing whole group
			// 3. Try finding connecting large rounds
			// 4. individual faces

			// Gather rounds into contiguous groups
			List<DesignFace> remainingFaces = new List<DesignFace>(roundFaces);
			List<List<Round>> roundSets = new List<List<Round>>();
			Round.Reset();

			while (remainingFaces.Count > 0) {
				List<Round> roundSet = new List<Round>();
				Queue<DesignEdge> edgesToVisit = new Queue<DesignEdge>();

				AddRound(remainingFaces[0], roundSet, remainingFaces, edgesToVisit);

				while (edgesToVisit.Count > 0) {
					DesignEdge designEdge = edgesToVisit.Dequeue();
					foreach (DesignFace testFace in designEdge.Faces) {
						if (remainingFaces.Contains(testFace)) 
							AddRound(testFace, roundSet, remainingFaces, edgesToVisit);
					}
				}

				foreach (Round round in roundSet) {
					if (round.Valence == 1)
						round.DesignFace.SetColor(null, Color.LavenderBlush);
					else if (round.Valence == 2)
						round.DesignFace.SetColor(null, Color.Lavender);
					else
						round.DesignFace.SetColor(null, Color.Khaki);
				}

				roundSets.Add(roundSet);
			}

			int faceCount = 0;
			while (faceCount != roundFaces.Count) {
				faceCount = roundFaces.Count;

				List<DesignFace> removeFaces = new List<DesignFace>();

				//removeFaces.AddRange(RemoveDesignFaces(roundFaces));

				//foreach (DesignFace designFace in roundFaces) 
				//    removeFaces.AddRange(RemoveDesignFaces(new DesignFace[] { designFace }));

				foreach (DesignFace designFace in removeFaces)
					roundFaces.Remove(designFace);

				removeFaces.Clear();

		//		break;
			}

			//foreach (DesignFace designFace in originalRoundFaces) {
			//    if (!designFace.IsDeleted)
			//        designFace.SetColor(null, Color.Red);
			//}
		}

		static void AddRound(DesignFace designFace, List<Round> roundSet, List<DesignFace> remainingFaces, Queue<DesignEdge> edgesToVisit) {
			remainingFaces.Remove(designFace);
			Round round = new Round(designFace);
			roundSet.Add(round);

			DesignFace otherFace = null;
			Round otherRound = null;
			foreach (DesignEdge newEdge in designFace.Edges) {
				edgesToVisit.Enqueue(newEdge);
				if (AddInHelper.TryGetAdjacentDesignFace(designFace, newEdge, out otherFace) && Round.TryGetRound(otherFace, out otherRound)) {
					round.AddAdjacentRound(otherRound);
					otherRound.AddAdjacentRound(round);
				}
			}

		}

		static ICollection<DesignFace> RemoveDesignFaces(ICollection<DesignFace> designFaces) { // returns faces removed
			Debug.Assert(designFaces.Count > 0, "Empty collection of design faces.");

			//List<IDocObject> docObjects = new List<IDocObject>();
			//foreach (DesignFace designFace in designFaces)
			//    docObjects.Add((IDocObject)designFace);

			//Window.ActiveWindow.ActiveContext.Selection =  docObjects;
			//Command.Execute("Fill");

			List<Face> faces = new List<Face>();
			foreach (DesignFace designFace in designFaces)
				faces.Add(designFace.Shape);

			Body body = faces[0].Body;
			try {
				body.DeleteFaces(faces, RepairAction.GrowSurrounding);
			}
			catch { ; }

			List<DesignFace> deletedFaces = new List<DesignFace>();
			foreach (DesignFace designFace in designFaces) {
				if (designFace.IsDeleted)
					deletedFaces.Add(designFace);
			}

			return deletedFaces;
		}

	}

	public class Round {
		static Dictionary<DesignFace, Round> designFaceToRound = new Dictionary<DesignFace, Round>();

		public static bool TryGetRound(DesignFace designFace, out Round round) {
			round = null;
			if (designFaceToRound.TryGetValue(designFace, out round))
				return true;

			return false;
		}

		public static void Reset() {
			designFaceToRound.Clear();
		}

		DesignFace designFace;
		Dictionary<Round, byte> adjacentRounds = new Dictionary<Round, byte>();

		public Round(DesignFace designFace) {
			this.designFace = designFace;
			Debug.Assert(!designFaceToRound.ContainsKey(designFace), "Round already exists for face!");
			designFaceToRound.Add(designFace, this);
		}

		public DesignFace DesignFace {
			get { return designFace; }
		}

		public ICollection<Round> AdjacentRounds {
			get { return adjacentRounds.Keys; }
		}

		public int Valence {
			get { return adjacentRounds.Count; }
		}

		public void AddAdjacentRound(Round round) {
			Debug.Assert(round != this);

			adjacentRounds[round] = 0;
		}

	}
}
