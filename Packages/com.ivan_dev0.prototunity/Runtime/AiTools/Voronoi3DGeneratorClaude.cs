using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace PrototUnity.AiTools {
	public class Voronoi3DGeneratorClaude : MonoBehaviour {
		[Header("Generation")] [SerializeField]
		private int seed = 12345;

		[SerializeField, Min(1)] private int cellCount = 20;
		[SerializeField] private Vector3 cubeSize = new Vector3(10f, 10f, 10f);

		[Tooltip("Minimum distance between seed points, as a fraction of the smaller cube dimension.")]
		[Range(0f, 0.5f)]
		[SerializeField]
		private float minSeedSpacing = 0.1f;

		[Tooltip("If enabled, all cells are clipped to stay inside the cube.")] [SerializeField]
		private bool clipToCube = true;

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

			var seeds = PickSeeds();
			var baseMat = faceMaterial != null ? faceMaterial : CreateDefaultMaterial();

			var rng = new Random(seed);
			for (var i = 0; i < seeds.Count; i++) {
				var cell = BuildCell(seeds, i);
				if (cell == null || cell.Faces.Count == 0) continue;

				Color tint = tintByCell
					? Color.HSVToRGB((float)rng.NextDouble(), 0.45f, 0.95f)
					: Color.white;
				CreateCellGameObject($"Cell_{i}", cell, baseMat, tint);
			}
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
			var rng = new Random(seed);
			var seeds = new List<Vector3>();
			var half = cubeSize * 0.5f;
			var minDim = Mathf.Min(cubeSize.x, Mathf.Min(cubeSize.y, cubeSize.z));
			var minDist = minSeedSpacing * minDim;
			var minDistSq = minDist * minDist;

			var maxAttempts = Mathf.Max(200, cellCount * 50);
			var attempts = 0;
			while (seeds.Count < cellCount && attempts < maxAttempts) {
				attempts++;
				var p = new Vector3(
					((float)rng.NextDouble() * 2f - 1f) * half.x,
					((float)rng.NextDouble() * 2f - 1f) * half.y,
					((float)rng.NextDouble() * 2f - 1f) * half.z);

				var ok = true;
				for (int i = 0; i < seeds.Count; i++) {
					if (!((seeds[i] - p).sqrMagnitude < minDistSq)) continue;
					ok = false;
					break;
				}

				if (ok) seeds.Add(p);
			}

			return seeds;
		}

		private ConvexPolyhedron BuildCell(List<Vector3> seeds, int index) {
			var poly = clipToCube
				? ConvexPolyhedron.CreateBox(cubeSize)
				: ConvexPolyhedron.CreateBox(cubeSize * 4f);

			for (var j = 0; j < seeds.Count; j++) {
				if (j == index) continue;
				var diff = seeds[j] - seeds[index];
				var len = diff.magnitude;
				if (len < EPSILON) continue;
				
				var n = diff / len;
				var d = Vector3.Dot(n, (seeds[index] + seeds[j]) * 0.5f);
				poly.ClipByPlane(n, d);
				if (poly.Faces.Count == 0) return null;
			}

			return poly;
		}

		private void CreateCellGameObject(string cellName, ConvexPolyhedron cell, Material baseMat, Color tint) {
			var cellGO = new GameObject(cellName);
			cellGO.transform.SetParent(transform, worldPositionStays: false);
			cellGO.transform.localPosition = Vector3.zero;
			cellGO.transform.localRotation = Quaternion.identity;
			cellGO.transform.localScale = Vector3.one;

			Vector3 centroid = cell.ComputeCentroid();
			float keep = 1f - cellShrink;

			Material mat = baseMat;
			if (tintByCell) {
				mat = new Material(baseMat);
				mat.color = tint;
			}

			for (int i = 0; i < cell.Faces.Count; i++) {
				ConvexPolyhedron.Face face = cell.Faces[i];
				var shrunk = new List<Vector3>(face.Vertices.Count);
				for (int v = 0; v < face.Vertices.Count; v++)
					shrunk.Add(Vector3.Lerp(centroid, face.Vertices[v], keep));

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

			// Keep half-space { x : dot(n, x) <= d }.
			public void ClipByPlane(Vector3 n, float d) {
				var newFaces = new List<Face>(Faces.Count + 1);
				var capEdges = new List<(Vector3 entry, Vector3 exit)>();

				for (int i = 0; i < Faces.Count; i++)
					ClipFace(Faces[i], n, d, newFaces, capEdges);

				Face cap = BuildCapFace(capEdges, n);
				if (cap != null) newFaces.Add(cap);

				Faces = newFaces;
			}

			private static void ClipFace(Face face, Vector3 n, float d,
				List<Face> newFaces, List<(Vector3 entry, Vector3 exit)> capEdges) {
				var clipped = new List<Vector3>(face.Vertices.Count + 2);
				Vector3 entryCut = default, exitCut = default;
				bool hasEntry = false, hasExit = false;
				int nVerts = face.Vertices.Count;

				for (int i = 0; i < nVerts; i++) {
					Vector3 a = face.Vertices[i];
					Vector3 b = face.Vertices[(i + 1) % nVerts];
					float da = Vector3.Dot(n, a) - d;
					float db = Vector3.Dot(n, b) - d;
					bool aIn = da <= EPSILON;
					bool bIn = db <= EPSILON;

					if (aIn) clipped.Add(a);
					if (aIn && !bIn) {
						float t = da / (da - db);
						Vector3 p = Vector3.Lerp(a, b, t);
						clipped.Add(p);
						exitCut = p;
						hasExit = true;
					} else if (!aIn && bIn) {
						float t = da / (da - db);
						Vector3 p = Vector3.Lerp(a, b, t);
						clipped.Add(p);
						entryCut = p;
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
				float e2 = EPSILON * EPSILON;
				for (int i = 0; i < pts.Count; i++) {
					if (result.Count == 0 || (result[result.Count - 1] - pts[i]).sqrMagnitude > e2)
						result.Add(pts[i]);
				}

				if (result.Count > 1 && (result[0] - result[result.Count - 1]).sqrMagnitude < e2)
					result.RemoveAt(result.Count - 1);
				return result;
			}

			private static Face BuildCapFace(List<(Vector3 entry, Vector3 exit)> edges, Vector3 normal) {
				if (edges.Count < 3) return null;
				float e2 = EPSILON * EPSILON * 100f;

				var remaining = new List<(Vector3 entry, Vector3 exit)>(edges);
				var cap = new List<Vector3>();
				var current = remaining[0];
				remaining.RemoveAt(0);
				cap.Add(current.entry);
				cap.Add(current.exit);

				int safety = edges.Count * 2;
				while (remaining.Count > 0 && safety-- > 0) {
					Vector3 needed = current.exit;
					int best = -1;
					float bestSq = e2;
					for (int i = 0; i < remaining.Count; i++) {
						float dsq = (remaining[i].entry - needed).sqrMagnitude;
						if (dsq < bestSq) {
							bestSq = dsq;
							best = i;
						}
					}

					if (best < 0) return null;
					current = remaining[best];
					remaining.RemoveAt(best);
					if ((current.exit - cap[0]).sqrMagnitude > e2)
						cap.Add(current.exit);
				}

				if (cap.Count < 3) return null;
				return new Face { Vertices = cap, Normal = normal };
			}

			public Vector3 ComputeCentroid() {
				Vector3 sum = Vector3.zero;
				int count = 0;
				for (int i = 0; i < Faces.Count; i++) {
					var verts = Faces[i].Vertices;
					for (int v = 0; v < verts.Count; v++) {
						sum += verts[v];
						count++;
					}
				}

				return count > 0 ? sum / count : Vector3.zero;
			}
		}
	}
}