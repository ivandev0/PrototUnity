using System.Runtime.InteropServices;
using PrototUnity.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototUnity.MarchingCubesMeshGenerator.MeshVoxel {
	[RequireComponent(typeof(MeshCollider))]
	public class VoxelMeshGenerator : AbstractMeshGenerator {
		[SerializeField] private ComputeShader voxelShader;
		[SerializeField] private Material voxelMaterial;
		[SerializeField] private Mesh voxelMesh;
		
		private struct Voxel {
			private uint id;
			private Vector3 position;
		}

		private ComputeBuffer voxelCountBuffer;
		private ComputeBuffer voxelsBuffer;
		private Texture3D colorTexture;

		private MeshCollider meshCollider;

		private static readonly int voxelsID = Shader.PropertyToID("voxels");
		private static readonly int numPointsPerAxisID = Shader.PropertyToID("numPointsPerAxis");
		private static readonly int objectToWorldID = Shader.PropertyToID("_ObjectToWorld");
		private static readonly int colorsID = Shader.PropertyToID("colors");
		private static readonly int colorSizeID = Shader.PropertyToID("colorSize");

		public override void BeforePointGeneration() {
			meshCollider = GetComponent<MeshCollider>();
			CreateBuffers();
		}

		private void CreateBuffers() {
			var numberOfCubesPerAxis = NumPointsPerAxis - 1;
			var numVoxels = numberOfCubesPerAxis * numberOfCubesPerAxis * numberOfCubesPerAxis;

			voxelCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
			voxelsBuffer = new ComputeBuffer(numVoxels, Marshal.SizeOf(typeof(Voxel)), ComputeBufferType.Append);

			voxelMaterial.enableInstancing = true;
			voxelMaterial.SetBuffer(voxelsID, voxelsBuffer);
			colorTexture = VoronoiGenerator.Utils.CreateRandomColors(0, NumPointsPerAxis, NumPointsPerAxis, NumPointsPerAxis);
			voxelMaterial.SetTexture(colorsID, colorTexture);
			voxelMaterial.SetVector(colorSizeID, new Vector4(colorTexture.width, colorTexture.height, colorTexture.depth, 0));
		}

		public override void AfterPointGeneration() {
			InitTextures();
		}

		private void InitTextures() {
			voxelShader.SetTexture(0, pointsID, pointsBuffer);
		}

		public override void AfterMeshGeneration() {
			meshCollider.sharedMesh = mesh; 
			
			GenerateVoxels();
		}

		private void GenerateVoxels() {
			voxelsBuffer.SetCounterValue(0);
			voxelShader.SetBuffer(0, voxelsID, voxelsBuffer);
			voxelShader.SetInt(numPointsPerAxisID, NumPointsPerAxis);
			ComputeHelper.Dispatch(voxelShader, NumPointsPerAxis, NumPointsPerAxis, NumPointsPerAxis);
		}

		private void Update() {
			Render();
		}

		public override void Render() {
			ComputeBuffer.CopyCount(voxelsBuffer, voxelCountBuffer, 0);
			int[] voxelCountArray = { 0 };
			voxelCountBuffer.GetData(voxelCountArray);
			var voxelsAmount = voxelCountArray[0];
			
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

		protected override void ReleaseBuffers() {
			base.ReleaseBuffers();
			voxelCountBuffer?.Release();
			voxelsBuffer?.Release();
		}
	}
}