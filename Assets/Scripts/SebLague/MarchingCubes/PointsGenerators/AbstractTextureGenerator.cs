using UnityEngine;

namespace SebLague.MarchingCubes.PointsGenerators {
	public abstract class AbstractTextureGenerator : MonoBehaviour {
		[SerializeField] private int numPointsPerAxis;
		public int NumPointsPerAxis => numPointsPerAxis;
		
		public abstract RenderTexture GenerateTexture();
	}
}