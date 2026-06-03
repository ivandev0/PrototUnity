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
			GameObject gameObject,
			float normalTolerance = defaultNormalTolerance, 
			float planeTolerance = defaultPlaneTolerance,
			bool includeInactive = false
		) where TParentMarker : Component {
			var meshFilters = gameObject.GetComponentsInChildren<MeshFilter>(includeInactive: includeInactive);
			var faces = CollectFaces(meshFilters);
			var facePairs = GatherPairs(faces, normalTolerance, planeTolerance);
			return Group<TParentMarker>(facePairs);
		}

		[ItemCanBeNull]
		private static FaceInfo[] CollectFaces(MeshFilter[] meshFilters) {
			var result = new FaceInfo[meshFilters.Length];
			for (var index = 0; index < meshFilters.Length; index++) {
				var mf = meshFilters[index];
				var mesh = mf.sharedMesh;
				if (mesh == null || mesh.vertexCount < 3) {
					result[index] = null;
					continue;
				}

				var vertices = mesh.vertices;
				var normals = mesh.normals;
				var localNormal = normals is { Length: > 0 }
					? normals[0]
					: ComputeNormal(vertices);
				if (localNormal.sqrMagnitude < 1e-10f) {
					result[index] = null;
					continue;
				}

				// TODO would be nice to check that all vertices are lying on a single plane
				var worldNormal = mf.transform.TransformDirection(localNormal).normalized;
				var center = vertices.Aggregate(Vector3.zero, (current, t) => current + mf.transform.TransformPoint(t));
				center /= vertices.Length;

				result[index] = new FaceInfo {
					target = mf.gameObject,
					normal = worldNormal,
					center = center,
				};
			}

			return result;
		}
		
		private static Dictionary<FaceInfo, FaceInfo> GatherPairs(
			[ItemCanBeNull] FaceInfo[] faces,
			float normalTolerance,
			float planeTolerance
		) {
			var normalToleranceSqr = normalTolerance * normalTolerance;
			var planeToleranceSqr = planeTolerance * planeTolerance;
			var bucketSize = Mathf.Max(planeTolerance, 0.0001f);
			var buckets = BuildCenterBuckets(faces, bucketSize);
			var pairLookup = new Dictionary<FaceInfo, FaceInfo>();
			
			for (var faceIndex = 0; faceIndex < faces.Length; faceIndex++) {
				var thisFace = faces[faceIndex];
				if (thisFace == null) continue;
				
				var bucket = GetBucketKey(thisFace.center, bucketSize);
				var candidates = new List<FaceInfo>();
				for (var x = -1; x <= 1; x++) {
					for (var y = -1; y <= 1; y++) {
						for (var z = -1; z <= 1; z++) {
							var key = new Vector3Int(bucket.x + x, bucket.y + y, bucket.z + z);
							if (!buckets.TryGetValue(key, out var bucketFaces)) continue;
							
							foreach (var candidate in bucketFaces) {
								if (candidate == thisFace) continue;
								candidates.Add(candidate);
							}
						}
					}
				}
				
				pairLookup[thisFace] = null;
				foreach (var otherFace in candidates) {
					if ((thisFace.normal + otherFace.normal).sqrMagnitude > normalToleranceSqr) continue;
					if ((thisFace.center - otherFace.center).sqrMagnitude > planeToleranceSqr) continue;
					
					pairLookup[thisFace] = otherFace;
					break;
				}
			}
			
			return pairLookup;
		}
		
		private static List<GameObjectPairInfo> Group<TParentMarker>(
			Dictionary<FaceInfo, FaceInfo> facePairs
		) where TParentMarker : Component {
			var result = new List<GameObjectPairInfo>();
			var ownerIndexes = new Dictionary<TParentMarker, int>();
			foreach (var (face, pair) in facePairs) {
				var owner = face.target.GetComponentInParent<TParentMarker>();
				if (owner == null) {
					throw new MissingComponentException($"{face.target.name} is missing a parent {typeof(TParentMarker).Name} component.");
				}
				
				if (!ownerIndexes.TryGetValue(owner, out var index)) {
					index = result.Count;
					ownerIndexes.Add(owner, index);
					result.Add(new GameObjectPairInfo {
						owner = owner.gameObject,
						meshes = new List<FaceInfo>(),
						pairs = new List<FaceInfo>()
					});
				}
				
				var info = result[index];
				info.meshes.Add(face);
				info.pairs.Add(pair);
			}
			
			return result;
		}
		
		private static Dictionary<Vector3Int, List<FaceInfo>> BuildCenterBuckets(
			[ItemCanBeNull] FaceInfo[] faces,
			float bucketSize
		) {
			var buckets = new Dictionary<Vector3Int, List<FaceInfo>>();
			foreach (var face in faces) {
				if (face == null) continue;
				
				var key = GetBucketKey(face.center, bucketSize);
				if (!buckets.TryGetValue(key, out var bucketFaces)) {
					bucketFaces = new List<FaceInfo>();
					buckets.Add(key, bucketFaces);
				}
				
				bucketFaces.Add(face);
			}
			
			return buckets;
		}
		
		private static Vector3Int GetBucketKey(Vector3 point, float bucketSize) {
			return new Vector3Int(
				Mathf.FloorToInt(point.x / bucketSize),
				Mathf.FloorToInt(point.y / bucketSize),
				Mathf.FloorToInt(point.z / bucketSize)
			);
		}

		private static Vector3 ComputeNormal(Vector3[] verts) {
			if (verts.Length < 3) return Vector3.zero;
			return Vector3.Cross(verts[1] - verts[0], verts[2] - verts[0]).normalized;
		}
	}
}
