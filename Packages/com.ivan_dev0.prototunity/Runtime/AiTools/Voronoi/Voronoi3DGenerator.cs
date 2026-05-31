using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PrototUnity.AiTools.Voronoi {
	public struct VoronoiGeneratorParameters {
		public readonly int seed;

		public readonly Vector3Int cellCount;
		public readonly Vector3 cubeSize;

		public readonly bool uniform;

		public VoronoiGeneratorParameters(
			int seed,
			Vector3Int cellCount,
			Vector3 cubeSize,
			bool uniform
		) {
			this.seed = seed;
			this.cellCount = cellCount;
			this.cubeSize = cubeSize;
			this.uniform = uniform;
		}
	}

	public struct VoronoiMeshParameters {
		public enum CellCenterPosition {
			MassCenter, Index
		}
		
		[CanBeNull] public readonly Material faceMaterial;
		[CanBeNull] public readonly GameObject cellPrefab;
		public readonly bool tintByCell;
		public readonly float cellShrink;
		[CanBeNull] public readonly Transform parent;
		public readonly CellCenterPosition cellCenterPosition;
		public readonly Vector3 cellCenterShift;

		public VoronoiMeshParameters(
			[CanBeNull] Material faceMaterial,
			[CanBeNull] GameObject cellPrefab,
			bool tintByCell,
			float cellShrink,
			[CanBeNull] Transform parent,
			CellCenterPosition cellCenterPosition, 
			Vector3 cellCenterShift
		) {
			this.faceMaterial = faceMaterial;
			this.cellPrefab = cellPrefab;
			this.tintByCell = tintByCell;
			this.cellShrink = cellShrink;
			this.parent = parent;
			this.cellCenterPosition = cellCenterPosition;
			this.cellCenterShift = cellCenterShift;
		}
	}
	
	public class Voronoi3DGenerator {
		private readonly VoronoiGeneratorParameters generatorParameters;
		private readonly VoronoiMeshParameters meshParameters;

		private const float EPSILON = 1e-5f;
		
		private struct CellIndex {
			public readonly Vector3Int index;
			public readonly Vector3 position;

			public CellIndex(Vector3Int index, Vector3 position) {
				this.index = index;
				this.position = position;
			}
		}

		public Voronoi3DGenerator(
			VoronoiGeneratorParameters generatorParameters,
			VoronoiMeshParameters meshParameters
		) {
			this.generatorParameters = generatorParameters;
			this.meshParameters = meshParameters;
		}
		
		public void Generate() {
			var indexAndCell = GenerateCells(generatorParameters);
			GenerateGameObjects(indexAndCell, meshParameters);
		}

		private static List<(CellIndex, ConvexPolyhedron)> GenerateCells(VoronoiGeneratorParameters generatorParameters) {
			Random.InitState(generatorParameters.seed);
			
			var indexAndSeeds = PickSeeds(
				generatorParameters.cellCount, generatorParameters.cubeSize, generatorParameters.uniform
			);
			var seeds = indexAndSeeds.Select(it => it.position).ToList();

			return indexAndSeeds
				.Select((cellIndex, i) => (cellIndex, BuildCell(seeds, i, generatorParameters.cubeSize)))
				.ToList();
		}

		private static void GenerateGameObjects(List<(CellIndex, ConvexPolyhedron)> indexAndCell, VoronoiMeshParameters meshParameters) {
			for (var i = 0; i < indexAndCell.Count; i++) {
				var index = indexAndCell[i].Item1;
				var cell = indexAndCell[i].Item2;
				if (cell == null || cell.faces.Count == 0) return;
				
				var cellGO = CreateCellGameObject(meshParameters, cell, index);
				cellGO.name = $"Cell_{index.index.ToString()}";
			}
		}

		private static List<CellIndex> PickSeeds(
			Vector3Int cellCount, Vector3 cubeSize, bool uniform
		) {
			var totalSeeds = cellCount.x * cellCount.y * cellCount.z;
			var seeds = new List<CellIndex>();
			var maxAttempts = Mathf.Max(200, totalSeeds * 50);

			var space = new Vector3(cubeSize.x / cellCount.x, cubeSize.y / cellCount.y, cubeSize.z / cellCount.z);
			for (var y = 0; y < cellCount.y; y++) {
				for (var x = 0; x < cellCount.x; x++) {
					for (var z = 0; z < cellCount.z; z++) { 
						var attempts = 0;
						while (seeds.Count < totalSeeds && attempts < maxAttempts) {
							attempts++;

							var center = new Vector3(
								space.x * 0.5f + x * space.x,
								space.y * 0.5f + y * space.y,
								space.z * 0.5f + z * space.z
							);

							var randomnessPower = uniform ? 0 : Mathf.Min(0.5f, Mathf.Max(0, cellCount.y - y - 1) * 0.1f);
							var randomness = new Vector3(
								Random.Range(-space.x * 0.5f, space.x * 0.5f),
								Random.Range(-space.y * 0.5f, space.y * 0.5f),
								Random.Range(-space.z * 0.5f, space.z * 0.5f)
							) * randomnessPower;
							var point = center + randomness;
							
							if (!IsPointInsideCube(point, cubeSize * 0.5f, cubeSize)) continue;
							if (!IsPointInsideCube(point, center, space)) continue;
							
							seeds.Add(new CellIndex(new Vector3Int(x, y, z), point));
							break;
						}
					}
				}
			}
			
			return seeds;
		}
		
		private static bool IsPointInsideCube(Vector3 point, Vector3 cubeCenter, Vector3 cubeSize) {
			if (point.x < cubeCenter.x - cubeSize.x * 0.5f || point.x > cubeCenter.x + cubeSize.x * 0.5f) return false;
			if (point.y < cubeCenter.y - cubeSize.y * 0.5f || point.y > cubeCenter.y + cubeSize.y * 0.5f) return false;
			if (point.z < cubeCenter.z - cubeSize.z * 0.5f || point.z > cubeCenter.z + cubeSize.z * 0.5f) return false;
			return true;
		}

		private static ConvexPolyhedron BuildCell(List<Vector3> seeds, int index, Vector3 cubeSize) {
			var poly = ConvexPolyhedron.CreateBox(cubeSize * 0.5f, cubeSize);

			for (var j = 0; j < seeds.Count; j++) {
				if (j == index) continue;
				var vectorBetweenCenters = seeds[j] - seeds[index];
				var lenght = vectorBetweenCenters.magnitude;
				if (lenght < EPSILON) continue;
				
				var middlePoint = (seeds[index] + seeds[j]) * 0.5f;
				var clippingPlaneOffsetFromOrigin = Vector3.Dot(vectorBetweenCenters.normalized, middlePoint);
				poly.ClipByPlane(vectorBetweenCenters.normalized, clippingPlaneOffsetFromOrigin);
				if (poly.faces.Count == 0) return null;
			}

			return poly;
		}

		private static GameObject CreateCellGameObject(
			VoronoiMeshParameters parameters, ConvexPolyhedron cell, CellIndex index
		) {
			var centroid = parameters.cellCenterPosition switch {
				VoronoiMeshParameters.CellCenterPosition.MassCenter => cell.ComputeCentroid(),
				VoronoiMeshParameters.CellCenterPosition.Index => index.index,
				_ => throw new ArgumentOutOfRangeException()
			} + parameters.cellCenterShift;

			GameObject cellGO;
			cellGO = parameters.cellPrefab == null ? new GameObject() : GameObject.Instantiate(parameters.cellPrefab);
			cellGO.transform.SetParent(parameters.parent, worldPositionStays: false);
			cellGO.transform.localPosition = centroid;
			cellGO.transform.localRotation = Quaternion.identity;
			cellGO.transform.localScale = Vector3.one;

			var keep = 1f - parameters.cellShrink;

			var tint = parameters.tintByCell
				? Color.HSVToRGB(Random.value, 0.45f, 0.95f)
				: Color.white;
			var baseMat = parameters.faceMaterial != null ? parameters.faceMaterial : CreateDefaultMaterial();
			baseMat.color = tint;

			for (var i = 0; i < cell.faces.Count; i++) {
				var face = cell.faces[i];
				var shrunk = new List<Vector3>(face.vertices.Count);
				for (var v = 0; v < face.vertices.Count; v++) {
					var shrunkVertex = Vector3.Lerp(centroid, face.vertices[v], keep);
					shrunk.Add(shrunkVertex - centroid);
				}

				var faceGO = new GameObject($"Face_{i}");
				faceGO.transform.SetParent(cellGO.transform, worldPositionStays: false);
				faceGO.transform.localPosition = shrunk.Aggregate(Vector3.zero, (current, vec) => current + vec) / shrunk.Count;
				faceGO.transform.localRotation = Quaternion.identity;
				faceGO.transform.localScale = Vector3.one;

				var mf = faceGO.AddComponent<MeshFilter>();
				var mr = faceGO.AddComponent<MeshRenderer>();
				mr.sharedMaterial = baseMat;
				mf.sharedMesh = BuildFaceMesh(shrunk, face.normal);
			}

			var meshCollider = cellGO.AddComponent<MeshCollider>();
			meshCollider.sharedMesh = Combine(cellGO.GetComponentsInChildren<MeshFilter>());
			return cellGO;
		}

		private static Mesh BuildFaceMesh(List<Vector3> verts, Vector3 normal) {
			var mesh = new Mesh { name = "VoronoiFace" };
			var center = verts.Aggregate(Vector3.zero, (current, vec) => current + vec) / verts.Count;
			mesh.SetVertices(verts.Select(it => it - center).ToList());

			int triCount = Mathf.Max(0, verts.Count - 2);
			var tris = new int[triCount * 3];
			for (int i = 0; i < triCount; i++) {
				tris[i * 3 + 0] = 0;
				tris[i * 3 + 1] = i + 1;
				tris[i * 3 + 2] = i + 2;
			}

			mesh.SetTriangles(tris, 0);

			var normals = new Vector3[verts.Count];
			for (int i = 0; i < normals.Length; i++) normals[i] = normal;
			mesh.SetNormals(normals);

			mesh.RecalculateBounds();
			return mesh;
		}

		private static Material CreateDefaultMaterial() {
			Shader shader = Shader.Find("Universal Render Pipeline/Lit");
			if (shader == null) shader = Shader.Find("Standard");
			var mat = new Material(shader) { name = "VoronoiDefault" };
			mat.color = new Color(0.75f, 0.75f, 0.8f, 1f);
			return mat;
		}

		private static Mesh Combine(MeshFilter[] meshFilters) {
			var instances = new CombineInstance[meshFilters.Length];

			for (var i = 0; i < meshFilters.Length; i++) {
				var meshFilter = meshFilters[i];
            
				instances[i] = new CombineInstance {
					mesh = meshFilter.sharedMesh,
					transform = Matrix4x4.Translate(meshFilter.transform.localPosition),
				};
			}

			var combinedMesh = new Mesh();
			combinedMesh.CombineMeshes(instances, mergeSubMeshes:true, useMatrices:true);
			return combinedMesh;
		}
	}
}