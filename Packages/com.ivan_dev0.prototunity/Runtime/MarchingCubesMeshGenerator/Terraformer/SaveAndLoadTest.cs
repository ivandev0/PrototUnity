using UnityEngine;

namespace PrototUnity.MarchingCubesMeshGenerator.Terraformer {
	public class SaveAndLoadTest : MonoBehaviour {
		[SerializeField] private MeshGeneratorBase meshGenerator;

		private MeshGeneratorBase.ISaveData data;

		private void OnEnable() {
			data = null;
		}

		[ContextMenu("Save")]
		private void Save() {
			data = meshGenerator.Save();
		}
		
		[ContextMenu("Load")]
		private void Load() {
			if (data == null) {
				Debug.Log("No data");
				return;
			}
			meshGenerator.Load(data);
		}
	}
}