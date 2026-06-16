using System;
using UnityEngine;

namespace SebLague.MarchingCubes {
	public class RenderSphere : MonoBehaviour {
		[SerializeField] private ComputeShader computeShader;

		[SerializeField] private int numPointsPerAxis;
		[SerializeField] private float boundSize;
		[SerializeField] private Vector3 center;
		[SerializeField] private Vector3 offset;
		[SerializeField] private float radius;

		private ComputeBuffer pointsBuffer;

		const int threadGroupSize = 8;
		
		private static readonly int pointsID = Shader.PropertyToID("points");
		private static readonly int numPointsPerAxisID = Shader.PropertyToID("numPointsPerAxis");
		private static readonly int boundsSizeID = Shader.PropertyToID("boundsSize");
		private static readonly int centerID = Shader.PropertyToID("center");
		private static readonly int offsetID = Shader.PropertyToID("offset");
		private static readonly int spacingID = Shader.PropertyToID("spacing");
		private static readonly int radiusID = Shader.PropertyToID("radius");

		private void Awake() {
			CreateBuffers();
			Generate();
		}

		private void CreateBuffers() {
			int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;

			// Always create buffers in editor (since buffers are released immediately to prevent memory leak)
			// Otherwise, only create if null or if size has changed
			if (!Application.isPlaying || (pointsBuffer == null || numPoints != pointsBuffer.count)) {
				if (Application.isPlaying) {
					ReleaseBuffers();
				}

				pointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
			}
		}

		void ReleaseBuffers() {
			pointsBuffer?.Release();
		}

		void OnDestroy() {
			if (Application.isPlaying) {
				ReleaseBuffers();
			}
		}

		private void Generate() {
			var numThreadsPerAxis = Mathf.CeilToInt(numPointsPerAxis / (float) threadGroupSize);
			float pointSpacing = boundSize / (numPointsPerAxis - 1);

			computeShader.SetBuffer(0, pointsID, pointsBuffer);
			computeShader.SetInt(numPointsPerAxisID, numPointsPerAxis);
			computeShader.SetFloat(boundsSizeID, boundSize);
			computeShader.SetVector(centerID, center);
			computeShader.SetVector(offsetID, offset);
			computeShader.SetFloat(spacingID, pointSpacing);
			computeShader.SetFloat(radiusID, radius);

			// Dispatch shader
			computeShader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);
		}

		private void OnDrawGizmos() {
			if (pointsBuffer == null) return;
			
			var data = new float[numPointsPerAxis * numPointsPerAxis * numPointsPerAxis * 4];
			pointsBuffer.GetData(data);

			for (var i = 0; i < data.Length; i += 4) {
				var pos = new Vector3(data[i], data[i + 1], data[i + 2]);
				var value = data[i + 3];
				Gizmos.color = new Color(value, value, value);
				Gizmos.DrawSphere(pos, 0.05f);
			}
		}
	}
}