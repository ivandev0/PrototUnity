using System.Runtime.InteropServices;
using JetBrains.Annotations;
using UnityEngine;

namespace PrototUnity.VoronoiGenerator.GpuRender {
	public class VoronoiShaderTest : MonoBehaviour {
		[SerializeField] private VoronoiGPURender voronoiGPURender;
		[SerializeField] private AbstractVoronoiComputeGenerator voronoiGenerator;

		private struct Voxel
		{
			uint id;
			Vector3 position;

			public Voxel(uint id, Vector3 position) {
				this.id = id;
				this.position = position;
			}
		};
		
		[SerializeField] private bool renderAll = true;
		[SerializeField] private uint[] ids;

		[CanBeNull] private ComputeBuffer buffer;
		
		private void Update() {
			buffer?.Release();

			if (renderAll) {
				var size = voronoiGenerator.Size.x * voronoiGenerator.Size.y * voronoiGenerator.Size.z;
				buffer = new ComputeBuffer(size, Marshal.SizeOf(typeof(Voxel)), ComputeBufferType.Structured);
				var allIds = new Voxel[size];
				for (uint i = 0; i < size; i++) {
					allIds[i] = new Voxel(i, Vector3.zero);
				}
				buffer.SetData(allIds);
				voronoiGPURender.SetSpecificIdsToRender(buffer);
			} else if (ids.Length == 0) {
				voronoiGPURender.SetSpecificIdsToRender(null);
			} else {
				buffer = new ComputeBuffer(ids.Length, Marshal.SizeOf(typeof(Voxel)));
				var filteredIds = new Voxel[ids.Length];
				for (var i = 0; i < ids.Length; i++) {
					filteredIds[i] = new Voxel(ids[i], Vector3.zero);
				}
				buffer.SetData(filteredIds);
				voronoiGPURender.SetSpecificIdsToRender(buffer);
			}
		}
	}
}