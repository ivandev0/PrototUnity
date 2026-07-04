using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrototUnity.VoronoiGenerator.CpuRender {
	public class VoronoiCPURender : MonoBehaviour {
		[SerializeField] private AbstractVoronoiComputeGenerator voronoiGenerator;

		private const string generatedCellPrefix = "VoronoiCell_";

		private void Start() {
			Generate();
		}

		[ContextMenu("Generate Voronoi Mesh")]
		public void Generate() {
			ClearGeneratedCells();
			voronoiGenerator.Generate();
			var (cells, vertices) = FillArrays(voronoiGenerator.VoronoiCellsBuffer, voronoiGenerator.VoronoiVerticesBuffer);
			BuildMeshesFromComputeData(cells, vertices);
		}
		
		private (AbstractVoronoiComputeGenerator.VoronoiCell[], Vector3[]) FillArrays(
			ComputeBuffer voronoiCellsBuffer, ComputeBuffer voronoiVerticesBuffer
		) {
			var cells = new AbstractVoronoiComputeGenerator.VoronoiCell[voronoiCellsBuffer.count];
			var vertices = new Vector3[voronoiVerticesBuffer.count];
			voronoiCellsBuffer.GetData(cells);
			voronoiVerticesBuffer.GetData(vertices);
			return (cells, vertices);
		}
		
		private void BuildMeshesFromComputeData(AbstractVoronoiComputeGenerator.VoronoiCell[] cells, Vector3[] vertices) {
			ClearGeneratedCells();
			var generatedMaterial = GenerateMaterial();
			
			for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++) {
				var cell = cells[cellIndex];

				var mesh = BuildCellMesh(cellIndex, cell, vertices);
				if (mesh == null) continue;

				var cellObject = new GameObject($"{generatedCellPrefix}{cellIndex}");
				cellObject.transform.SetParent(transform, worldPositionStays: false);
				cellObject.transform.localRotation = Quaternion.identity;
				cellObject.transform.localScale = Vector3.one;

				var cellMeshFilter = cellObject.AddComponent<MeshFilter>();
				cellMeshFilter.sharedMesh = mesh;

				var cellMeshRenderer = cellObject.AddComponent<MeshRenderer>();
				cellMeshRenderer.sharedMaterial = generatedMaterial;
			}
		}

		private static Mesh BuildCellMesh(int index, AbstractVoronoiComputeGenerator.VoronoiCell cell, IReadOnlyList<Vector3> sourceVertices) {
			if (cell.vertexCount < 3) return null;

			var vertices = new List<Vector3>((int) cell.vertexCount);
			var triangles = new List<int>((int) cell.vertexCount);

			for (var i = 0; i < cell.vertexCount; i++) {
				vertices.Add(sourceVertices[i + (int) cell.vertexStart]);
			}

			for (var i = 0; i < cell.vertexCount; i++) {
				triangles.Add(i);
			}

			var mesh = new Mesh {
				name = $"Voronoi Cell {index}",
				indexFormat = IndexFormat.UInt32
			};
			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			mesh.RecalculateNormals();
			return mesh;
		}

		private void ClearGeneratedCells() {
			for (var i = transform.childCount - 1; i >= 0; i--) {
				var child = transform.GetChild(i);
				if (!child.name.StartsWith(generatedCellPrefix)) continue;

				DestroyGameObject(child.gameObject);
			}
		}

		private Material GenerateMaterial() {
			var shader = Shader.Find("Universal Render Pipeline/Lit");
			if (shader == null) shader = Shader.Find("Standard");
			return new Material(shader) {
				name = "Generated Voronoi Mesh Material",
				color = new Color(0.75f, 0.82f, 1f, 1f)
			};
		}
		
		private static void DestroyGameObject(GameObject target) {
			if (Application.isPlaying) Destroy(target);
			else DestroyImmediate(target);
		}
	}
}