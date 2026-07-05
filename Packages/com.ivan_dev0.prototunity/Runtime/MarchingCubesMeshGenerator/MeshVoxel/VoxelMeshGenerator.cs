using System;
using System.Runtime.InteropServices;
using PrototUnity.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototUnity.MarchingCubesMeshGenerator.MeshVoxel {
	[RequireComponent(typeof(MeshCollider))]
	public class VoxelMeshGenerator : AbstractMeshGenerator {
		[SerializeField] private ComputeShader triangleShader;
		[SerializeField] private ComputeShader voxelShader;

		[SerializeField] private Material voxelMaterial;
		[SerializeField] private Mesh voxelMesh;

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
		
		private struct Voxel {
			private Vector3 position;
		}

		private RenderTexture pointsBuffer;
		private ComputeBuffer trianglesBuffer;
		private ComputeBuffer triCountBuffer;
		private ComputeBuffer voxelsBuffer;
		private Texture3D colorTexture;

		private Mesh mesh;
		private MeshCollider meshCollider;

		private static readonly int pointsID = Shader.PropertyToID("points");
		private static readonly int trianglesID = Shader.PropertyToID("triangles");
		private static readonly int voxelsID = Shader.PropertyToID("voxels");
		private static readonly int numPointsPerAxisID = Shader.PropertyToID("numPointsPerAxis");
		private static readonly int objectToWorldID = Shader.PropertyToID("_ObjectToWorld");
		private static readonly int colorsID = Shader.PropertyToID("colors");
		private static readonly int colorSizeID = Shader.PropertyToID("colorSize");

		private void Awake() {
			meshCollider = GetComponent<MeshCollider>();
			
			CreateBuffers();
			GeneratePoints();
			GenerateMesh();
		}

		private void CreateBuffers() {
			var numberOfCubesPerAxis = NumPointsPerAxis - 1;
			var numVoxels = numberOfCubesPerAxis * numberOfCubesPerAxis * numberOfCubesPerAxis;
			var maxTriangleCount = numVoxels * 5;

			trianglesBuffer = new ComputeBuffer(maxTriangleCount, Marshal.SizeOf(typeof(Triangle)), ComputeBufferType.Append);
			triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
			voxelsBuffer = new ComputeBuffer(numVoxels, Marshal.SizeOf(typeof(Voxel)), ComputeBufferType.Append);

			voxelMaterial.enableInstancing = true;
			voxelMaterial.SetBuffer(voxelsID, voxelsBuffer);
			colorTexture = VoronoiGenerator.Utils.CreateRandomColors(0, NumPointsPerAxis, NumPointsPerAxis, NumPointsPerAxis);
			voxelMaterial.SetTexture(colorsID, colorTexture);
			voxelMaterial.SetVector(colorSizeID, new Vector4(colorTexture.width, colorTexture.height, colorTexture.depth, 0));
		}

		private void InitTextures() {
			triangleShader.SetTexture(0, pointsID, pointsBuffer);
			// editShader.SetTexture(0, pointsID, pointsBuffer);
			voxelShader.SetTexture(0, pointsID, pointsBuffer);
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

		private void Update() {
			Render();
		}

		public override void GeneratePoints() {
			pointsBuffer = textureGenerator.GenerateTexture();
			InitTextures();
		}

		public override void GenerateMesh() {
			GenerateTriangles();
			GenerateMarchingMesh();
			GenerateVoxels();
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
			meshCollider.sharedMesh = mesh; 
		}

		private void GenerateVoxels() {
			voxelsBuffer.SetCounterValue(0);
			voxelShader.SetBuffer(0, voxelsID, voxelsBuffer);
			voxelShader.SetInt(numPointsPerAxisID, NumPointsPerAxis);
			ComputeHelper.Dispatch(voxelShader, NumPointsPerAxis, NumPointsPerAxis, NumPointsPerAxis);
		}

		public override void Render() {
			ComputeBuffer.CopyCount(voxelsBuffer, triCountBuffer, 0);
			int[] triCountArray = { 0 };
			triCountBuffer.GetData(triCountArray);
			var voxelsAmount = triCountArray[0];
			
			var rp = new RenderParams(voxelMaterial) {
				worldBounds = new Bounds(Vector3.zero, boundSize * 1.1f),
				shadowCastingMode = ShadowCastingMode.On,
				receiveShadows = true,
				matProps = new MaterialPropertyBlock()
			};
			rp.matProps.SetMatrix(objectToWorldID, Matrix4x4.Scale(boundSize / NumPointsPerAxis));
			var commandCount = 1;
			var commandBuf = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, commandCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
			var commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[commandCount];
			commandData[0].indexCountPerInstance = voxelMesh.GetIndexCount(0);
			commandData[0].instanceCount = (uint) voxelsAmount;
			commandBuf.SetData(commandData);
			Graphics.RenderMeshIndirect(rp, voxelMesh, commandBuf, commandCount);
		}
	}
}