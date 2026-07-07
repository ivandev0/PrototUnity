using UnityEngine;

namespace PrototUnity.MarchingCubesMeshGenerator {
	public class GeneratorTest : MonoBehaviour {
		[SerializeField] private MeshGeneratorBase generator;
		
		private void Awake() {
			generator.Generate();
		}
	}
}