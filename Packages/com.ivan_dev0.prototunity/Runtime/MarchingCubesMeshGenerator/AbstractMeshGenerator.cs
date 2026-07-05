using System;
using PrototUnity.PointsGenerators;
using PrototUnity.Utils;
using UnityEngine;

namespace PrototUnity.MarchingCubesMeshGenerator {
	public abstract class AbstractMeshGenerator : MonoBehaviour {
		[SerializeField] protected AbstractTextureGenerator textureGenerator;
		
		[SerializeField] protected Vector3 boundSize = Vector3.one;
		
		[SerializeField] private ComputeShader editShader;
		
		protected struct Triangle {
			private Vector3 vertexC;
			private Vector3 vertexB;
			private Vector3 vertexA;

			public Vector3 this[int i]
			{
				get
				{
					return i switch {
						0 => vertexA,
						1 => vertexB,
						2 => vertexC,
						_ => throw new ArgumentOutOfRangeException($"{i}")
					};
				}
			}
		};
		
		protected RenderTexture pointsBuffer;
		
		protected int NumPointsPerAxis => textureGenerator.NumPointsPerAxis;
		
		protected static readonly int pointsID = Shader.PropertyToID("points");
		private static readonly int textureSizeID = Shader.PropertyToID("textureSize");
		private static readonly int brushCenterID = Shader.PropertyToID("brushCenter");
		private static readonly int brushRadiusID = Shader.PropertyToID("brushRadius");
		private static readonly int deltaTimeID = Shader.PropertyToID("deltaTime");
		private static readonly int weightID = Shader.PropertyToID("weight");

		protected virtual void Awake() {
			GeneratePoints();
		}

		public virtual void GeneratePoints() {
			pointsBuffer = textureGenerator.GenerateTexture();
			
			editShader.SetTexture(0, pointsID, pointsBuffer);
		}
		
		public abstract void GenerateMesh();
		public abstract void Render();

		public void Terraform(Vector3 point, float terraformWeight, float terraformRadius) {
			var boundsSize = boundSize.x;
			var textureSize = NumPointsPerAxis;
			var worldSizeInOnePixel = boundsSize / textureSize;
			var terraformPixelRadius = Mathf.CeilToInt(terraformRadius / worldSizeInOnePixel);
			var texturePosition = GetTexturePosition(point, textureSize, boundsSize);
			editShader.SetInt(textureSizeID, textureSize);
			editShader.SetInts(brushCenterID, texturePosition.x, texturePosition.y, texturePosition.z);
			editShader.SetInt(brushRadiusID, terraformPixelRadius);
			editShader.SetFloat(deltaTimeID, Time.deltaTime);
			editShader.SetFloat(weightID, terraformWeight);
			
			ComputeHelper.Dispatch(editShader, textureSize, textureSize, textureSize);
		
			GenerateMesh();
		}
		
		protected virtual void ReleaseBuffers() {
			pointsBuffer.Release();
		}
		
		private static Vector3Int GetTexturePosition(Vector3 worldPosition, int textureSize, float cubeSize) {
			var textureNormalizedCoordinates = ((worldPosition + Vector3.one * (cubeSize * 0.5f)) / cubeSize).Clamp01();
			return (textureNormalizedCoordinates * (textureSize - 1)).RoundToInt();
		}
		
		void OnDestroy() {
			if (Application.isPlaying) {
				ReleaseBuffers();
			}
		}
	}
}