using System.Runtime.InteropServices;
using UnityEngine;

namespace PrototUnity.VoronoiGenerator.GpuRender {
	public class VoronoiGeneratorTest : AbstractVoronoiComputeGenerator {
		public override void Generate() {
			var cells = new VoronoiCell[Size.x * Size.y * Size.z];
			VoronoiCellsBuffer = new ComputeBuffer(cells.Length,
				Marshal.SizeOf(typeof(VoronoiCell)), ComputeBufferType.Structured);

			for (uint i = 0; i < cells.Length; i++) {
				cells[i] = new VoronoiCell {
					vertexCount = 36,
					vertexStart = 36 * i
				};
			}

			VoronoiCellsBuffer.SetData(cells);

			var vertices = new Vector3[36 * cells.Length];
			VoronoiVerticesBuffer = new ComputeBuffer(
				vertices.Length, Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Structured
			);

			for (var i = 0; i < Size.x; i++) {
				for (var j = 0; j < Size.y; j++) {
					for (var k = 0; k < Size.z; k++) {
						var cube = CreateVertices(new Vector3(i, j, k));
						cube.CopyTo(vertices, (k + i * Size.z + j * Size.z * Size.y) * 36);
					}
				}
			}

			VoronoiVerticesBuffer.SetData(vertices);
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