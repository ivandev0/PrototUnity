using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PrototUnity.AiTools {
	public class Voronoi3DGeneratorClaude : MonoBehaviour {
		[Header("Generation")] [SerializeField]
		private int seed = 12345;

		[SerializeField] private Vector3 cellCount = new Vector3Int(1, 1, 1);
		[SerializeField] private Vector3 cubeSize = new Vector3(10f, 10f, 10f);

		[Tooltip("If enabled, all cells are generated uniformly")] [SerializeField]
		private bool uniform = true;
		
		[Header("Appearance")]
		[Tooltip("Shrinks each cell around its centroid so adjacent cells don't touch.")]
		[Range(0f, 0.2f)]
		[SerializeField]
		private float cellShrink = 0.02f;

		[SerializeField] private Material faceMaterial;

		[Tooltip("If true, each face gets its own material instance tinted by the cell color.")] [SerializeField]
		private bool tintByCell = true;

		private const float EPSILON = 1e-5f;

		[ContextMenu("Generate")]
		public void Generate() {
			Clear();

			Random.InitState(seed);
			
			var seeds = PickSeeds();
			var baseMat = faceMaterial != null ? faceMaterial : CreateDefaultMaterial();

			for (var i = 0; i < seeds.Count; i++) {
				var cell = BuildCell(seeds, i);
				if (cell == null || cell.Faces.Count == 0) continue;

				Color tint = tintByCell
					? Color.HSVToRGB(Random.value, 0.45f, 0.95f)
					: Color.white;
				CreateCellGameObject($"Cell_{i}", cell, baseMat, tint);
			}
			return;
		}

		[ContextMenu("Clear")]
		public void Clear() {
			for (var i = transform.childCount - 1; i >= 0; i--) {
				var child = transform.GetChild(i).gameObject;
				if (Application.isPlaying) Destroy(child);
				else DestroyImmediate(child);
			}
		}

		private List<Vector3> PickSeeds() {
			var totalSeeds = cellCount.x * cellCount.y * cellCount.z;
			var seeds = new List<Vector3>();
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

							var randomnessPower = uniform ? 0 : Mathf.Max(0, cellCount.y - y - 1) * 0.1f; 
							var point = -cubeSize * 0.5f + center + Random.insideUnitSphere * randomnessPower;
							if (point.x < -cubeSize.x * 0.5f || point.x > cubeSize.x * 0.5f) continue;
							if (point.y < -cubeSize.y * 0.5f || point.y > cubeSize.y * 0.5f) continue;
							if (point.z < -cubeSize.z * 0.5f || point.z > cubeSize.z * 0.5f) continue;
							seeds.Add(point);
							break;
						}
					}
				}
			}
			
			return seeds;
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
				if (poly.Faces.Count == 0) return null;
			}

			return poly;
		}

		private void CreateCellGameObject(string cellName, ConvexPolyhedron cell, Material baseMat, Color tint) {
			Vector3 centroid = cell.ComputeCentroid();

			var cellGO = new GameObject(cellName);
			cellGO.transform.SetParent(transform, worldPositionStays: false);
			cellGO.transform.localPosition = centroid;
			cellGO.transform.localRotation = Quaternion.identity;
			cellGO.transform.localScale = Vector3.one;

			float keep = 1f - cellShrink;

			Material mat = baseMat;
			if (tintByCell) {
				mat = new Material(baseMat);
				mat.color = tint;
			}

			for (int i = 0; i < cell.Faces.Count; i++) {
				ConvexPolyhedron.Face face = cell.Faces[i];
				var shrunk = new List<Vector3>(face.Vertices.Count);
				for (int v = 0; v < face.Vertices.Count; v++) {
					var shrunkVertex = Vector3.Lerp(centroid, face.Vertices[v], keep);
					shrunk.Add(shrunkVertex - centroid);
				}

				var faceGO = new GameObject($"Face_{i}");
				faceGO.transform.SetParent(cellGO.transform, worldPositionStays: false);
				faceGO.transform.localPosition = Vector3.zero;
				faceGO.transform.localRotation = Quaternion.identity;
				faceGO.transform.localScale = Vector3.one;

				var mf = faceGO.AddComponent<MeshFilter>();
				var mr = faceGO.AddComponent<MeshRenderer>();
				mr.sharedMaterial = mat;
				mf.sharedMesh = BuildFaceMesh(shrunk, face.Normal);
			}

			var meshCollider = cellGO.AddComponent<MeshCollider>();
			meshCollider.sharedMesh = Combine(cellGO.GetComponentsInChildren<MeshFilter>());
		}

		private static Mesh BuildFaceMesh(List<Vector3> verts, Vector3 normal) {
			var mesh = new Mesh { name = "VoronoiFace" };
			mesh.SetVertices(verts);

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
				};
			}

			var combinedMesh = new Mesh();
			combinedMesh.CombineMeshes(instances, mergeSubMeshes:true, useMatrices:false);
			return combinedMesh;
		}

		// ------------------------------------------------------------------
		// Convex polyhedron represented as a list of planar faces (CCW from
		// outside). Clipping is Sutherland-Hodgman in 3D: each half-space
		// clips every face, intersection segments are chained into a cap.
		// ------------------------------------------------------------------
		private class ConvexPolyhedron {
			public class Face {
				public List<Vector3> Vertices;
				public Vector3 Normal;
			}

			public List<Face> Faces = new List<Face>();

			public static ConvexPolyhedron CreateBox(Vector3 size) {
				var h = size * 0.5f;
				var poly = new ConvexPolyhedron();

				poly.Faces.Add(new Face {
					Normal = new Vector3(-1f, 0f, 0f),
					Vertices = new List<Vector3> {
						new(-h.x, -h.y, -h.z),
						new(-h.x, -h.y, h.z),
						new(-h.x, h.y, h.z),
						new(-h.x, h.y, -h.z),
					}
				});
				poly.Faces.Add(new Face {
					Normal = new Vector3(1f, 0f, 0f),
					Vertices = new List<Vector3> {
						new(h.x, -h.y, h.z),
						new(h.x, -h.y, -h.z),
						new(h.x, h.y, -h.z),
						new(h.x, h.y, h.z),
					}
				});
				poly.Faces.Add(new Face {
					Normal = new Vector3(0f, -1f, 0f),
					Vertices = new List<Vector3> {
						new(-h.x, -h.y, -h.z),
						new(h.x, -h.y, -h.z),
						new(h.x, -h.y, h.z),
						new(-h.x, -h.y, h.z),
					}
				});
				poly.Faces.Add(new Face {
					Normal = new Vector3(0f, 1f, 0f),
					Vertices = new List<Vector3> {
						new(-h.x, h.y, h.z),
						new(h.x, h.y, h.z),
						new(h.x, h.y, -h.z),
						new(-h.x, h.y, -h.z),
					}
				});
				poly.Faces.Add(new Face {
					Normal = new Vector3(0f, 0f, -1f),
					Vertices = new List<Vector3> {
						new(h.x, -h.y, -h.z),
						new(-h.x, -h.y, -h.z),
						new(-h.x, h.y, -h.z),
						new(h.x, h.y, -h.z),
					}
				});
				poly.Faces.Add(new Face {
					Normal = new Vector3(0f, 0f, 1f),
					Vertices = new List<Vector3> {
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
				var newFaces = new List<Face>(Faces.Count + 1);
				var capEdges = new List<(Vector3 entry, Vector3 exit)>();

				foreach (var face in Faces) {
					ClipFace(face, normal, clippingPlaneOffsetFromOrigin, newFaces, capEdges);
				}

				var cap = BuildCapFace(capEdges, normal);
				if (cap != null) newFaces.Add(cap);

				Faces = newFaces;
			}

			private static void ClipFace(
				Face face, Vector3 clipPlaneNormal, float clipPlaneOffsetFromOrigin, List<Face> newFaces, List<(Vector3 entry, Vector3 exit)> capEdges
			) {
				var nVerts = face.Vertices.Count;
				var clipped = new List<Vector3>(nVerts + 2);
				Vector3 entryCut = default, exitCut = default;
				bool hasEntry = false, hasExit = false;

				for (var i = 0; i < nVerts; i++) {
					var a = face.Vertices[i];
					var b = face.Vertices[(i + 1) % nVerts];
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
					newFaces.Add(new Face { Vertices = clipped, Normal = face.Normal });
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

				return new Face { Vertices = points, Normal = normal };
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
				foreach (var face in Faces) {
					sum += face.Vertices.Aggregate(Vector3.zero, (current, vec) => current + vec);
					count += face.Vertices.Count;
				}

				return count > 0 ? sum / count : Vector3.zero;
			}
		}
	}
}