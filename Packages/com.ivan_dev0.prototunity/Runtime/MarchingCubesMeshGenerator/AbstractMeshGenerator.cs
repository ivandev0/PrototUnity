using PrototUnity.PointsGenerators;
using PrototUnity.Utils;
using UnityEngine;

namespace PrototUnity.MarchingCubesMeshGenerator {
	public abstract class AbstractMeshGenerator : MonoBehaviour {
		[SerializeField] protected AbstractTextureGenerator textureGenerator;
		
		[SerializeField] protected Vector3 boundSize = Vector3.one;
		
		// TODO set up "points" buffer
		[SerializeField] private ComputeShader editShader;
		
		protected int NumPointsPerAxis => textureGenerator.NumPointsPerAxis;
		
		private static readonly int textureSizeID = Shader.PropertyToID("textureSize");
		private static readonly int brushCenterID = Shader.PropertyToID("brushCenter");
		private static readonly int brushRadiusID = Shader.PropertyToID("brushRadius");
		private static readonly int deltaTimeID = Shader.PropertyToID("deltaTime");
		private static readonly int weightID = Shader.PropertyToID("weight");
		
		public abstract void GeneratePoints();
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
		
		private static Vector3Int GetTexturePosition(Vector3 worldPosition, int textureSize, float cubeSize) {
			var textureNormalizedCoordinates = ((worldPosition + Vector3.one * (cubeSize * 0.5f)) / cubeSize).Clamp01();
			return (textureNormalizedCoordinates * (textureSize - 1)).RoundToInt();
		}
	}
}