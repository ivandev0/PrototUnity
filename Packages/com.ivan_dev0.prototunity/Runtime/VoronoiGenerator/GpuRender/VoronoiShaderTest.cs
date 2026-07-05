using JetBrains.Annotations;
using UnityEngine;

namespace PrototUnity.VoronoiGenerator.GpuRender {
	public class VoronoiShaderTest : MonoBehaviour {
		[SerializeField] private VoronoiGPURender voronoiGPURender;
		[SerializeField] private AbstractVoronoiComputeGenerator voronoiGenerator;

		[SerializeField] private bool renderAll = true;
		[SerializeField] private int[] ids;

		[CanBeNull] private ComputeBuffer buffer;
		
		private void Update() {
			buffer?.Release();

			if (renderAll) {
				var size = voronoiGenerator.Size.x * voronoiGenerator.Size.y * voronoiGenerator.Size.z;
				buffer = new ComputeBuffer(size, sizeof(int), ComputeBufferType.Structured);
				var allIds = new int[size];
				for (var i = 0; i < size; i++) {
					allIds[i] = i;
				}
				buffer.SetData(allIds);
				voronoiGPURender.SetSpecificIdsToRender(buffer);
			} else if (ids.Length == 0) {
				voronoiGPURender.SetSpecificIdsToRender(null);
			} else {
				buffer = new ComputeBuffer(ids.Length, sizeof(int));
				buffer.SetData(ids);
				voronoiGPURender.SetSpecificIdsToRender(buffer);
			}
		}
	}
}