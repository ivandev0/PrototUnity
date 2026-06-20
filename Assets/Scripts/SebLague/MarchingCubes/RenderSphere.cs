using UnityEngine;

namespace SebLague.MarchingCubes {
	public class RenderSphere : MonoBehaviour {
		[SerializeField] private ComputeShader computeShader;

		private RenderTexture pointsTexture;

		private const int threadGroupSize = 8;
		
		private static readonly int pointsID = Shader.PropertyToID("points");
		private static readonly int textureSizeID = Shader.PropertyToID("textureSize");
		private static readonly int radiusID = Shader.PropertyToID("radius");

		private void CreateBuffers(int numPointsPerAxis) {
			pointsTexture = Utils.CreateRenderCubeTexture(numPointsPerAxis, "pointsTexture");
		}

		private void ReleaseBuffers() {
			pointsTexture?.Release();
		}

		private void OnDestroy() {
			if (Application.isPlaying) {
				ReleaseBuffers();
			}
		}

		public RenderTexture Generate(int numPointsPerAxis, float radius) {
			CreateBuffers(numPointsPerAxis);
			var numThreadsPerAxis = Mathf.CeilToInt(numPointsPerAxis / (float) threadGroupSize);

			computeShader.SetTexture(0, pointsID, pointsTexture);
			computeShader.SetInt(textureSizeID, numPointsPerAxis);
			computeShader.SetFloat(radiusID, radius);

			// Dispatch shader
			computeShader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);
			return pointsTexture;
		}
	}
}