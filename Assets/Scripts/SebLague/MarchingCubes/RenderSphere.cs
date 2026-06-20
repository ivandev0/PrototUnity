using System;
using UnityEngine;

namespace SebLague.MarchingCubes {
	public class RenderSphere : MonoBehaviour {
		[SerializeField] private ComputeShader computeShader;

		[SerializeField] private int numPointsPerAxis;
		[SerializeField] private float radius;

		private RenderTexture pointsTexture;

		const int threadGroupSize = 8;
		
		private static readonly int pointsID = Shader.PropertyToID("points");
		private static readonly int textureSizeID = Shader.PropertyToID("textureSize");
		private static readonly int radiusID = Shader.PropertyToID("radius");

		private void Awake() {
			CreateBuffers();
			Generate();
		}

		private void CreateBuffers() {
			pointsTexture = Utils.CreateRenderCubeTexture(numPointsPerAxis, "pointsTexture");
		}

		void ReleaseBuffers() {
			pointsTexture?.Release();
		}

		void OnDestroy() {
			if (Application.isPlaying) {
				ReleaseBuffers();
			}
		}

		public RenderTexture Generate() {
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