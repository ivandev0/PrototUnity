using UnityEngine;

namespace PrototUnity.MarchingCubesMeshGenerator {
	public abstract class AbstractMeshGenerator : MonoBehaviour {
		public abstract void GeneratePoints();
		public abstract void GenerateMesh();
		public abstract void Render();
	}
}