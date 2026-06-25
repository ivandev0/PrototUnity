using System;
using System.Runtime.InteropServices;
using PrototUnity.Utils;
using SebLague.MarchingCubes.PointsGenerators;
using UnityEngine;
using UnityEngine.Rendering;

namespace SebLague.MarchingCubes {
	public class GenerateMesh : MonoBehaviour {
		[SerializeField] private AbstractTextureGenerator textureGenerator;
		[SerializeField] private ComputeShader triangleShader;
		[SerializeField] private ComputeShader editShader;
		[SerializeField] private ComputeShader voxelShader;

		[SerializeField] private Vector3 boundSize = Vector3.one;
		[SerializeField] private Material voxelMaterial;
		[SerializeField] private Mesh voxelMesh;
		
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
		
		private struct Voxel {
			private Vector3 position;
		}

		private RenderTexture pointsBuffer;
		private ComputeBuffer trianglesBuffer;
		private ComputeBuffer triCountBuffer;
		private ComputeBuffer voxelsBuffer;

		private Mesh mesh;
		private MeshFilter meshFilter;
		private MeshCollider meshCollider;

		private static readonly int pointsID = Shader.PropertyToID("points");
		private static readonly int trianglesID = Shader.PropertyToID("triangles");
		private static readonly int voxelsID = Shader.PropertyToID("voxels");
		private static readonly int numPointsPerAxisID = Shader.PropertyToID("numPointsPerAxis");
		private static readonly int textureSizeID = Shader.PropertyToID("textureSize");
		private static readonly int brushCenterID = Shader.PropertyToID("brushCenter");
		private static readonly int brushRadiusID = Shader.PropertyToID("brushRadius");
		private static readonly int deltaTimeID = Shader.PropertyToID("deltaTime");
		private static readonly int weightID = Shader.PropertyToID("weight");
		private static readonly int objectToWorldID = Shader.PropertyToID("_ObjectToWorld");

		private void Awake() {
			meshFilter = GetComponent<MeshFilter>();
			meshCollider = GetComponent<MeshCollider>();
			
			CreateBuffers();
			GeneratePoints();
			GenerateTriangles();
			GenerateMarchingMesh();
			GenerateVoxels();
		}

		private void CreateBuffers() {
			var numberOfCubesPerAxis = NumPointsPerAxis - 1;
			var numVoxels = numberOfCubesPerAxis * numberOfCubesPerAxis * numberOfCubesPerAxis;
			var maxTriangleCount = numVoxels * 5;

			trianglesBuffer = new ComputeBuffer(maxTriangleCount, Marshal.SizeOf(typeof(Triangle)), ComputeBufferType.Append);
			triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
			voxelsBuffer = new ComputeBuffer(numVoxels, Marshal.SizeOf(typeof(Voxel)), ComputeBufferType.Append);
			
			voxelMaterial.SetBuffer(voxelsID, voxelsBuffer);
		}

		private void InitTextures() {
			triangleShader.SetTexture(0, pointsID, pointsBuffer);
			editShader.SetTexture(0, pointsID, pointsBuffer);
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
			SetUpVoxelRendering();
		}

		private void GeneratePoints() {
			pointsBuffer = textureGenerator.GenerateTexture();
			InitTextures();
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
			// meshFilter.mesh = mesh;
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
			
			ComputeHelper.Dispatch(editShader, textureSize, textureSize, textureSize);

			GenerateTriangles();
			GenerateMarchingMesh();
			GenerateVoxels();
		}

		private static Vector3Int GetTexturePosition(Vector3 worldPosition, int textureSize, float cubeSize) {
			var textureNormalizedCoordinates = ((worldPosition + Vector3.one * (cubeSize * 0.5f)) / cubeSize).Clamp01();
			return (textureNormalizedCoordinates * (textureSize - 1)).RoundToInt();
		}

		private void GenerateVoxels() {
			voxelsBuffer.SetCounterValue(0);
			voxelShader.SetBuffer(0, voxelsID, voxelsBuffer);
			voxelShader.SetInt(numPointsPerAxisID, NumPointsPerAxis);
			ComputeHelper.Dispatch(voxelShader, NumPointsPerAxis, NumPointsPerAxis, NumPointsPerAxis);
		}

		private void SetUpVoxelRendering() {
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