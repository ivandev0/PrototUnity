using UnityEditor;
using UnityEngine;

namespace PrototUnity.Editor.Utils {
	public static class PrefabUtils {
		public static GameObject CreateOrGetPrefab(string path) {
			// Check if parent prefab already exists
			var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (existingPrefab != null) {
				Debug.Log("VisualParentPrefab already exists, using existing one.");
				return existingPrefab;
			}

			// Create a new empty GameObject for the parent prefab
			var parentObj = new GameObject("VisualParentPrefab");

			// Save as prefab
			var savedPrefab = PrefabUtility.SaveAsPrefabAsset(parentObj, path);

			// Destroy the temporary scene object
			Object.DestroyImmediate(parentObj);

			Debug.Log($"Created parent prefab at {path}");

			return savedPrefab;
		}

	}
}