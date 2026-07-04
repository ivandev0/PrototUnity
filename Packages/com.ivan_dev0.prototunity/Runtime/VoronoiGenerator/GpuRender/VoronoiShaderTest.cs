using System.Runtime.InteropServices;
using UnityEngine;

namespace PrototUnity.VoronoiGenerator.GpuRender {
	public class VoronoiShaderTest : AbstractVoronoiComputeGenerator {
		[SerializeField] private Vector3Int size = new Vector3Int(10, 10, 10);

		private ComputeBuffer voronoiCellBuffer;
		private ComputeBuffer voronoiVertexBuffer;

		public override void Generate() {
			var cells = new VoronoiCell[size.x * size.y * size.z];
			voronoiCellBuffer = new ComputeBuffer(cells.Length,
				Marshal.SizeOf(typeof(VoronoiCell)), ComputeBufferType.Structured);

			for (uint i = 0; i < cells.Length; i++) {
				cells[i] = new VoronoiCell {
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

		public override (ComputeBuffer cells, ComputeBuffer vertices) GetGeneratedData() {
			return (voronoiCellBuffer, voronoiVertexBuffer);
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
	}
}