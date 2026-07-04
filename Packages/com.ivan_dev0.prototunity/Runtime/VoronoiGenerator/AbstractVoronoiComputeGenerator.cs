using UnityEngine;

namespace PrototUnity.VoronoiGenerator {
	public abstract class AbstractVoronoiComputeGenerator : MonoBehaviour {
		[SerializeField] public Vector3Int size = new(10, 10, 10);
		
		public struct VoronoiCell {
			public uint vertexCount;
			public uint vertexStart;
		}
		
		public abstract void Generate();
		public abstract (ComputeBuffer cells, ComputeBuffer vertices) GetGeneratedData();
	}
}