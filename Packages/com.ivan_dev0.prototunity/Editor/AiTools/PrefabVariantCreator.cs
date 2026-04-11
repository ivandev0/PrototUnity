using System;
using PrototUnity.Editor.Utils;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PrototUnity.Editor.AiTools {
	public static class PrefabVariantCreator {
		private const string ItemsFolder = "Assets/Prefabs/Visuals/Items";
		private const string ParentPrefabPath = "Assets/Prefabs/Visuals/Items/VisualParentPrefab.prefab";

		[MenuItem("Tools/Create Visual Parent Prefab and Variants")]
		public static void CreateParentAndVariants() {
			// Step 1: Create the base parent prefab if it doesn't exist
			GameObject basePrefab = PrefabUtils.CreateOrGetPrefab(ParentPrefabPath);

			// Step 2: Get all prefabs in the Items folder
			string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ItemsFolder });

			int convertedCount = 0;
			int skippedCount = 0;

			foreach (string guid in prefabGuids) {
				string path = AssetDatabase.GUIDToAssetPath(guid);

				// Skip the parent prefab itself
				if (path == ParentPrefabPath)
					continue;

				GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (prefab == null)
					continue;

				// Check if it's already a variant
				if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant) {
					Debug.Log($"Skipping {path} - already a variant");
					skippedCount++;
					continue;
				}

				// Convert to variant
				if (ConvertToVariant(prefab, path, basePrefab)) {
					convertedCount++;
				}
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log($"Done! Converted {convertedCount} prefabs to variants. Skipped {skippedCount} (already variants).");
			EditorUtility.DisplayDialog("Visual Prefab Variant Creator",
				$"Converted {convertedCount} prefabs to variants.\nSkipped {skippedCount} (already variants).",
				"OK");
		}
	
		private static bool ConvertToVariant(GameObject originalPrefab, string originalPath, GameObject newParent) {
			try {
				// Instantiate the original prefab to preserve its contents
				GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(originalPrefab);
				PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

				// Create a variant of the base prefab
				GameObject variantInstance = (GameObject)PrefabUtility.InstantiatePrefab(newParent);

				// Copy all children from original to variant
				// Children are nested prefab instances, so we need to get their source and re-instantiate
				int childCount = instance.transform.childCount;
				for (int i = childCount - 1; i >= 0; i--) {
					Transform child = instance.transform.GetChild(i);
					child.transform.SetParent(variantInstance.transform, true);
				}

				// Copy all components (except Transform) from original root to variant root
				Component[] components = instance.GetComponents<Component>();
				foreach (Component comp in components) {
					if (comp is Transform)
						continue;

					ComponentUtility.CopyComponent(comp);
					ComponentUtility.PasteComponentAsNew(variantInstance);
				}

				// Copy transform properties
				variantInstance.transform.localPosition = instance.transform.localPosition;
				variantInstance.transform.localRotation = instance.transform.localRotation;
				variantInstance.transform.localScale = instance.transform.localScale;

				// Rename to match original
				variantInstance.name = originalPrefab.name;

				// Save as variant, overwriting the original
				PrefabUtility.SaveAsPrefabAsset(variantInstance, originalPath);

				// Cleanup
				Object.DestroyImmediate(instance);
				Object.DestroyImmediate(variantInstance);

				Debug.Log($"Converted {originalPath} to variant");
				return true;
			}
			catch (Exception e) {
				Debug.LogError($"Failed to convert {originalPath}: {e.Message}");
				return false;
			}
		}
	}
}