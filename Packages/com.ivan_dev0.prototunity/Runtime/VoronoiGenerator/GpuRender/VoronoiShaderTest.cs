using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototUnity.VoronoiGenerator.GpuRender {
	public class VoronoiShaderTest : MonoBehaviour {
		[SerializeField] private Material voronoiMaterial;
		[SerializeField] private Vector3 boundSize;
		[SerializeField] private Vector3Int count = new Vector3Int(10, 10, 10);

		private Mesh mesh;
		private ComputeBuffer voronoiCellBuffer;
		private ComputeBuffer voronoiVertexBuffer;

		private static readonly int cellsID = Shader.PropertyToID("cells");
		private static readonly int verticesID = Shader.PropertyToID("vertices");
		private static readonly int colorsID = Shader.PropertyToID("colors");
		private static readonly int colorSizeID = Shader.PropertyToID("colorSize");

		private void Start() {
			GenerateVoronoi(count);
			SetUpMaterial(count);
		}

		private void Update() {
			Show(count);
		}

		void GenerateVoronoi(Vector3Int size) {
			var cells = new VoronoiComputeGenerator.VoronoiCell[size.x * size.y * size.z];
			voronoiCellBuffer = new ComputeBuffer(cells.Length,
				Marshal.SizeOf(typeof(VoronoiComputeGenerator.VoronoiCell)), ComputeBufferType.Structured);

			for (uint i = 0; i < cells.Length; i++) {
				cells[i] = new VoronoiComputeGenerator.VoronoiCell {
					vertexCount = 36,
					vertexStart = 36 * i
				};
			}

			voronoiCellBuffer.SetData(cells);

			var vertices = new Vector3[36 * cells.Length];
			voronoiVertexBuffer = new ComputeBuffer(
				vertices.Length, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Structured
			);

			for (var i = 0; i < size.x; i++) {
				for (var j = 0; j < size.y; j++) {
					for (var k = 0; k < size.z; k++) {
						var cube = CreateVertices(new Vector3(i, j, k));
						cube.CopyTo(vertices, (k + i * size.z + j * size.z * size.y) * 36);
					}
				}
			}

			voronoiVertexBuffer.SetData(vertices);
		}

		private static Vector3[] CreateVertices(Vector3 offset) {
			var h = 0.5f;
			Vector3[] vertices = {
				// Front face
				new(-h, -h, h), new(h, -h, h), new(h, h, h),
				new(-h, -h, h), new(h, h, h), new(-h, h, h),

				// Back face
				new(h, -h, -h), new(-h, -h, -h), new(-h, h, -h),
				new(h, -h, -h), new(-h, h, -h), new(h, h, -h),

				// Left face
				new(-h, -h, -h), new(-h, -h, h), new(-h, h, h),
				new(-h, -h, -h), new(-h, h, h), new(-h, h, -h),

				// Right face
				new(h, -h, h), new(h, -h, -h), new(h, h, -h),
				new(h, -h, h), new(h, h, -h), new(h, h, h),

				// Top face
				new(-h, h, h), new(h, h, h), new(h, h, -h),
				new(-h, h, h), new(h, h, -h), new(-h, h, -h),

				// Bottom face
				new(-h, -h, -h), new(h, -h, -h), new(h, -h, h),
				new(-h, -h, -h), new(h, -h, h), new(-h, -h, h),
			};
			
			for (var i = 0; i < vertices.Length; i++) {
				vertices[i] += offset;
			}

			return vertices;
		}

		private void SetUpMaterial(Vector3Int size) {
			voronoiMaterial.enableInstancing = true;

			voronoiMaterial.SetBuffer(cellsID, voronoiCellBuffer);
			voronoiMaterial.SetBuffer(verticesID, voronoiVertexBuffer);

			var colorTexture = Utils.CreateRandomColors(0, size.x, size.y, size.z);
			voronoiMaterial.SetTexture(colorsID, colorTexture);
			voronoiMaterial.SetVector(colorSizeID, new Vector4(size.x, size.y, size.z, 0));

			mesh = new Mesh();
			var verticesCount = 63;
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