using System;
using UnityEngine;

namespace SebLague.MarchingCubes {
	public class GenerateMesh : MonoBehaviour {
		[SerializeField] private RenderSphere sphereGenerator;
		[SerializeField] private ComputeShader triangleShader;

		[SerializeField] private int numPointsPerAxis;
		[SerializeField] private float radius;
		[SerializeField] private Vector3 boundSize = Vector3.one;

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

		const int threadGroupSize = 8;

		private static readonly int pointsID = Shader.PropertyToID("points");
		private static readonly int trianglesID = Shader.PropertyToID("triangles");
		private static readonly int numPointsPerAxisID = Shader.PropertyToID("numPointsPerAxis");

		private void Awake() {
			CreateBuffers();
			GeneratePoints();
			GenerateTriangles();
			GenerateMarchingMesh();
		}

		private void CreateBuffers() {
			var numberOfCubesPerAxis = numPointsPerAxis - 1;
			var numVoxels = numberOfCubesPerAxis * numberOfCubesPerAxis * numberOfCubesPerAxis;
			var maxTriangleCount = numVoxels * 5;

			trianglesBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);
			triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
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
			pointsBuffer = sphereGenerator.Generate(numPointsPerAxis, radius);
		}

		private void GenerateTriangles() {
			var numberOfCubesPerAxis = numPointsPerAxis - 1;
			var numThreadsPerAxis = Mathf.CeilToInt(numberOfCubesPerAxis / (float)threadGroupSize);

			trianglesBuffer.SetCounterValue(0);
			triangleShader.SetTexture(0, pointsID, pointsBuffer);
			triangleShader.SetBuffer(0, trianglesID, trianglesBuffer);
			triangleShader.SetInt(numPointsPerAxisID, numPointsPerAxis);

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

			mesh = new Mesh();
			var vertices = new Vector3[numTris * 3];
			var meshTriangles = new int[numTris * 3];

			for (var i = 0; i < numTris; i++) {
				for (var j = 0; j < 3; j++) {
					meshTriangles[i * 3 + j] = i * 3 + j;
					vertices[i * 3 + j] = Vector3.Scale(tris[i][j] / numPointsPerAxis, boundSize);
				}
			}

			mesh.vertices = vertices;
			mesh.triangles = meshTriangles;

			mesh.RecalculateNormals();
			GetComponent<MeshFilter>().mesh = mesh;
			
			AddCollider();
		}

		private void AddCollider() {
			var meshCollider = gameObject.AddComponent<MeshCollider>();
			meshCollider.sharedMesh = mesh;
		}
	}
}