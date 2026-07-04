using UnityEngine;
using UnityEngine.Rendering;

namespace PrototUnity.VoronoiGenerator.GpuRender {
	public class VoronoiGPURender : MonoBehaviour {
		[SerializeField] private AbstractVoronoiComputeGenerator voronoiGenerator;
		[SerializeField] private Material voronoiMaterial;
		[SerializeField] private Vector3 boundSize;
		[SerializeField] private Vector3Int count = new Vector3Int(10, 10, 10);

		private Mesh mesh;
		private ComputeBuffer idBuffer;

		private static readonly int cellsID = Shader.PropertyToID("cells");
		private static readonly int verticesID = Shader.PropertyToID("vertices");
		private static readonly int colorsID = Shader.PropertyToID("colors");
		private static readonly int colorSizeID = Shader.PropertyToID("colorSize");

		private void Start() {
			GenerateMesh();

			voronoiGenerator.Generate();
			var (cellsBuffer, verticesBuffer) = voronoiGenerator.GetGeneratedData();
			SetUpMaterial(count, cellsBuffer, verticesBuffer);
		}

		private void Update() {
			Show(count);
		}

		private void SetUpMaterial(Vector3Int size, ComputeBuffer voronoiCellBuffer, ComputeBuffer voronoiVertexBuffer) {
			voronoiMaterial.enableInstancing = true;

			voronoiMaterial.SetBuffer(cellsID, voronoiCellBuffer);
			voronoiMaterial.SetBuffer(verticesID, voronoiVertexBuffer);

			var colorTexture = Utils.CreateRandomColors(0, size.x, size.y, size.z);
			voronoiMaterial.SetTexture(colorsID, colorTexture);
			voronoiMaterial.SetVector(colorSizeID, new Vector4(size.x, size.y, size.z, 0));
		}

		private void GenerateMesh() {
			mesh = new Mesh {
				indexFormat = IndexFormat.UInt32
			};
			var verticesCount = 255;
			var vertices = new Vector3[verticesCount];
			var triangles = new int[verticesCount];
			for (var i = 0; i < verticesCount; i++) {
				vertices[i] = Vector3.zero;
				triangles[i] = i;
			}

			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
		}

		private void Show(Vector3Int size) {
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
			commandData[0].indexCountPerInstance = mesh.GetIndexCount(0);
			commandData[0].instanceCount = (uint)(size.x * size.y * size.z);
			commandBuf.SetData(commandData);
			Graphics.RenderMeshIndirect(rp, mesh, commandBuf, commandCount);
		}
	}
}