using System.Runtime.InteropServices;
using PrototUnity.Utils;
using PrototUnity.VoronoiGenerator;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototUnity.MarchingCubesMeshGenerator.VoronoiVoxel {
	[RequireComponent(typeof(MeshCollider))]
	public class VoronoiMeshGenerator : AbstractMeshGenerator {
		[SerializeField] private ComputeShader voxelShader;
		
		[SerializeField] private ComputeShader voronoiShader;
		[SerializeField] private Material voronoiMaterial;
		
		private int NumVoxels => (NumPointsPerAxis - 1) * (NumPointsPerAxis - 1) * (NumPointsPerAxis - 1);
		
		struct VoronoiCell
		{
			public uint vertexCount;
			public uint vertexStart;
		};
		
		private struct Voxel {
			private uint id;
			private Vector3 position;
		}

		private ComputeBuffer voxelCountBuffer;
		private ComputeBuffer voxelsBuffer;
		private Texture3D colorTexture;
		private ComputeBuffer voronoiCellsBuffer;
		private ComputeBuffer voronoiVerticesBuffer;

		private MeshCollider meshCollider;
		private Mesh voronoiMesh;

		private static readonly int voxelsID = Shader.PropertyToID("voxels");
		private static readonly int numPointsPerAxisID = Shader.PropertyToID("numPointsPerAxis");
		private static readonly int colorsID = Shader.PropertyToID("colors");
		private static readonly int colorSizeID = Shader.PropertyToID("colorSize");
		private static readonly int centerGridSizeID = Shader.PropertyToID("_CenterGridSize");
		private static readonly int worldOriginID = Shader.PropertyToID("_WorldOrigin");
		private static readonly int boxSizeID = Shader.PropertyToID("_BoxSize");
		private static readonly int centersID = Shader.PropertyToID("centers");
		private static readonly int cellsID = Shader.PropertyToID("cells");
		private static readonly int verticesID = Shader.PropertyToID("vertices");
		private static readonly int idsToRenderID = Shader.PropertyToID("idsToRender");

		public override void BeforePointGeneration() {
			meshCollider = GetComponent<MeshCollider>();
			CreateBuffers();
		}

		private void CreateBuffers() {
			voxelCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
			voxelsBuffer = new ComputeBuffer(NumVoxels, Marshal.SizeOf(typeof(Voxel)), ComputeBufferType.Append);
			
			voronoiCellsBuffer = new ComputeBuffer(NumVoxels, Marshal.SizeOf<VoronoiCell>(), ComputeBufferType.Structured);
			voronoiVerticesBuffer = new ComputeBuffer(NumVoxels * 255, Marshal.SizeOf<Vector3>(), ComputeBufferType.Structured);
			
			voronoiMaterial.enableInstancing = true;
			voronoiMaterial.SetBuffer(idsToRenderID, voxelsBuffer);
			colorTexture = VoronoiGenerator.Utils.CreateRandomColors(0, NumPointsPerAxis, NumPointsPerAxis, NumPointsPerAxis);
			voronoiMaterial.SetTexture(colorsID, colorTexture);
			voronoiMaterial.SetVector(colorSizeID, new Vector4(NumPointsPerAxis, NumPointsPerAxis, NumPointsPerAxis, 0));
			voronoiMaterial.SetBuffer(cellsID, voronoiCellsBuffer);
			voronoiMaterial.SetBuffer(verticesID, voronoiVerticesBuffer);
			
			CreateVoxelEmptyMesh();
		}

		private void CreateVoxelEmptyMesh() {
			voronoiMesh = new Mesh(); 
			var verticesCount = 255;
			var vertices = new Vector3[verticesCount];
			var triangles = new int[verticesCount];
			for (var i = 0; i < verticesCount; i++) {
				vertices[i] = Vector3.zero;
				triangles[i] = i;
			}
			voronoiMesh.SetVertices(vertices);
			voronoiMesh.SetTriangles(triangles, 0);
		}

		public override void AfterPointGeneration() {
			InitTextures();
			DispatchVoronoi(Vector3Int.one * (NumPointsPerAxis - 1));
		}

		private void InitTextures() {
			// TODO this is slightly incorrect. We want to filter voxels that are "between" points, not "on" them.
			//  This results in visually incorrect mesh generation.
			voxelShader.SetTexture(0, pointsID, pointsBuffer);
		}
		
		// TODO use voronoi generator instead
		private void DispatchVoronoi(Vector3Int size) {
			var seedTexture = VoronoiComputeGenerator.GenerateSeedTexture(size, boundSize);
			var kernel = voronoiShader.FindKernel("BuildVoronoiCells3D");

			voronoiShader.SetInts(nameID: centerGridSizeID, size.x, size.y, size.z);
			voronoiShader.SetFloats(worldOriginID, 0, 0, 0);
			voronoiShader.SetFloats(boxSizeID, boundSize.x, boundSize.y, boundSize.z);
			voronoiShader.SetTexture(kernel, centersID, seedTexture);
			voronoiShader.SetBuffer(kernel, cellsID, voronoiCellsBuffer);
			voronoiShader.SetBuffer(kernel, verticesID, voronoiVerticesBuffer);

			ComputeHelper.Dispatch(voronoiShader, size.x, size.y, size.z);
		}

		public override void AfterMeshGeneration() {
			meshCollider.sharedMesh = mesh; 
			
			GenerateVoxels();
		}

		private void GenerateVoxels() {
			var numVoxels = NumPointsPerAxis - 1;
			voxelsBuffer.SetCounterValue(0);
			voxelShader.SetBuffer(0, voxelsID, voxelsBuffer);
			voxelShader.SetInt(numPointsPerAxisID, numVoxels);
			ComputeHelper.Dispatch(voxelShader, numVoxels, numVoxels, numVoxels);
		}

		private void Update() {
			Render();
		}

		public override void Render() {
			ComputeBuffer.CopyCount(voxelsBuffer, voxelCountBuffer, 0);
			int[] voxelCountArray = { 0 };
			voxelCountBuffer.GetData(voxelCountArray);
			var voxelsAmount = voxelCountArray[0];
			
			var rp = new RenderParams(voronoiMaterial) {
				worldBounds = new Bounds(Vector3.zero, boundSize * 1.1f),
				shadowCastingMode = ShadowCastingMode.On,
				receiveShadows = true,
				matProps = new MaterialPropertyBlock()
			};
			var commandCount = 1;
			var commandBuf = new GraphicsBuffer(
				GraphicsBuffer.Target.IndirectArguments,
				commandCount,
				GraphicsBuffer.IndirectDrawIndexedArgs.size
			);
			var commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[commandCount];
			commandData[0].indexCountPerInstance = voronoiMesh.GetIndexCount(0);
			commandData[0].instanceCount = (uint) voxelsAmount;
			commandBuf.SetData(commandData);
			Graphics.RenderMeshIndirect(rp, voronoiMesh, commandBuf, commandCount);

		}

		protected override void ReleaseBuffers() {
			base.ReleaseBuffers();
			voxelCountBuffer?.Release();
			voxelsBuffer?.Release();
			voronoiCellsBuffer?.Release();
			voronoiVerticesBuffer?.Release();
		}
	}
}