using System.Runtime.InteropServices;
using PrototUnity.Utils;
using PrototUnity.VoronoiGenerator;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototUnity.MarchingCubesMeshGenerator.VoronoiVoxel {
	[RequireComponent(typeof(MeshCollider))]
	public class VoronoiMeshGenerator : AbstractMeshGenerator {
		[SerializeField] private AbstractVoronoiComputeGenerator voronoiGenerator;
		[SerializeField] private ComputeShader voxelShader;
		
		[SerializeField] private ComputeShader voronoiShader;
		[SerializeField] private Material voronoiMaterial;
		
		private int NumVoxels => (NumPointsPerAxis - 1) * (NumPointsPerAxis - 1) * (NumPointsPerAxis - 1);
		
		private struct Voxel {
			private uint id;
			private Vector3 position;
		}

		private ComputeBuffer voxelCountBuffer;
		private ComputeBuffer voxelsBuffer;
		private Texture3D colorTexture;

		private MeshCollider meshCollider;
		private Mesh voronoiMesh;

		private static readonly int voxelsID = Shader.PropertyToID("voxels");
		private static readonly int numPointsPerAxisID = Shader.PropertyToID("numPointsPerAxis");
		private static readonly int colorsID = Shader.PropertyToID("colors");
		private static readonly int colorSizeID = Shader.PropertyToID("colorSize");
		private static readonly int cellsID = Shader.PropertyToID("cells");
		private static readonly int verticesID = Shader.PropertyToID("vertices");
		private static readonly int idsToRenderID = Shader.PropertyToID("idsToRender");

		public override void BeforePointGeneration() {
			meshCollider = GetComponent<MeshCollider>();
			voronoiGenerator.Generate();
			CreateBuffers();
		}

		private void CreateBuffers() {
			voxelCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
			voxelsBuffer = new ComputeBuffer(NumVoxels, Marshal.SizeOf(typeof(Voxel)), ComputeBufferType.Append);
			
			voronoiMaterial.enableInstancing = true;
			voronoiMaterial.SetBuffer(idsToRenderID, voxelsBuffer);
			colorTexture = VoronoiGenerator.Utils.CreateRandomColors(0, NumPointsPerAxis, NumPointsPerAxis, NumPointsPerAxis);
			voronoiMaterial.SetTexture(colorsID, colorTexture);
			voronoiMaterial.SetVector(colorSizeID, new Vector4(NumPointsPerAxis, NumPointsPerAxis, NumPointsPerAxis, 0));
			voronoiMaterial.SetBuffer(cellsID, voronoiGenerator.VoronoiCellsBuffer);
			voronoiMaterial.SetBuffer(verticesID, voronoiGenerator.VoronoiVerticesBuffer);
			
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
		}

		private void InitTextures() {
			// TODO this is slightly incorrect. We want to filter voxels that are "between" points, not "on" them.
			//  This results in visually incorrect mesh generation.
			voxelShader.SetTexture(0, pointsID, pointsBuffer);
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
		}
	}
}