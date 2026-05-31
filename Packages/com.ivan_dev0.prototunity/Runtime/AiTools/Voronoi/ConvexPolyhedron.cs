using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PrototUnity.AiTools.Voronoi { 
	/**
	 * Convex polyhedron represented as a list of planar faces (CCW from outside).
	 * Clipping is Sutherland-Hodgman in 3D: each half-space clips every face,
	 * intersection segments are chained into a cap.
	 */
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