using System.Runtime.InteropServices;
using PrototUnity.AiTools.Voronoi;
using SebLague.MarchingCubes;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace PrototUnity.VoronoiGenerator {
	public class VoronoiComputeGenerator : AbstractVoronoiComputeGenerator {
		[SerializeField] private ComputeShader voronoiShader;
		[SerializeField] private Vector3 boundSize = Vector3.one;

		private static readonly int centerGridSizeID = Shader.PropertyToID("_CenterGridSize");
		private static readonly int worldOriginID = Shader.PropertyToID("_WorldOrigin");
		private static readonly int boxSizeID = Shader.PropertyToID("_BoxSize");
		private static readonly int centersID = Shader.PropertyToID("centers");
		private static readonly int cellsID = Shader.PropertyToID("cells");
		private static readonly int verticesID = Shader.PropertyToID("vertices");
		
		private Texture3D seedTexture;

		private void Start() {
			Generate();
		}

		public override void Generate() {
			if (voronoiShader == null) {
				Debug.LogError("Voronoi compute shader is not assigned.", this);
				return;
			}

			if (size.x <= 0 || size.y <= 0 || size.z <= 0) {
				Debug.LogError($"Voronoi size must be positive. Current value: {size}", this);
				return;
			}

			DispatchVoronoi();
		}

		private void DispatchVoronoi() {
			ReleaseBuffers();

			var numCells = size.x * size.y * size.z;
			seedTexture = GenerateSeedTexture(size, boundSize);

			var kernel = voronoiShader.FindKernel("BuildVoronoiCells3D");
			VoronoiCellsBuffer = new ComputeBuffer(numCells, Marshal.SizeOf<VoronoiCell>(), ComputeBufferType.Structured);
			VoronoiVerticesBuffer = new ComputeBuffer(numCells * 255, Marshal.SizeOf<Vector3>(), ComputeBufferType.Structured);

			voronoiShader.SetInts(centerGridSizeID, size.x, size.y, size.z);
			voronoiShader.SetFloats(worldOriginID, 0, 0, 0);
			voronoiShader.SetFloats(boxSizeID, boundSize.x, boundSize.y, boundSize.z);
			voronoiShader.SetTexture(kernel, centersID, seedTexture);
			voronoiShader.SetBuffer(kernel, cellsID, VoronoiCellsBuffer);
			voronoiShader.SetBuffer(kernel, verticesID, VoronoiVerticesBuffer);

			ComputeHelper.Dispatch(voronoiShader, size.x, size.y, size.z);
		}

		public static Texture3D GenerateSeedTexture(Vector3Int size, Vector3 boundSize) {
			var voronoiGeneratorParameters = new VoronoiGeneratorParameters(
				seed: 0,
				cellCount: size,
				cubeSize: boundSize,
				uniform: false
			);

			Random.InitState(voronoiGeneratorParameters.seed);
			var seeds = PrototUnity.AiTools.Voronoi.Utils.PickSeeds(voronoiGeneratorParameters);
			
			var texture = new Texture3D(size.x, size.y, size.z, GraphicsFormat.R32G32B32A32_SFloat, TextureCreationFlags.None) {
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				name = $"VoronoiSeeds3D_{size.x}x{size.y}x{size.z}"
			};
			
			for (var x = 0; x < size.x; x++) { 
				for (var y = 0; y < size.y; y++) { 
					for (var z = 0; z < size.z; z++) {
						var seedIndex = PrototUnity.AiTools.Voronoi.Utils.ToSeedIndex(size, new Vector3Int(x, y, z));
						var position = seeds[seedIndex].position - boundSize * 0.5f;
						texture.SetPixel(x, y, z, new Color(position.x, position.y, position.z, 1));
					}
				}
			}
			
			texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			return texture;
		}

		protected override void ReleaseBuffers() {
			if (seedTexture != null) {
				DestroyTexture(seedTexture);
				seedTexture = null;
			}
		}

		private static void DestroyTexture(Texture target) {
			if (Application.isPlaying) Destroy(target);
			else DestroyImmediate(target);
		}
	}
}
