using System.Collections.Generic;
using System.Runtime.InteropServices;
using PrototUnity.AiTools.Voronoi;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace SebLague.MarchingCubes {
	public class VoronoiComputeGenerator : MonoBehaviour {
		private const string GeneratedCellPrefix = "VoronoiCell_";

		[SerializeField] private ComputeShader voronoiShader;
		[SerializeField] private Vector3Int voronoiSize = Vector3Int.one;
		[SerializeField] private Vector3 boundSize = Vector3.one;

		private struct VoronoiCell {
			public uint vertexCount;
			public uint vertexStart;
		}
		
		private ComputeBuffer voronoiCellsBuffer;
		private ComputeBuffer voronoiVerticesBuffer;
		private Texture3D seedTexture;

		private void Start() {
			Generate();
		}

		[ContextMenu("Generate Voronoi Mesh")]
		public void Generate() {
			if (voronoiShader == null) {
				Debug.LogError("Voronoi compute shader is not assigned.", this);
				return;
			}

			if (voronoiSize.x <= 0 || voronoiSize.y <= 0 || voronoiSize.z <= 0) {
				Debug.LogError($"Voronoi size must be positive. Current value: {voronoiSize}", this);
				return;
			}

			DispatchVoronoi(voronoiSize);
			BuildMeshesFromComputeData();
		}

		private void DispatchVoronoi(Vector3Int size) {
			ReleaseBuffers();

			var numCells = size.x * size.y * size.z;
			seedTexture = GenerateSeedTexture(size, boundSize);

			var kernel = voronoiShader.FindKernel("BuildVoronoiCells3D");
			voronoiCellsBuffer = new ComputeBuffer(numCells, Marshal.SizeOf<VoronoiCell>(), ComputeBufferType.Structured);
			voronoiVerticesBuffer = new ComputeBuffer(numCells * 255, Marshal.SizeOf<Vector3>(), ComputeBufferType.Structured);

			voronoiShader.SetInts("_CenterGridSize", size.x, size.y, size.z);
			voronoiShader.SetFloats("_WorldOrigin", 0, 0, 0);
			voronoiShader.SetFloats("_BoxSize", boundSize.x, boundSize.y, boundSize.z);
			voronoiShader.SetTexture(kernel, "centers", seedTexture);
			voronoiShader.SetBuffer(kernel, "cells", voronoiCellsBuffer);
			voronoiShader.SetBuffer(kernel, "vertices", voronoiVerticesBuffer);

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

		private void BuildMeshesFromComputeData() {
			if (voronoiCellsBuffer == null || voronoiVerticesBuffer == null) {
				Debug.LogError("Voronoi buffers were not created.", this);
				return;
			}

			var cells = new VoronoiCell[voronoiCellsBuffer.count];
			var vertices = new Vector3[voronoiVerticesBuffer.count];
			voronoiCellsBuffer.GetData(cells);
			voronoiVerticesBuffer.GetData(vertices);

			ClearGeneratedCells();
			var generatedMaterial = GenerateMaterial();
			
			for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++) {
				var cell = cells[cellIndex];

				var mesh = BuildCellMesh(cellIndex, cell, vertices);
				if (mesh == null) continue;

				var cellObject = new GameObject($"{GeneratedCellPrefix}{cellIndex}");
				cellObject.transform.SetParent(transform, worldPositionStays: false);
				cellObject.transform.localRotation = Quaternion.identity;
				cellObject.transform.localScale = Vector3.one;

				var cellMeshFilter = cellObject.AddComponent<MeshFilter>();
				cellMeshFilter.sharedMesh = mesh;

				var cellMeshRenderer = cellObject.AddComponent<MeshRenderer>();
				cellMeshRenderer.sharedMaterial = generatedMaterial;
			}
		}

		private static Mesh BuildCellMesh(int index, VoronoiCell cell, IReadOnlyList<Vector3> sourceVertices) {
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
				if (!child.name.StartsWith(GeneratedCellPrefix)) continue;

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

		private void ReleaseBuffers() {
			if (voronoiCellsBuffer != null) {
				voronoiCellsBuffer.Release();
				voronoiCellsBuffer = null;
			}

			if (voronoiVerticesBuffer != null) {
				voronoiVerticesBuffer.Release();
				voronoiVerticesBuffer = null;
			}

			if (seedTexture != null) {
				DestroyTexture(seedTexture);
				seedTexture = null;
			}
		}

		private void OnDestroy() {
			ReleaseBuffers();
			ClearGeneratedCells();
		}

		private void OnDisable() {
			ReleaseBuffers();
		}

		private static void DestroyTexture(Texture target) {
			if (Application.isPlaying) Destroy(target);
			else DestroyImmediate(target);
		}

		private static void DestroyGameObject(GameObject target) {
			if (Application.isPlaying) Destroy(target);
			else DestroyImmediate(target);
		}
	}
}
