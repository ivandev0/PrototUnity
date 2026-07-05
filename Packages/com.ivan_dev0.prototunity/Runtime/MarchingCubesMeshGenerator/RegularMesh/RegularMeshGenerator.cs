using System.Runtime.InteropServices;
using PrototUnity.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototUnity.MarchingCubesMeshGenerator.RegularMesh { 
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
	public class RegularMeshGenerator : AbstractMeshGenerator {
		[SerializeField] private ComputeShader triangleShader;

		private ComputeBuffer trianglesBuffer;
		private ComputeBuffer triCountBuffer;

		private Mesh mesh;
		private MeshFilter meshFilter;
		private MeshCollider meshCollider;

		private static readonly int trianglesID = Shader.PropertyToID("triangles");
		private static readonly int numPointsPerAxisID = Shader.PropertyToID("numPointsPerAxis");

		protected override void Awake() {
			base.Awake();
			meshFilter = GetComponent<MeshFilter>();
			meshCollider = GetComponent<MeshCollider>();
			
			CreateBuffers();
			GenerateTriangles();
			GenerateMarchingMesh();
		}

		private void CreateBuffers() {
			var numberOfCubesPerAxis = NumPointsPerAxis - 1;
			var numVoxels = numberOfCubesPerAxis * numberOfCubesPerAxis * numberOfCubesPerAxis;
			var maxTriangleCount = numVoxels * 5;

			trianglesBuffer = new ComputeBuffer(maxTriangleCount, Marshal.SizeOf(typeof(Triangle)), ComputeBufferType.Append);
			triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
		}

		protected override void ReleaseBuffers() {
			trianglesBuffer?.Release();
			triCountBuffer?.Release();
		}

		public override void GeneratePoints() {
			base.GeneratePoints();
			InitTextures();
		}

		private void InitTextures() {
			triangleShader.SetTexture(0, pointsID, pointsBuffer);
		}

		public override void GenerateMesh() {
			GenerateTriangles();
			GenerateMarchingMesh();
		}

		public override void Render() {
			// nothing
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
					vertices[i * 3 + j] = Vector3.Scale(tris[i][j] / NumPointsPerAxis, boundSize);
				}
			}

			mesh.vertices = vertices;
			mesh.triangles = meshTriangles;

			mesh.RecalculateNormals();
			meshFilter.mesh = mesh;
			meshCollider.sharedMesh = mesh; 
		}
	}
}