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
		[SerializeField] private bool differentHeight;
		
		private struct Voxel {
			private uint id;
			private Vector3 position;
		}

		private ComputeBuffer voxelCountBuffer;
		private ComputeBuffer voxelsBuffer;
		private Texture3D colorTexture;
		private Texture2D heightTexture;

		private MeshCollider meshCollider;

		private static readonly int voxelsID = Shader.PropertyToID("voxels");
		private static readonly int numPointsPerAxisID = Shader.PropertyToID("numPointsPerAxis");
		private static readonly int objectToWorldID = Shader.PropertyToID("_ObjectToWorld");
		private static readonly int colorsID = Shader.PropertyToID("colors");
		private static readonly int colorSizeID = Shader.PropertyToID("colorSize");
		private static readonly int heightsID = Shader.PropertyToID("heights");

		protected override void BeforePointGeneration() {
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

			var voxelSize = BoundSize / NumPointsPerAxis;
			var range = new Vector2(-voxelSize.x * 2, voxelSize.x * 2);
			heightTexture = differentHeight 
				? Utils.GetRandomHeights(colorTexture.width, colorTexture.height, range) 
				: Utils.GetEmptyTexture(colorTexture.width, colorTexture.height);
			voxelMaterial.SetTexture(heightsID, heightTexture);
		}

		protected override void AfterPointGeneration() {
			InitTextures();
		}

		private void InitTextures() {
			voxelShader.SetTexture(0, pointsID, pointsBuffer);
		}

		protected override void AfterMeshGeneration() {
			meshCollider.sharedMesh = mesh; 
			
			GenerateVoxels();
			OnMeshChanged(mesh);
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
				worldBounds = new Bounds(Vector3.zero, BoundSize * 1.1f),
				shadowCastingMode = ShadowCastingMode.On,
				receiveShadows = true,
				matProps = new MaterialPropertyBlock()
			};
			rp.matProps.SetMatrix(objectToWorldID, Matrix4x4.TRS(transform.position, Quaternion.identity, BoundSize / NumPointsPerAxis));
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