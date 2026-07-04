using UnityEngine;

namespace PrototUnity.VoronoiGenerator {
	public abstract class AbstractVoronoiComputeGenerator : MonoBehaviour {
		public struct VoronoiCell {
			public uint vertexCount;
			public uint vertexStart;
		}
		
		public abstract void Generate();
		public abstract (ComputeBuffer cells, ComputeBuffer vertices) GetGeneratedData();
	}
}