using System;
using PrototUnity.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace SebLague.MarchingCubes {
	public class GenerateMesh : MonoBehaviour {
		[SerializeField] private AbstractTextureGenerator textureGenerator;
		[SerializeField] private ComputeShader triangleShader;
		[SerializeField] private ComputeShader editShader;

		[SerializeField] private Vector3 boundSize = Vector3.one;
		
		private int NumPointsPerAxis => textureGenerator.NumPointsPerAxis;

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

		private RenderTexture pointsBuffer;
		private ComputeBuffer trianglesBuffer;
		private ComputeBuffer triCountBuffer;

		private Mesh mesh;
		private MeshFilter meshFilter;
		private MeshCollider meshCollider;

		const int threadGroupSize = 8;

		private static readonly int pointsID = Shader.PropertyToID("points");
		private static readonly int trianglesID = Shader.PropertyToID("triangles");
		private static readonly int numPointsPerAxisID = Shader.PropertyToID("numPointsPerAxis");
		private static readonly int textureSizeID = Shader.PropertyToID("textureSize");
		private static readonly int brushCenterID = Shader.PropertyToID("brushCenter");
		private static readonly int brushRadiusID = Shader.PropertyToID("brushRadius");
		private static readonly int deltaTimeID = Shader.PropertyToID("deltaTime");
		private static readonly int weightID = Shader.PropertyToID("weight");

		private void Awake() {
			meshFilter = GetComponent<MeshFilter>();
			meshCollider = GetComponent<MeshCollider>();
			
			CreateBuffers();
			GeneratePoints();
			GenerateTriangles();
			GenerateMarchingMesh();
		}

		private void CreateBuffers() {
			var numberOfCubesPerAxis = NumPointsPerAxis - 1;
			var numVoxels = numberOfCubesPerAxis * numberOfCubesPerAxis * numberOfCubesPerAxis;
			var maxTriangleCount = numVoxels * 5;

			trianglesBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);
			triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
		}

		private void InitTextures() {
			triangleShader.SetTexture(0, pointsID, pointsBuffer);
			editShader.SetTexture(0, pointsID, pointsBuffer);
		}

		private void ReleaseBuffers() {
			pointsBuffer?.Release();
			trianglesBuffer?.Release();
			triCountBuffer?.Release();
		}

		void OnDestroy() {
			if (Application.isPlaying) {
				ReleaseBuffers();
			}
		}

		private void GeneratePoints() {
			pointsBuffer = textureGenerator.GenerateTexture();
			InitTextures();
		}

		private void GenerateTriangles() {
			var numberOfCubesPerAxis = NumPointsPerAxis - 1;
			var numThreadsPerAxis = Mathf.CeilToInt(numberOfCubesPerAxis / (float)threadGroupSize);

			trianglesBuffer.SetCounterValue(0);
			triangleShader.SetBuffer(0, trianglesID, trianglesBuffer);
			triangleShader.SetInt(numPointsPerAxisID, NumPointsPerAxis);

			triangleShader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);
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
					vertices[i * 3 + j] = Vector3.Scale(tris[i][j] / NumPointsPerAxis, boundSize);
				}
			}

			mesh.vertices = vertices;
			mesh.triangles = meshTriangles;

			mesh.RecalculateNormals();
			meshFilter.mesh = mesh;
			meshCollider.sharedMesh = mesh; 
		}

		public void Terraform(Vector3 point, float terraformWeight, float terraformRadius) {
			var boundsSize = boundSize.x;
			var textureSize = pointsBuffer.width;
			var worldSizeInOnePixel = boundsSize / textureSize;
			var terraformPixelRadius = Mathf.CeilToInt(terraformRadius / worldSizeInOnePixel);
			var texturePosition = GetTexturePosition(point, textureSize, boundsSize);
			editShader.SetInt(textureSizeID, textureSize);
			editShader.SetInts(brushCenterID, texturePosition.x, texturePosition.y, texturePosition.z);
			editShader.SetInt(brushRadiusID, terraformPixelRadius);
			editShader.SetFloat(deltaTimeID, Time.deltaTime);
			editShader.SetFloat(weightID, terraformWeight);
			
			var numThreadsPerAxis = Mathf.CeilToInt(textureSize / (float)threadGroupSize);
			editShader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);

			GenerateTriangles();
			GenerateMarchingMesh();
		}

		private static Vector3Int GetTexturePosition(Vector3 worldPosition, int textureSize, float cubeSize) {
			var textureNormalizedCoordinates = ((worldPosition + Vector3.one * (cubeSize * 0.5f)) / cubeSize).Clamp01();
			return (textureNormalizedCoordinates * (textureSize - 1)).RoundToInt();
		}
	}
}