using System;
using System.Runtime.InteropServices;
using PrototUnity.PointsGenerators;
using PrototUnity.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototUnity.MarchingCubesMeshGenerator {
	public abstract class MeshGeneratorBase : MonoBehaviour {
		public void Generate() {
			BeforePointGeneration();
			GeneratePoints();
			AfterPointGeneration();
			
			BeforeMeshGeneration();
			GenerateMesh();
			AfterMeshGeneration();
		}
		
		protected virtual void BeforePointGeneration() {}
		protected abstract void GeneratePoints();
		protected virtual void AfterPointGeneration() {}
		
		protected virtual void BeforeMeshGeneration() {}
		protected abstract void GenerateMesh();
		protected virtual void AfterMeshGeneration() {}

		public abstract void Terraform(Vector3 point, float terraformWeight, float terraformRadius);
	}
	
	public abstract class AbstractMeshGenerator : MeshGeneratorBase {
		[SerializeField] protected AbstractTextureGenerator textureGenerator;
		
		[SerializeField] private Vector3 boundSize = Vector3.one;
		public Vector3 BoundSize => boundSize;
		
		[SerializeField] private ComputeShader editShader;
		[SerializeField] private ComputeShader triangleShader;

		private struct Triangle {
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
		private ComputeBuffer trianglesBuffer;
		private ComputeBuffer triCountBuffer;

		protected Mesh mesh;
		
		protected int NumPointsPerAxis => textureGenerator.NumPointsPerAxis;
		
		protected static readonly int pointsID = Shader.PropertyToID("points");
		private static readonly int textureSizeID = Shader.PropertyToID("textureSize");
		private static readonly int brushCenterID = Shader.PropertyToID("brushCenter");
		private static readonly int brushRadiusID = Shader.PropertyToID("brushRadius");
		private static readonly int deltaTimeID = Shader.PropertyToID("deltaTime");
		private static readonly int weightID = Shader.PropertyToID("weight");
		private static readonly int trianglesID = Shader.PropertyToID("triangles");
		private static readonly int numPointsPerAxisID = Shader.PropertyToID("numPointsPerAxis");
		
		protected virtual void Awake() {
			Generate();
		}

		protected sealed override void GeneratePoints() {
			pointsBuffer = textureGenerator.GenerateTexture();
			
			InitTextures();
			CreateBuffers();
		}

		private void InitTextures() {
			triangleShader.SetTexture(0, pointsID, pointsBuffer);
			editShader.SetTexture(0, pointsID, pointsBuffer);
		}

		private void CreateBuffers() {
			var numberOfCubesPerAxis = NumPointsPerAxis - 1;
			var numVoxels = numberOfCubesPerAxis * numberOfCubesPerAxis * numberOfCubesPerAxis;
			var maxTriangleCount = numVoxels * 5;

			trianglesBuffer = new ComputeBuffer(maxTriangleCount, Marshal.SizeOf(typeof(Triangle)), ComputeBufferType.Append);
			triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
		}

		protected sealed override void GenerateMesh() {
			GenerateTriangles();
			GenerateMarchingMesh();
		}
		
		private void GenerateTriangles() {
			var numberOfCubesPerAxis = NumPointsPerAxis - 1;

			trianglesBuffer.SetCounterValue(0);
			triangleShader.SetBuffer(0, trianglesID, trianglesBuffer);
			triangleShader.SetInt(numPointsPerAxisID, NumPointsPerAxis);

			ComputeHelper.Dispatch(triangleShader, numberOfCubesPerAxis, numberOfCubesPerAxis, numberOfCubesPerAxis);
		}

		private void GenerateMarchingMesh() {
			// Get number of triangles in the triangle buffer
			ComputeBuffer.CopyCount(trianglesBuffer, triCountBuffer, 0);
			int[] triCountArray = { 0 };
			triCountBuffer.GetData(triCountArray);
			var numTris = triCountArray[0];

			// Get triangle data from shader
			var tris = new Triangle[numTris];
			trianglesBuffer.GetData(tris, 0, 0, numTris);

			mesh = new Mesh {
				indexFormat = IndexFormat.UInt32
			};
			var vertices = new Vector3[numTris * 3];
			var meshTriangles = new int[numTris * 3];

			for (var i = 0; i < numTris; i++) {
				for (var j = 0; j < 3; j++) {
					meshTriangles[i * 3 + j] = i * 3 + j;
					vertices[i * 3 + j] = Vector3.Scale(tris[i][j] / NumPointsPerAxis, BoundSize);
				}
			}

			mesh.vertices = vertices;
			mesh.triangles = meshTriangles;

			mesh.RecalculateNormals();
		}

		public abstract void Render();

		public sealed override void Terraform(Vector3 point, float terraformWeight, float terraformRadius) {
			var boundsSize = BoundSize.x;
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
		
			BeforeMeshGeneration();
			GenerateMesh();
			AfterMeshGeneration();
		}
		
		protected virtual void ReleaseBuffers() {
			pointsBuffer.Release();
			triCountBuffer.Release();
			trianglesBuffer.Release();
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

		private void OnDisable() {
			ReleaseBuffers();
		}
	}
}