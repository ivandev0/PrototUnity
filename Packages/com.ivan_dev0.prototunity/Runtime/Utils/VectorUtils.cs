using UnityEngine;

namespace PrototUnity.Utils {
	public static class VectorUtils {
		public static Vector3 Clamp01(this Vector3 vector) {
			return new Vector3(Mathf.Clamp01(vector.x), Mathf.Clamp01(vector.y), Mathf.Clamp01(vector.z));
		}
		
		public static Vector3Int RoundToInt(this Vector3 vector) {
			return new Vector3Int(Mathf.RoundToInt(vector.x), Mathf.RoundToInt(vector.y), Mathf.RoundToInt(vector.z));
		}
	}
}