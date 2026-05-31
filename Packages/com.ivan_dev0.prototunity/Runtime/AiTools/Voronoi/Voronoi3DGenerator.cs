using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace PrototUnity.AiTools.Voronoi {
	public class Voronoi3DGenerator {
		private readonly int seed;

		private readonly Vector3Int cellCount;
		private readonly Vector3 cubeSize;

		private readonly bool uniform;
		
		private readonly float cellShrink;

		private readonly Material faceMaterial;
		private readonly GameObject cellPrefab;

		private readonly bool tintByCell;
		
		private readonly Transform parent;

		private const float EPSILON = 1e-5f;

		public Voronoi3DGenerator(
			int seed,
			Vector3Int cellCount,
			Vector3 cubeSize,
			bool uniform,
			float cellShrink,
			Material faceMaterial,
			GameObject cellPrefab,
			bool tintByCell,
			Transform parent
		) {
			this.seed = seed;
			this.cellCount = cellCount;
			this.cubeSize = cubeSize;
			this.uniform = uniform;
			this.cellShrink = cellShrink;
			this.faceMaterial = faceMaterial;
			this.cellPrefab = cellPrefab;
			this.tintByCell = tintByCell;
			this.parent = parent;
		}
		
		public void Generate() {
			Random.InitState(seed);
			
			var indexAndSeeds = PickSeeds();
			var seeds = indexAndSeeds.Select(it => it.Item2).ToList();
			var baseMat = faceMaterial != null ? faceMaterial : CreateDefaultMaterial();

			var cells = seeds.Select((_, index) => BuildCell(seeds, index)).ToList();
			
			for (var i = 0; i < cells.Count; i++) {
				var cell = cells[i];
				if (cell == null || cell.faces.Count == 0) return;
				var tint = tintByCell
					? Color.HSVToRGB(Random.value, 0.45f, 0.95f)
					: Color.white;
				
				var cellGO = CreateCellGameObject(cellPrefab, cell, cellShrink, baseMat, tint);
				cellGO.transform.SetParent(parent, worldPositionStays: false);
				cellGO.name = $"Cell_{indexAndSeeds[i].Item1.ToString()}";
			}
		}
		
		private List<(Vector3Int, Vector3)> PickSeeds() {
			var totalSeeds = cellCount.x * cellCount.y * cellCount.z;
			var seeds = new List<(Vector3Int, Vector3)>();
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
							) - cubeSize * 0.5f;

							var randomnessPower = uniform ? 0 : Mathf.Min(0.5f, Mathf.Max(0, cellCount.y - y - 1) * 0.1f);
							var randomness = new Vector3(
								Random.Range(-space.x, space.x),
								Random.Range(-space.y, space.y),
								Random.Range(-space.z, space.z)
							) * randomnessPower;
							var point = center + randomness;
							
							if (!IsPointInsideCube(point, Vector3.zero, cubeSize)) continue;
							if (!IsPointInsideCube(point, center, space)) continue;
							
							seeds.Add((new Vector3Int(x, y - cellCount.y, z), point));
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

		private ConvexPolyhedron BuildCell(List<Vector3> seeds, int index) {
			var poly = ConvexPolyhedron.CreateBox(cubeSize);

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
			[CanBeNull] GameObject prefab, ConvexPolyhedron cell, float cellShrink, Material baseMat, Color tint
		) {
			var centroid = cell.ComputeCentroid();

			GameObject cellGO;
			cellGO = prefab == null ? new GameObject() : GameObject.Instantiate(prefab);
			cellGO.transform.localPosition = centroid;
			cellGO.transform.localRotation = Quaternion.identity;
			cellGO.transform.localScale = Vector3.one;

			var keep = 1f - cellShrink;

			var mat = new Material(baseMat) {
				color = tint
			};

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
				mr.sharedMaterial = mat;
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