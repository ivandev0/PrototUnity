using UnityEngine;

namespace PrototUnity.VoronoiGenerator {
	public abstract class AbstractVoronoiComputeGenerator : MonoBehaviour {
		[SerializeField] private Vector3Int size = new(10, 10, 10);
		public Vector3Int Size => size;

		public ComputeBuffer VoronoiCellsBuffer { get; protected set; }
		public ComputeBuffer VoronoiVerticesBuffer { get; protected set; }

		public struct VoronoiCell {
			public uint vertexCount;
			public uint vertexStart;
		}
		
		public abstract void Generate();
		
		protected virtual void ReleaseBuffers() {
			if (VoronoiCellsBuffer != null) {
				VoronoiCellsBuffer.Release();
				VoronoiCellsBuffer = null;
			}

			if (VoronoiVerticesBuffer != null) {
				VoronoiVerticesBuffer.Release();
				VoronoiVerticesBuffer = null;
			}
		}

		private void OnDestroy() {
			ReleaseBuffers();
		}

		private void OnDisable() {
			ReleaseBuffers();
		}
	}
}