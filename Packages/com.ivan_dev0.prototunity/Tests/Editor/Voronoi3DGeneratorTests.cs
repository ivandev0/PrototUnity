using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PrototUnity.AiTools.Voronoi;
using UnityEngine;
using Object = UnityEngine.Object;

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

		[Test]
		public void ConvexPolyhedronJobResultTypes_DoNotContainNativeContainers() {
			var polyhedronType = Type.GetType("PrototUnity.AiTools.Voronoi.ConvexPolyhedron, PrototUnity", throwOnError: true);
			var faceType = Type.GetType("PrototUnity.AiTools.Voronoi.ConvexFace, PrototUnity", throwOnError: true);

			AssertNoNativeContainerFields(polyhedronType);
			AssertNoNativeContainerFields(faceType);
		}

		private static Voronoi3DGenerator CreateGenerator(Vector3Int cellCount, Transform parent) {
			return new Voronoi3DGenerator(
				new VoronoiGeneratorParameters(
					seed: 0,
					cellCount: cellCount,
					cubeSize: cellCount,
					seedModifier: VoronoiGeneratorParameters.uniform
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

		private static void AssertNoNativeContainerFields(Type type) {
			var nativeContainerFields = type
				.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(field => field.FieldType.FullName?.StartsWith("Unity.Collections.Native") == true)
				.Select(field => $"{field.FieldType.Name} {field.Name}")
				.ToArray();

			Assert.That(
				nativeContainerFields,
				Is.Empty,
				$"{type.FullName} should only contain range/value fields so it can be stored in job containers."
			);
		}
	}
}
