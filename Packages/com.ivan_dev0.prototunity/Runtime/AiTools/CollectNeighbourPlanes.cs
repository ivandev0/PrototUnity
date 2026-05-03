using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace PrototUnity.AiTools {
	public static class CollectNeighbourPlanes {
		internal const float defaultNormalTolerance = 0.02f;
		internal const float defaultPlaneTolerance = 0.1f;
		
		public struct GameObjectPairInfo {
			public GameObject owner;
			public List<FaceInfo> meshes;
			[ItemCanBeNull] public List<FaceInfo> pairs;
		}
		
		public class FaceInfo {
			public GameObject target;
			public Vector3 normal;
			public Vector3 center;
		}

		public static List<GameObjectPairInfo> GatherNeighbors<TParentMarker>(
			GameObject gameObject, float normalTolerance = defaultNormalTolerance, float planeTolerance = defaultPlaneTolerance
		) where TParentMarker : Component {
			var meshFilters = gameObject.GetComponentsInChildren<MeshFilter>(includeInactive: true);
			var faces = CollectFaces(meshFilters);
			var result = faces
				.Where(it => it != null)
				.GroupBy(it => it.target.GetComponentInParent<TParentMarker>()).Select(group => new GameObjectPairInfo() {
					owner = group.Key.gameObject,
					meshes = group.ToList(),
					pairs = new List<FaceInfo>(new FaceInfo[group.Count()])
				}).ToList();
			var pairs = 0;
			
			for (var i = 0; i < result.Count; i++) {
				for (var thisFaceIndex = 0; thisFaceIndex < result[i].meshes.Count; thisFaceIndex++) {
					var thisFace = result[i].meshes[thisFaceIndex];
					for (var j = i + 1; j < result.Count; j++) {
						for (var otherFaceIndex = 0; otherFaceIndex < result[j].meshes.Count; otherFaceIndex++) {
							var otherFace = result[j].meshes[otherFaceIndex];
							
							if ((thisFace.normal + otherFace.normal).sqrMagnitude > normalTolerance * normalTolerance) continue;
							if ((thisFace.center - otherFace.center).sqrMagnitude > planeTolerance * planeTolerance) continue;
							
							result[i].pairs[thisFaceIndex] = otherFace;
							result[j].pairs[otherFaceIndex] = thisFace;
							pairs++;
						}
					}
				}
			}

			Debug.Log($"Collect {pairs} neighbor face pair(s) ({pairs * 2} faces).");
			return result;
		}

		[ItemCanBeNull]
		private static List<FaceInfo> CollectFaces(MeshFilter[] meshFilters) {
			var result = new List<FaceInfo>();
			foreach (var mf in meshFilters) {
				var mesh = mf.sharedMesh;
				if (mesh == null || mesh.vertexCount < 3) {
					result.Add(null);
					continue;
				}
				
				var vertices = mesh.vertices;
				var normals = mesh.normals;
				var localNormal = normals is { Length: > 0 }
					? normals[0]
					: ComputeNormal(vertices);
				if (localNormal.sqrMagnitude < 1e-10f) {
					result.Add(null);
					continue;
				}

				// TODO would be nice to check that all vertices are lying on a single plane
				var worldNormal = mf.transform.TransformDirection(localNormal).normalized;
				var verticesWorld = vertices.Select(it => mf.transform.TransformPoint(it)).ToArray();
				var center = Average(verticesWorld);

				result.Add(new FaceInfo {
					target = mf.gameObject,
					normal = worldNormal,
					center = center,
				});
			}

			return result;
		}

		private static Vector3 ComputeNormal(Vector3[] verts) {
			if (verts.Length < 3) return Vector3.zero;
			return Vector3.Cross(verts[1] - verts[0], verts[2] - verts[0]).normalized;
		}
		
		private static Vector3 Average(Vector3[] points) {
			if (points.Length == 0) return Vector3.zero;
			var sum = points.Aggregate(Vector3.zero, (current, t) => current + t);
			return sum / points.Length;
		}
	}
}