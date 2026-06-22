using UnityEngine;

namespace SebLague.MarchingCubes {
	public class ComputeHelper {
		/// Convenience method for dispatching a compute shader.
		/// It calculates the number of thread groups based on the number of iterations needed.
		public static void Dispatch(
			ComputeShader cs,
			int numIterationsX,
			int numIterationsY = 1,
			int numIterationsZ = 1,
			int kernelIndex = 0
		) {
			var threadGroupSizes = GetThreadGroupSizes(cs, kernelIndex);
			var numGroupsX = Mathf.CeilToInt(numIterationsX / (float)threadGroupSizes.x);
			var numGroupsY = Mathf.CeilToInt(numIterationsY / (float)threadGroupSizes.y);
			var numGroupsZ = Mathf.CeilToInt(numIterationsZ / (float)threadGroupSizes.y);
			cs.Dispatch(kernelIndex, numGroupsX, numGroupsY, numGroupsZ);
		}

		public static Vector3Int GetThreadGroupSizes(ComputeShader compute, int kernelIndex = 0) {
			uint x, y, z;
			compute.GetKernelThreadGroupSizes(kernelIndex, out x, out y, out z);
			return new Vector3Int((int)x, (int)y, (int)z);
		}
	}
}