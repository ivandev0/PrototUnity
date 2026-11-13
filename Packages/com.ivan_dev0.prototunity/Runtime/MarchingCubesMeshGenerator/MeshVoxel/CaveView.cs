using PrototUnity.CutThrough;
using UnityEngine;

namespace PrototUnity.MarchingCubesMeshGenerator.MeshVoxel {
	public class CaveView : MonoBehaviour {
		[SerializeField] private TargetHiddenNotifier notifier;
		[Tooltip("Material of the voxels")]
		[SerializeField] private Material material;

		private static readonly int showOnlyCavesID = Shader.PropertyToID("showOnlyCaves");

		private void OnEnable() {
			notifier.TargetBecomeHiddenEvent += OnTargetChangeVisibility;
		}

		private void OnDisable() {
			notifier.TargetBecomeHiddenEvent -= OnTargetChangeVisibility;
			SetShowOnlyCaves(false);
		}

		private void OnTargetChangeVisibility(bool hidden) {
			SetShowOnlyCaves(hidden);
		}
		
		private void SetShowOnlyCaves(bool showOnlyCaves) {
			material.SetFloat(showOnlyCavesID, showOnlyCaves ? 1 : 0);
		}
	}
}
