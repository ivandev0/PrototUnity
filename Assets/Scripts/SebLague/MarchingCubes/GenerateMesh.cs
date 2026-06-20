using System;
using UnityEngine;

namespace SebLague.MarchingCubes {
	public class GenerateMesh : MonoBehaviour {
		[SerializeField] private ComputeShader pointShader;
		[SerializeField] private ComputeShader triangleShader;

		[SerializeField] private int numPointsPerAxis;
		[SerializeField] private float boundSize;
		[SerializeField] private Vector3 center;
		[SerializeField] private Vector3 offset;
		[SerializeField] private float radius;

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

		private ComputeBuffer pointsBuffer;
		private ComputeBuffer trianglesBuffer;
		private ComputeBuffer triCountBuffer;

		private Mesh mesh;

		const int threadGroupSize = 8;

		private static readonly int pointsID = Shader.PropertyToID("points");
		private static readonly int trianglesID = Shader.PropertyToID("triangles");
		private static readonly int numPointsPerAxisID = Shader.PropertyToID("numPointsPerAxis");
		private static readonly int boundsSizeID = Shader.PropertyToID("boundsSize");
		private static readonly int centerID = Shader.PropertyToID("center");
		private static readonly int offsetID = Shader.PropertyToID("offset");
		private static readonly int spacingID = Shader.PropertyToID("spacing");
		private static readonly int radiusID = Shader.PropertyToID("radius");

		private void Awake() {
			CreateBuffers();
			GeneratePoints();
			GenerateTriangles();
			GenerateMarchingMesh();
		}

		private void CreateBuffers() {
			var numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
			var numberOfCubesPerAxis = numPointsPerAxis - 1;
			var numVoxels = numberOfCubesPerAxis * numberOfCubesPerAxis * numberOfCubesPerAxis;
			var maxTriangleCount = numVoxels * 5;

			// Always create buffers in editor (since buffers are released immediately to prevent memory leak)
			// Otherwise, only create if null or if size has changed
			if (!Application.isPlaying || (pointsBuffer == null || numPoints != pointsBuffer.count)) {
				if (Application.isPlaying) {
					ReleaseBuffers();
				}

				trianglesBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);
				pointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
				triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
			}
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
			var numThreadsPerAxis = Mathf.CeilToInt(numPointsPerAxis / (float)threadGroupSize);
			var numberOfCubesPerAxis = numPointsPerAxis - 1;
			var pointSpacing = boundSize / numberOfCubesPerAxis;

			pointShader.SetBuffer(0, pointsID, pointsBuffer);
			pointShader.SetInt(numPointsPerAxisID, numPointsPerAxis);
			pointShader.SetFloat(boundsSizeID, boundSize);
			pointShader.SetVector(centerID, center);
			pointShader.SetVector(offsetID, offset);
			pointShader.SetFloat(spacingID, pointSpacing);
			pointShader.SetFloat(radiusID, radius);

			pointShader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);
		}

		private void GenerateTriangles() {
			var numberOfCubesPerAxis = numPointsPerAxis - 1;
			var numThreadsPerAxis = Mathf.CeilToInt(numberOfCubesPerAxis / (float)threadGroupSize);

			trianglesBuffer.SetCounterValue(0);
			triangleShader.SetBuffer(0, pointsID, pointsBuffer);
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
					vertices[i * 3 + j] = tris[i][j];
				}
			}

			mesh.vertices = vertices;
			mesh.triangles = meshTriangles;

			mesh.RecalculateNormals();
			GetComponent<MeshFilter>().mesh = mesh;
		}
	}
}