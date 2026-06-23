using UnityEngine;

namespace SebLague.MarchingCubes.PointsGenerators {
	public class RenderCube : AbstractTextureGenerator {
		[SerializeField] private ComputeShader computeShader;
		[SerializeField] private float size;

		private RenderTexture pointsTexture;
		
		private static readonly int pointsID = Shader.PropertyToID("points");
		private static readonly int textureSizeID = Shader.PropertyToID("textureSize");
		private static readonly int sizeID = Shader.PropertyToID("size");

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

		public override RenderTexture GenerateTexture() {
			CreateBuffers(NumPointsPerAxis);

			computeShader.SetTexture(0, pointsID, pointsTexture);
			computeShader.SetInt(textureSizeID, NumPointsPerAxis);
			computeShader.SetFloat(sizeID, size);

			ComputeHelper.Dispatch(computeShader, NumPointsPerAxis, NumPointsPerAxis, NumPointsPerAxis);
			return pointsTexture;
		}
	}
}