using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PrototUnity.AiTools {
	public class Voronoi3DGeneratorClaude : MonoBehaviour {
		[Header("Generation")] [SerializeField]
		private int seed = 12345;

		[SerializeField] private Vector3Int cellCount = new Vector3Int(1, 1, 1);
		[SerializeField] private Vector3 cubeSize = new Vector3(10f, 10f, 10f);

		[Tooltip("If enabled, all cells are generated uniformly")] [SerializeField]
		private bool uniform = true;
		
		[Header("Appearance")]
		[Tooltip("Shrinks each cell around its centroid so adjacent cells don't touch.")]
		[Range(0f, 0.2f)]
		[SerializeField]
		private float cellShrink = 0.02f;

		[SerializeField] private Material faceMaterial;
		[SerializeField] private GameObject cellPrefab;

		[Tooltip("If true, each face gets its own material instance tinted by the cell color.")] [SerializeField]
		private bool tintByCell = true;

		[ContextMenu("Generate")]
		public void Generate() {
			new Voronoi3DGenerator(seed, cellCount, cubeSize, uniform, cellShrink, faceMaterial, cellPrefab, tintByCell, transform)
				.Generate();
		}

		[ContextMenu("Clear")]
		public void Clear() {
			for (var i = transform.childCount - 1; i >= 0; i--) {
				var child = transform.GetChild(i).gameObject;
				if (Application.isPlaying) Destroy(child);
				else DestroyImmediate(child);
			}
		}
	}

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

			for (var i = 0; i < seeds.Count; i++) {
				var cell = BuildCell(seeds, i);
				if (cell == null || cell.faces.Count == 0) continue;

				Color tint = tintByCell
					? Color.HSVToRGB(Random.value, 0.45f, 0.95f)
					: Color.white;
				CreateCellGameObject($"Cell_{indexAndSeeds[i].Item1.ToString()}", cell, baseMat, tint);
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

		private void CreateCellGameObject(string cellName, ConvexPolyhedron cell, Material baseMat, Color tint) {
			Vector3 centroid = cell.ComputeCentroid();

			GameObject cellGO;
			if (cellPrefab == null) {
				cellGO = new GameObject(cellName);
				cellGO.transform.SetParent(parent, worldPositionStays: false);
			} else { 
				cellGO = GameObject.Instantiate(cellPrefab, parent, false); 
				cellGO.name = cellName;
			}
			cellGO.transform.localPosition = centroid;
			cellGO.transform.localRotation = Quaternion.identity;
			cellGO.transform.localScale = Vector3.one;

			float keep = 1f - cellShrink;

			Material mat = baseMat;
			if (tintByCell) {
				mat = new Material(baseMat);
				mat.color = tint;
			}

			for (int i = 0; i < cell.faces.Count; i++) {
				ConvexPolyhedron.Face face = cell.faces[i];
				var shrunk = new List<Vector3>(face.vertices.Count);
				for (int v = 0; v < face.vertices.Count; v++) {
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

	// ------------------------------------------------------------------
	// Convex polyhedron represented as a list of planar faces (CCW from
	// outside). Clipping is Sutherland-Hodgman in 3D: each half-space
	// clips every face, intersection segments are chained into a cap.
	// ------------------------------------------------------------------
	internal class ConvexPolyhedron {
		private const float EPSILON = 1e-5f;

		public class Face {
			public List<Vector3> vertices;
			public Vector3 normal;
		}

		public List<Face> faces = new List<Face>();

		public static ConvexPolyhedron CreateBox(Vector3 size) {
			var h = size * 0.5f;
			var poly = new ConvexPolyhedron();

			poly.faces.Add(new Face {
				normal = new Vector3(-1f, 0f, 0f),
				vertices = new List<Vector3> {
					new(-h.x, -h.y, -h.z),
					new(-h.x, -h.y, h.z),
					new(-h.x, h.y, h.z),
					new(-h.x, h.y, -h.z),
				}
			});
			poly.faces.Add(new Face {
				normal = new Vector3(1f, 0f, 0f),
				vertices = new List<Vector3> {
					new(h.x, -h.y, h.z),
					new(h.x, -h.y, -h.z),
					new(h.x, h.y, -h.z),
					new(h.x, h.y, h.z),
				}
			});
			poly.faces.Add(new Face {
				normal = new Vector3(0f, -1f, 0f),
				vertices = new List<Vector3> {
					new(-h.x, -h.y, -h.z),
					new(h.x, -h.y, -h.z),
					new(h.x, -h.y, h.z),
					new(-h.x, -h.y, h.z),
				}
			});
			poly.faces.Add(new Face {
				normal = new Vector3(0f, 1f, 0f),
				vertices = new List<Vector3> {
					new(-h.x, h.y, h.z),
					new(h.x, h.y, h.z),
					new(h.x, h.y, -h.z),
					new(-h.x, h.y, -h.z),
				}
			});
			poly.faces.Add(new Face {
				normal = new Vector3(0f, 0f, -1f),
				vertices = new List<Vector3> {
					new(h.x, -h.y, -h.z),
					new(-h.x, -h.y, -h.z),
					new(-h.x, h.y, -h.z),
					new(h.x, h.y, -h.z),
				}
			});
			poly.faces.Add(new Face {
				normal = new Vector3(0f, 0f, 1f),
				vertices = new List<Vector3> {
					new(-h.x, -h.y, h.z),
					new(h.x, -h.y, h.z),
					new(h.x, h.y, h.z),
					new(-h.x, h.y, h.z),
				}
			});
			return poly;
		}

		// Keep half-space { x : dot(normal, x) <= clippingPlaneOffsetFromOrigin }.
		public void ClipByPlane(Vector3 normal, float clippingPlaneOffsetFromOrigin) {
			var newFaces = new List<Face>(faces.Count + 1);
			var capEdges = new List<(Vector3 entry, Vector3 exit)>();

			foreach (var face in faces) {
				ClipFace(face, normal, clippingPlaneOffsetFromOrigin, newFaces, capEdges);
			}

			var cap = BuildCapFace(capEdges, normal);
			if (cap != null) newFaces.Add(cap);

			faces = newFaces;
		}

		private static void ClipFace(
			Face face, Vector3 clipPlaneNormal, float clipPlaneOffsetFromOrigin, List<Face> newFaces,
			List<(Vector3 entry, Vector3 exit)> capEdges
		) {
			var nVerts = face.vertices.Count;
			var clipped = new List<Vector3>(nVerts + 2);
			Vector3 entryCut = default, exitCut = default;
			bool hasEntry = false, hasExit = false;

			for (var i = 0; i < nVerts; i++) {
				var a = face.vertices[i];
				var b = face.vertices[(i + 1) % nVerts];
				var da = Vector3.Dot(clipPlaneNormal, a) - clipPlaneOffsetFromOrigin;
				var db = Vector3.Dot(clipPlaneNormal, b) - clipPlaneOffsetFromOrigin;
				if (Mathf.Abs(da) <= EPSILON) da = 0f;
				if (Mathf.Abs(db) <= EPSILON) db = 0f;
				var aIn = da <= 0f;
				var bIn = db <= 0f;

				if (aIn) clipped.Add(a);
				if (aIn && !bIn) {
					float t = da / (da - db);
					Vector3 intersectionPoint = Vector3.Lerp(a, b, t);
					clipped.Add(intersectionPoint);
					exitCut = intersectionPoint;
					hasExit = true;
				} else if (!aIn && bIn) {
					float t = da / (da - db);
					Vector3 intersectionPoint = Vector3.Lerp(a, b, t);
					clipped.Add(intersectionPoint);
					entryCut = intersectionPoint;
					hasEntry = true;
				}
			}

			clipped = RemoveNearDuplicates(clipped);
			if (clipped.Count >= 3)
				newFaces.Add(new Face { vertices = clipped, normal = face.normal });
			if (hasEntry && hasExit)
				capEdges.Add((entryCut, exitCut));
		}

		private static List<Vector3> RemoveNearDuplicates(List<Vector3> pts) {
			if (pts.Count < 2) return pts;
			var result = new List<Vector3>(pts.Count);
			var e2 = EPSILON * EPSILON;
			for (int i = 0; i < pts.Count; i++) {
				if (result.Count == 0 || (result[^1] - pts[i]).sqrMagnitude > e2) {
					result.Add(pts[i]);
				}
			}

			if (result.Count > 1 && (result[0] - result[^1]).sqrMagnitude < e2) {
				result.RemoveAt(result.Count - 1);
			}

			return result;
		}

		private static Face BuildCapFace(List<(Vector3 entry, Vector3 exit)> edges, Vector3 normal) {
			if (edges.Count < 3) return null;
			float e2 = EPSILON * EPSILON * 100f;

			var points = new List<Vector3>();
			foreach (var edge in edges) {
				AddUnique(points, edge.entry, e2);
				AddUnique(points, edge.exit, e2);
			}

			if (points.Count < 3) return null;

			var centroid = points.Aggregate(Vector3.zero, (current, p) => current + p);
			centroid /= points.Count;

			// Pick any reference vector not parallel to normal so normal × reference is non-zero.
			// Default to Y; fall back to X when normal is itself mostly along Y.
			var reference = Mathf.Abs(normal.y) > 0.9f ? Vector3.right : Vector3.up;
			var u = Vector3.Cross(normal, reference).normalized;
			var v = Vector3.Cross(normal, u).normalized;

			points.Sort((p1, p2) => {
				var d1 = p1 - centroid;
				var d2 = p2 - centroid;
				var a1 = Mathf.Atan2(Vector3.Dot(v, d1), Vector3.Dot(u, d1));
				var a2 = Mathf.Atan2(Vector3.Dot(v, d2), Vector3.Dot(u, d2));
				return a1.CompareTo(a2);
			});

			return new Face { vertices = points, normal = normal };
		}

		private static void AddUnique(List<Vector3> list, Vector3 p, float error) {
			foreach (var existing in list) {
				if ((p - existing).sqrMagnitude < error) return;
			}

			list.Add(p);
		}

		public Vector3 ComputeCentroid() {
			var sum = Vector3.zero;
			var count = 0;
			foreach (var face in faces) {
				sum += face.vertices.Aggregate(Vector3.zero, (current, vec) => current + vec);
				count += face.vertices.Count;
			}

			return count > 0 ? sum / count : Vector3.zero;
		}
	}
}