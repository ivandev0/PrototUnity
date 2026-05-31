using System.Collections.Generic;
using NUnit.Framework;
using PrototUnity.AiTools.Voronoi;
using UnityEngine;

namespace PrototUnity.Editor.Tests.Editor {
	public class Voronoi3DGeneratorTests {
		[Test]
		public void Generate_CreatesDistinctCellPositions() {
			var parent = new GameObject("Voronoi test parent");
			try {
				var cellCount = new Vector3Int(3, 3, 3);
				var generator = CreateGenerator(cellCount, parent.transform);

				generator.Generate();

				Assert.That(parent.transform.childCount, Is.EqualTo(27));

				var distinctPositions = new HashSet<Vector3>();
				for (var i = 0; i < parent.transform.childCount; i++) {
					distinctPositions.Add(parent.transform.GetChild(i).localPosition);
				}

				Assert.That(distinctPositions, Has.Count.EqualTo(27));
			} finally {
				Object.DestroyImmediate(parent);
			}
		}

		private static Voronoi3DGenerator CreateGenerator(Vector3Int cellCount, Transform parent) {
			return new Voronoi3DGenerator(
				new VoronoiGeneratorParameters(
					seed: 0,
					cellCount: cellCount,
					cubeSize: cellCount,
					uniform: true
				),
				new VoronoiMeshParameters(
					faceMaterial: null,
					cellPrefab: null,
					tintByCell: false,
					cellShrink: 0,
					parent: parent,
					cellCenterPosition: VoronoiMeshParameters.CellCenterPosition.Index,
					cellCenterShift: Vector3.zero
				)
			);
		}
	}
}
