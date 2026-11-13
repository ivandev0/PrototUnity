using System.Runtime.InteropServices;
using PrototUnity.Utils;
using UnityEngine;
using UnityEngine.Events;
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

		private ComputeBuffer voxelsBuffer;
		private ComputeBuffer modifiedCellsCountBuffer;
		private Texture3D colorTexture;
		private Texture2D heightTexture;
		private GraphicsBuffer drawArgsBuffer;
		private MaterialPropertyBlock propertyBlock;

		private MeshCollider meshCollider;

		public Vector3 Scale => BoundSize / NumPointsPerAxis;
		public event UnityAction<Modification[]> VoxelsModifiedEvent = delegate { }; 

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

			voxelsBuffer = new ComputeBuffer(numVoxels, Marshal.SizeOf(typeof(Voxel)), ComputeBufferType.Append);
			modifiedCellsCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
			drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
			drawArgsBuffer.SetData(new[] {
				new GraphicsBuffer.IndirectDrawIndexedArgs { indexCountPerInstance = voxelMesh.GetIndexCount(0) }
			});

			if (!voxelMaterial.enableInstancing) {
				Debug.LogWarning($"Material {voxelMaterial.name} has disabled instancing.");
				voxelMaterial.enableInstancing = true;
			}
			
			// Runtime data goes into the property block, not into the shared material asset.
			// Otherwise, saving assets during play mode re-serializes the material and resets values.
			propertyBlock = new MaterialPropertyBlock();

			propertyBlock.SetBuffer(voxelsID, voxelsBuffer);
			colorTexture = VoronoiGenerator.Utils.CreateRandomColors(0, NumPointsPerAxis, NumPointsPerAxis, NumPointsPerAxis);
			propertyBlock.SetTexture(colorsID, colorTexture);
			propertyBlock.SetVector(colorSizeID, new Vector4(colorTexture.width, colorTexture.height, colorTexture.depth, 0));

			var voxelSize = Scale;
			var range = new Vector2(-voxelSize.x * 2, voxelSize.x * 2);
			heightTexture = differentHeight 
				? Utils.GetRandomHeights(colorTexture.width, colorTexture.height, range) 
				: Utils.GetEmptyTexture(colorTexture.width, colorTexture.height);
			propertyBlock.SetTexture(heightsID, heightTexture);
		}

		protected override void AfterPointGeneration() {
			InitTextures();
		}

		private void InitTextures() {
			voxelShader.SetTexture(0, pointsID, pointsBuffer);
			propertyBlock.SetTexture(pointsID, pointsBuffer);
			propertyBlock.SetTexture(dugAreaID, dugBuffer);
		}

		protected override void AfterMeshGeneration() {
			meshCollider.sharedMesh = mesh; 
			
			GenerateVoxels();
			OnMeshChanged(mesh);
			GenerateModifiedVoxels();
		}

		private void GenerateVoxels() {
			voxelsBuffer.SetCounterValue(0);
			voxelShader.SetBuffer(0, voxelsID, voxelsBuffer);
			voxelShader.SetInt(numPointsPerAxisID, NumPointsPerAxis);
			ComputeHelper.Dispatch(voxelShader, NumPointsPerAxis, NumPointsPerAxis, NumPointsPerAxis);
			// `CopyCount` fills `instanceCount`. Happens on GPU
			GraphicsBuffer.CopyCount(voxelsBuffer, drawArgsBuffer, sizeof(uint));
		}

		private void GenerateModifiedVoxels() {
			ComputeBuffer.CopyCount(modifiedCellsBuffer, modifiedCellsCountBuffer, 0);
			int[] modifiedVoxelCountArray = { 0 };
			modifiedCellsCountBuffer.GetData(modifiedVoxelCountArray);
			var voxelsAmount = modifiedVoxelCountArray[0];

			var modifiedData = new Modification[voxelsAmount];
			modifiedCellsBuffer.GetData(modifiedData);
			VoxelsModifiedEvent.Invoke(modifiedData);
		}

		private void Update() {
			Render();
		}

		public override void Render() {
			propertyBlock.SetMatrix(objectToWorldID, Matrix4x4.TRS(transform.position, Quaternion.identity, Scale));
			var rp = new RenderParams(voxelMaterial) {
				worldBounds = new Bounds(Vector3.zero, BoundSize * 1.1f),
				shadowCastingMode = ShadowCastingMode.On,
				receiveShadows = true,
				matProps = propertyBlock
			};
			Graphics.RenderMeshIndirect(rp, voxelMesh, drawArgsBuffer);
		}

		protected override void ReleaseBuffers() {
			base.ReleaseBuffers();
			voxelsBuffer?.Release();
			drawArgsBuffer?.Release();
		}
	}
}