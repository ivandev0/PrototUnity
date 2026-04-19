using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace PrototUnity.Editor.AiTools {
	public static class Voronoi3DGenerator {
		private static readonly int seedCount = 32;
		private static readonly float boundsSize = 4f;
		private static readonly float shrinkFactor = 1.0f;
		
		private class FaceData {
			public List<Vector3> vertices;
			public Vector3 normal;
		}

		[MenuItem("Tools/Create 3D Voronoi")]
		public static void Execute() {
			// Cleanup previous if exists
			var old = GameObject.Find("3D Voronoi Diagram");
			if (old != null) Object.Destroy(old);

			var boundsMin = -Vector3.one * boundsSize / 2;
			var boundsMax = Vector3.one * boundsSize / 2;

			Random.InitState(DateTime.Now.Millisecond);

			var seeds = new List<Vector3>();
			for (var i = 0; i < seedCount; i++) {
				seeds.Add(new Vector3(
					Random.Range(boundsMin.x, boundsMax.x),
					Random.Range(boundsMin.y, boundsMax.y),
					Random.Range(boundsMin.z, boundsMax.z)
				));
			}

			var root = new GameObject("3D Voronoi Diagram") {
				transform = {
					position = new Vector3(0, boundsSize / 2 + 1, 0) // Lift above ground
				}
			};

			// Determine Shader
			var shaderName = "Standard";
			if (GraphicsSettings.currentRenderPipeline != null) {
				shaderName = "Universal Render Pipeline/Lit";
			}

			var shader = Shader.Find(shaderName);
			if (shader == null) shader = Shader.Find("Diffuse");
			if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");

			for (var i = 0; i < seeds.Count; i++) {
				var currentSeed = seeds[i];
				var faces = GetInitialCubeFaces(boundsMin, boundsMax);

				for (var j = 0; j < seeds.Count; j++) {
					if (i == j) continue;

					var otherSeed = seeds[j];
					var planeNormal = (otherSeed - currentSeed).normalized;
					var planePoint = (currentSeed + otherSeed) * 0.5f;

					var nextFaces = new List<FaceData>();
					var intersectionPoints = new List<Vector3>();

					foreach (var face in faces) {
						var clippedVertices = ClipPolygon(face.vertices, planePoint, -planeNormal, out var faceIntersections);
						if (clippedVertices.Count >= 3) {
							nextFaces.Add(new FaceData { vertices = clippedVertices, normal = face.normal });
						}

						intersectionPoints.AddRange(faceIntersections);
					}

					if (intersectionPoints.Count >= 3) {
						var newFaceVerts = SortConvexPolygon(intersectionPoints, planeNormal);
						if (newFaceVerts.Count >= 3) {
							nextFaces.Add(new FaceData { vertices = newFaceVerts, normal = planeNormal });
						}
					}

					faces = nextFaces;
				}

				var cellObj = new GameObject("Cell_" + i) {
					transform = {
						parent = root.transform,
						localPosition = Vector3.zero
					}
				};

				var cellColor = Color.HSVToRGB((float)i / seedCount, 0.6f, 0.8f);
				var cellMat = new Material(shader) {
					color = cellColor
				};
				// Handle URP BaseColor
				if (cellMat.HasProperty("_BaseColor")) cellMat.SetColor("_BaseColor", cellColor);

				for (var f = 0; f < faces.Count; f++) {
					var shrunkVertices = new List<Vector3>();
					foreach (var v in faces[f].vertices) {
						shrunkVertices.Add(currentSeed + (v - currentSeed) * shrinkFactor);
					}

					var shrunkFaceData = new FaceData { vertices = shrunkVertices, normal = faces[f].normal };
					var faceObj = CreateFaceObject(shrunkFaceData, "Face_" + f, cellMat);
					faceObj.transform.SetParent(cellObj.transform);
				}
			}

			Debug.Log($"Generated 3D Voronoi with {seedCount} cells and separated face meshes.");
		}

		private static List<FaceData> GetInitialCubeFaces(Vector3 min, Vector3 max) {
			var faces = new List<FaceData> {
				new() {
					vertices = new List<Vector3> {
						new(min.x, max.y, min.z), new(min.x, min.y, min.z),
						new(max.x, min.y, min.z), new(max.x, max.y, min.z)
					},
					normal = Vector3.back
				},
				new() {
					vertices = new List<Vector3> {
						new(min.x, min.y, max.z), new(min.x, max.y, max.z),
						new(max.x, max.y, max.z), new(max.x, min.y, max.z)
					},
					normal = Vector3.forward
				},
				new() {
					vertices = new List<Vector3> {
						new(min.x, min.y, min.z), new(min.x, min.y, max.z),
						new(max.x, min.y, max.z), new(max.x, min.y, min.z)
					},
					normal = Vector3.down
				},
				new() {
					vertices = new List<Vector3> {
						new(min.x, max.y, max.z), new(min.x, max.y, min.z),
						new(max.x, max.y, min.z), new(max.x, max.y, max.z)
					},
					normal = Vector3.up
				},
				new() {
					vertices = new List<Vector3> {
						new(min.x, min.y, min.z), new(min.x, max.y, min.z),
						new(min.x, max.y, max.z), new(min.x, min.y, max.z)
					},
					normal = Vector3.left
				},
				new() {
					vertices = new List<Vector3> {
						new(max.x, min.y, min.z), new(max.x, min.y, max.z),
						new(max.x, max.y, max.z), new(max.x, max.y, min.z)
					},
					normal = Vector3.right
				}
			};
			return faces;
		}

		private static List<Vector3> ClipPolygon(List<Vector3> vertices, Vector3 planePoint, Vector3 planeNormal, out List<Vector3> intersections) {
			intersections = new List<Vector3>();
			var shResult = new List<Vector3>();
			if (vertices.Count == 0) return shResult;

			for (var i = 0; i < vertices.Count; i++) {
				var v1 = vertices[i];
				var v2 = vertices[(i + 1) % vertices.Count];

				var v1In = Vector3.Dot(v1 - planePoint, planeNormal) >= -1e-5f;
				var v2In = Vector3.Dot(v2 - planePoint, planeNormal) >= -1e-5f;

				if (v1In && v2In) {
					shResult.Add(v2);
				} else if (v1In) {
					var intersect = Intersect(v1, v2, planePoint, planeNormal);
					shResult.Add(intersect);
					intersections.Add(intersect);
				} else if (v2In) {
					var intersect = Intersect(v1, v2, planePoint, planeNormal);
					shResult.Add(intersect);
					shResult.Add(v2);
					intersections.Add(intersect);
				}
			}

			return shResult;
		}

		private static Vector3 Intersect(Vector3 v1, Vector3 v2, Vector3 planePoint, Vector3 planeNormal) {
			var direction = v2 - v1;
			var dot = Vector3.Dot(direction, planeNormal);
			if (Mathf.Abs(dot) < 1e-6f) return v1;
			var t = Vector3.Dot(planePoint - v1, planeNormal) / dot;
			return v1 + direction * t;
		}

		private static List<Vector3> SortConvexPolygon(List<Vector3> points, Vector3 normal) {
			if (points.Count < 3) return points;
			var unique = new List<Vector3>();
			foreach (var p in points) {
				if (!unique.Any(u => Vector3.Distance(u, p) < 1e-4f)) unique.Add(p);
			}

			if (unique.Count < 3) return unique;
			var centroid = Vector3.zero;
			foreach (var p in unique) centroid += p;
			centroid /= unique.Count;
			var v0 = (unique[0] - centroid).normalized;
			var binormal = Vector3.Cross(normal, v0).normalized;
			return unique.OrderBy(p => {
				Vector3 dir = (p - centroid).normalized;
				return Mathf.Atan2(Vector3.Dot(dir, binormal), Vector3.Dot(dir, v0));
			}).ToList();
		}

		private static GameObject CreateFaceObject(FaceData face, string name, Material mat) {
			var obj = new GameObject(name) {
				transform = {
					localPosition = Vector3.zero
				}
			};
			var mf = obj.AddComponent<MeshFilter>();
			var mr = obj.AddComponent<MeshRenderer>();
			mr.sharedMaterial = mat;
			
			var triangles = new int[(face.vertices.Count - 2) * 3];
			for (var i = 0; i < face.vertices.Count - 2; i++) {
				triangles[i * 3] = 0;
				triangles[i * 3 + 1] = i + 1;
				triangles[i * 3 + 2] = i + 2;
			}
			
			var mesh = new Mesh {
				name = name,
				vertices = face.vertices.ToArray(),
				triangles = triangles
			};
			mesh.RecalculateNormals();
			
			var faceNormal = Vector3.Cross(face.vertices[1] - face.vertices[0], face.vertices[2] - face.vertices[0]).normalized;
			if (Vector3.Dot(faceNormal, face.normal) < 0) {
				for (var i = 0; i < triangles.Length; i += 3) {
					int temp = triangles[i + 1];
					triangles[i + 1] = triangles[i + 2];
					triangles[i + 2] = temp;
				}

				mesh.triangles = triangles;
				mesh.RecalculateNormals();
			}

			mf.sharedMesh = mesh;
			return obj;
		}
	}
}