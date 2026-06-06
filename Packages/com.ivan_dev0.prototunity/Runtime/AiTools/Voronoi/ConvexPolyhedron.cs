using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace PrototUnity.AiTools.Voronoi {
	internal readonly struct ConvexFace {
		public readonly int vertexStart;
		public readonly int vertexCount;
		public readonly Vector3 normal;

		public ConvexFace(int vertexStart, int vertexCount, Vector3 normal) {
			this.vertexStart = vertexStart;
			this.vertexCount = vertexCount;
			this.normal = normal;
		}
	}

	/**
	 * Convex polyhedron represented as a range into flat face and vertex buffers.
	 * This struct contains only value metadata so it can be stored inside native job containers.
	 */
	internal readonly struct ConvexPolyhedron {
		public readonly int faceStart;
		public readonly int faceCount;

		public ConvexPolyhedron(int faceStart, int faceCount) {
			this.faceStart = faceStart;
			this.faceCount = faceCount;
		}

		public bool IsEmpty => faceCount == 0;

		public Vector3 ComputeCentroid(NativeList<ConvexFace> faces, NativeList<Vector3> vertices) {
			var sum = Vector3.zero;
			var count = 0;
			for (var faceOffset = 0; faceOffset < faceCount; faceOffset++) {
				var face = faces[faceStart + faceOffset];
				for (var vertexOffset = 0; vertexOffset < face.vertexCount; vertexOffset++) {
					sum += vertices[face.vertexStart + vertexOffset];
					count++;
				}
			}

			return count > 0 ? sum / count : Vector3.zero;
		}
	}

	/**
	 * Job-local mutable builder for Sutherland-Hodgman clipping in 3D.
	 * The mutable native containers are flat and are disposed before the job writes its result.
	 */
	internal struct ConvexPolyhedronBuilder : IDisposable {
		private const float EPSILON = 1e-5f;

		private NativeList<ConvexFace> faces;
		private NativeList<Vector3> vertices;

		private readonly struct ClipEdge {
			public readonly Vector3 entry;
			public readonly Vector3 exit;

			public ClipEdge(Vector3 entry, Vector3 exit) {
				this.entry = entry;
				this.exit = exit;
			}
		}

		public int FaceCount => faces.IsCreated ? faces.Length : 0;

		public static ConvexPolyhedronBuilder CreateBox(Vector3 center, Vector3 size) {
			var h = size * 0.5f;
			var poly = new ConvexPolyhedronBuilder {
				faces = new NativeList<ConvexFace>(6, Allocator.Temp),
				vertices = new NativeList<Vector3>(24, Allocator.Temp)
			};

			poly.AddFace4(
				new Vector3(-1f, 0f, 0f),
				new Vector3(-h.x, -h.y, -h.z) + center,
				new Vector3(-h.x, -h.y, h.z) + center,
				new Vector3(-h.x, h.y, h.z) + center,
				new Vector3(-h.x, h.y, -h.z) + center
			);
			poly.AddFace4(
				new Vector3(1f, 0f, 0f),
				new Vector3(h.x, -h.y, h.z) + center,
				new Vector3(h.x, -h.y, -h.z) + center,
				new Vector3(h.x, h.y, -h.z) + center,
				new Vector3(h.x, h.y, h.z) + center
			);
			poly.AddFace4(
				new Vector3(0f, -1f, 0f),
				new Vector3(-h.x, -h.y, -h.z) + center,
				new Vector3(h.x, -h.y, -h.z) + center,
				new Vector3(h.x, -h.y, h.z) + center,
				new Vector3(-h.x, -h.y, h.z) + center
			);
			poly.AddFace4(
				new Vector3(0f, 1f, 0f),
				new Vector3(-h.x, h.y, h.z) + center,
				new Vector3(h.x, h.y, h.z) + center,
				new Vector3(h.x, h.y, -h.z) + center,
				new Vector3(-h.x, h.y, -h.z) + center
			);
			poly.AddFace4(
				new Vector3(0f, 0f, -1f),
				new Vector3(h.x, -h.y, -h.z) + center,
				new Vector3(-h.x, -h.y, -h.z) + center,
				new Vector3(-h.x, h.y, -h.z) + center,
				new Vector3(h.x, h.y, -h.z) + center
			);
			poly.AddFace4(
				new Vector3(0f, 0f, 1f),
				new Vector3(-h.x, -h.y, h.z) + center,
				new Vector3(h.x, -h.y, h.z) + center,
				new Vector3(h.x, h.y, h.z) + center,
				new Vector3(-h.x, h.y, h.z) + center
			);
			return poly;
		}

		private void AddFace4(Vector3 normal, Vector3 a, Vector3 b, Vector3 c, Vector3 d) {
			var vertexStart = vertices.Length;
			vertices.Add(a);
			vertices.Add(b);
			vertices.Add(c);
			vertices.Add(d);
			faces.Add(new ConvexFace(vertexStart, 4, normal));
		}

		// Keep half-space { x : dot(normal, x) <= clippingPlaneOffsetFromOrigin }.
		public void ClipByPlane(Vector3 normal, float clippingPlaneOffsetFromOrigin) {
			if (faces.Length == 0) return;

			var newFaces = new NativeList<ConvexFace>(initialCapacity: faces.Length + 1, Allocator.Temp);
			var newVertices = new NativeList<Vector3>(initialCapacity: vertices.Length + 8, Allocator.Temp);
			var capEdges = new NativeList<ClipEdge>(Allocator.Temp);

			for (var faceIndex = 0; faceIndex < faces.Length; faceIndex++) {
				ClipFace(faces[faceIndex], normal, clippingPlaneOffsetFromOrigin, newFaces, newVertices, capEdges);
			}

			BuildCapFace(capEdges, normal, newFaces, newVertices);

			capEdges.Dispose();
			faces.Dispose();
			vertices.Dispose();
			faces = newFaces;
			vertices = newVertices;
		}

		private void ClipFace(
			ConvexFace face, Vector3 clipPlaneNormal, float clipPlaneOffsetFromOrigin,
			NativeList<ConvexFace> newFaces, NativeList<Vector3> newVertices,
			NativeList<ClipEdge> capEdges
		) {
			var clipped = new NativeList<Vector3>(initialCapacity: face.vertexCount + 2, allocator: Allocator.Temp);
			Vector3 entryCut = default, exitCut = default;
			var hasEntry = false;
			var hasExit = false;

			for (var i = 0; i < face.vertexCount; i++) {
				var a = vertices[face.vertexStart + i];
				var b = vertices[face.vertexStart + ((i + 1) % face.vertexCount)];
				var da = Vector3.Dot(clipPlaneNormal, a) - clipPlaneOffsetFromOrigin;
				var db = Vector3.Dot(clipPlaneNormal, b) - clipPlaneOffsetFromOrigin;
				if (Mathf.Abs(da) <= EPSILON) da = 0f;
				if (Mathf.Abs(db) <= EPSILON) db = 0f;
				var aIn = da <= 0f;
				var bIn = db <= 0f;

				if (aIn) clipped.Add(a);
				if (aIn && !bIn) {
					var t = da / (da - db);
					var intersectionPoint = Vector3.Lerp(a, b, t);
					clipped.Add(intersectionPoint);
					exitCut = intersectionPoint;
					hasExit = true;
				} else if (!aIn && bIn) {
					var t = da / (da - db);
					var intersectionPoint = Vector3.Lerp(a, b, t);
					clipped.Add(intersectionPoint);
					entryCut = intersectionPoint;
					hasEntry = true;
				}
			}

			RemoveNearDuplicates(clipped);
			if (clipped.Length >= 3) AddFace(face.normal, clipped, newFaces, newVertices);
			if (hasEntry && hasExit) capEdges.Add(new ClipEdge(entryCut, exitCut));
			clipped.Dispose();
		}

		private static void AddFace(
			Vector3 normal, NativeList<Vector3> sourceVertices,
			NativeList<ConvexFace> targetFaces, NativeList<Vector3> targetVertices
		) {
			var vertexStart = targetVertices.Length;
			for (var i = 0; i < sourceVertices.Length; i++) {
				targetVertices.Add(sourceVertices[i]);
			}

			targetFaces.Add(new ConvexFace(vertexStart, sourceVertices.Length, normal));
		}

		private static void RemoveNearDuplicates(NativeList<Vector3> pts) {
			if (pts.Length < 2) return;

			var e2 = EPSILON * EPSILON;
			var writeIndex = 1;
			var previous = pts[0];
			for (var readIndex = 1; readIndex < pts.Length; readIndex++) {
				var point = pts[readIndex];
				if ((previous - point).sqrMagnitude <= e2) continue;

				pts[writeIndex] = point;
				writeIndex++;
				previous = point;
			}

			while (pts.Length > writeIndex) {
				pts.RemoveAt(pts.Length - 1);
			}

			if (pts.Length > 1 && (pts[0] - pts[pts.Length - 1]).sqrMagnitude < e2) {
				pts.RemoveAt(pts.Length - 1);
			}
		}

		private static void BuildCapFace(
			NativeList<ClipEdge> edges, Vector3 normal,
			NativeList<ConvexFace> targetFaces, NativeList<Vector3> targetVertices
		) {
			if (edges.Length < 3) return;
			var e2 = EPSILON * EPSILON * 100f;

			var points = new NativeList<Vector3>(Allocator.Temp);
			for (var i = 0; i < edges.Length; i++) {
				AddUnique(points, edges[i].entry, e2);
				AddUnique(points, edges[i].exit, e2);
			}

			if (points.Length < 3) {
				points.Dispose();
				return;
			}

			var centroid = Vector3.zero;
			for (var i = 0; i < points.Length; i++) {
				centroid += points[i];
			}
			centroid /= points.Length;

			var reference = Mathf.Abs(normal.y) > 0.9f ? Vector3.right : Vector3.up;
			var u = Vector3.Cross(normal, reference).normalized;
			var v = Vector3.Cross(normal, u).normalized;

			points.Sort(Comparer<Vector3>.Create(
				(p1, p2) => {
					var d1 = p1 - centroid;
					var d2 = p2 - centroid;
					var a1 = Mathf.Atan2(Vector3.Dot(v, d1), Vector3.Dot(u, d1));
					var a2 = Mathf.Atan2(Vector3.Dot(v, d2), Vector3.Dot(u, d2));
					return a1.CompareTo(a2);
				}
			));

			AddFace(normal, points, targetFaces, targetVertices);
			points.Dispose();
		}

		private static void AddUnique(NativeList<Vector3> list, Vector3 p, float error) {
			for (var i = 0; i < list.Length; i++) {
				if ((p - list[i]).sqrMagnitude < error) return;
			}

			list.Add(p);
		}

		public void WriteTo(ref NativeStream.Writer writer) {
			writer.Write(faces.Length);
			for (var faceIndex = 0; faceIndex < faces.Length; faceIndex++) {
				var face = faces[faceIndex];
				writer.Write(face.normal);
				writer.Write(face.vertexCount);
				for (var vertexIndex = 0; vertexIndex < face.vertexCount; vertexIndex++) {
					writer.Write(vertices[face.vertexStart + vertexIndex]);
				}
			}
		}

		public void Dispose() {
			if (faces.IsCreated) faces.Dispose();
			if (vertices.IsCreated) vertices.Dispose();
		}
	}
}
