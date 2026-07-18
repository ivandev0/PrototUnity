using PrototUnity.MarchingCubesMeshGenerator.MeshVoxel;
using PrototUnity.PointsGenerators;
using UnityEngine;

namespace PrototUnity.MarchingCubesMeshGenerator.Terraformer.Tests {
	internal class VoxelModificationTest : MonoBehaviour {
		[SerializeField] private AbstractTextureGenerator textureGenerator;
		[SerializeField] private VoxelMeshGenerator voxelMeshGenerator;
		[SerializeField] private GameObject prefab;

		private Matrix4x4 scaling;

		private void OnEnable() {
			scaling = Matrix4x4.Scale(voxelMeshGenerator.BoundSize / textureGenerator.NumPointsPerAxis);
			voxelMeshGenerator.VoxelsModifiedEvent += OnVoxelsModifiedEvent;
		}

		private void OnDisable() {
			voxelMeshGenerator.VoxelsModifiedEvent -= OnVoxelsModifiedEvent;
		}

		private void OnVoxelsModifiedEvent(AbstractMeshGenerator.Modification[] modifications) {
			foreach (var modification in modifications) {
				if (modification is { after: > 0, before: < 0 }) {
					var newObject = Instantiate(prefab);
					var cube = newObject.transform.GetChild(0);
					cube.localPosition = modification.position;
					newObject.transform.localScale = scaling.MultiplyVector(Vector3.one);
				}
			}
		}
	}
}