using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PrototUnity.Editor.AiTools {
	public class HideNeighbourPlanesScript : MonoBehaviour {
		[FormerlySerializedAs("m_NormalTolerance")] [Tooltip("Max |n_A + n_B| for two face normals to count as opposite.")] [SerializeField, Range(0.001f, 0.1f)]
		private float normalTolerance = 0.02f;

		[Tooltip("Max plane-distance gap to treat two faces as coplanar. Increase if Cell Shrink on the generator is large.")]
		[SerializeField]
		private float planeTolerance = 0.1f;

		[ContextMenu("Hide Neighbor Faces")]
		public void HideNeighbors() {
			HideNeighbourPlanes.HideNeighbors(GetComponentsInChildren<MeshFilter>(includeInactive: true), normalTolerance, planeTolerance);
		}

		[ContextMenu("Show All Faces")]
		public void ShowAll() {
			HideNeighbourPlanes.ShowAll(GetComponentsInChildren<MeshFilter>(includeInactive: true));
		}
	}

	public static class HideNeighbourPlanes {
		private struct FaceInfo {
			public GameObject target;
			public Vector3 normal;
			public float distanceToPlaneFromOrigin;
		}

		public static void HideNeighbors(MeshFilter[] meshFilters, float normalTolerance, float planeTolerance) {
			var faces = CollectFaces(meshFilters);
			var pairs = 0;
			var used = new bool[faces.Count];

			for (var i = 0; i < faces.Count; i++) {
				if (used[i]) continue;
				for (var j = i + 1; j < faces.Count; j++) {
					if (used[j]) continue;
					if ((faces[i].normal + faces[j].normal).sqrMagnitude > normalTolerance * normalTolerance) continue;
					if (Mathf.Abs(faces[i].distanceToPlaneFromOrigin + faces[j].distanceToPlaneFromOrigin) > planeTolerance) continue;

					faces[i].target.SetActive(false);
					faces[j].target.SetActive(false);
					used[i] = used[j] = true;
					pairs++;
					break;
				}
			}

			Debug.Log($"Hid {pairs} neighbor face pair(s) ({pairs * 2} faces).");
		}

		public static void ShowAll(MeshFilter[] meshFilters) {
			foreach (var meshFilter in meshFilters) {
				meshFilter.gameObject.SetActive(true);
			}
		}

		private static List<FaceInfo> CollectFaces(MeshFilter[] meshFilters) {
			var result = new List<FaceInfo>();
			foreach (var mf in meshFilters) {
				var mesh = mf.sharedMesh;
				if (mesh == null || mesh.vertexCount < 3) continue;

				var vertices = mesh.vertices;
				var normals = mesh.normals;
				var localNormal = normals is { Length: > 0 }
					? normals[0]
					: ComputeNormal(vertices);
				if (localNormal.sqrMagnitude < 1e-10f) continue;

				var worldNormal = mf.transform.TransformDirection(localNormal).normalized;
				var worldPoint = mf.transform.TransformPoint(vertices[0]);
				var distanceFromOrigin = Vector3.Dot(worldNormal, worldPoint);

				result.Add(new FaceInfo {
					target = mf.gameObject,
					normal = worldNormal,
					distanceToPlaneFromOrigin = distanceFromOrigin,
				});
			}

			return result;
		}

		private static Vector3 ComputeNormal(Vector3[] verts) {
			if (verts.Length < 3) return Vector3.zero;
			return Vector3.Cross(verts[1] - verts[0], verts[2] - verts[0]).normalized;
		}
	}
}