using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PrototUnity.AiTools.Voronoi {
	public struct VoronoiGeneratorParameters {
		public readonly int seed;

		public readonly Vector3Int cellCount;
		public readonly Vector3 cubeSize;

		public readonly bool uniform;

		public VoronoiGeneratorParameters(
			int seed,
			Vector3Int cellCount,
			Vector3 cubeSize,
			bool uniform
		) {
			this.seed = seed;
			this.cellCount = cellCount;
			this.cubeSize = cubeSize;
			this.uniform = uniform;
		}
	}

	public struct VoronoiMeshParameters {
		public enum CellCenterPosition {
			MassCenter, Index
		}
		
		[CanBeNull] public readonly Material faceMaterial;
		[CanBeNull] public readonly GameObject cellPrefab;
		public readonly bool tintByCell;
		public readonly float cellShrink;
		[CanBeNull] public readonly Transform parent;
		public readonly CellCenterPosition cellCenterPosition;
		public readonly Vector3 cellCenterShift;

		public VoronoiMeshParameters(
			[CanBeNull] Material faceMaterial,
			[CanBeNull] GameObject cellPrefab,
			bool tintByCell,
			float cellShrink,
			[CanBeNull] Transform parent,
			CellCenterPosition cellCenterPosition, 
			Vector3 cellCenterShift
		) {
			this.faceMaterial = faceMaterial;
			this.cellPrefab = cellPrefab;
			this.tintByCell = tintByCell;
			this.cellShrink = cellShrink;
			this.parent = parent;
			this.cellCenterPosition = cellCenterPosition;
			this.cellCenterShift = cellCenterShift;
		}
	}
	
	public class Voronoi3DGenerator {
		private readonly VoronoiGeneratorParameters generatorParameters;
		private readonly VoronoiMeshParameters meshParameters;

		private const float EPSILON = 1e-5f;
		
		private struct CellIndex {
			public readonly Vector3Int index;
			public readonly Vector3 position;

			public CellIndex(Vector3Int index, Vector3 position) {
				this.index = index;
				this.position = position;
			}
		}

		private struct VoronoiCells : IDisposable {
			public NativeArray<CellIndex> indexAndSeeds;
			public NativeList<ConvexPolyhedron> cells;
			public NativeList<ConvexFace> faces;
			public NativeList<Vector3> vertices;

			public VoronoiCells(
				NativeArray<CellIndex> indexAndSeeds,
				NativeList<ConvexPolyhedron> cells,
				NativeList<ConvexFace> faces,
				NativeList<Vector3> vertices
			) {
				this.indexAndSeeds = indexAndSeeds;
				this.cells = cells;
				this.faces = faces;
				this.vertices = vertices;
			}

			public void Dispose() {
				if (indexAndSeeds.IsCreated) indexAndSeeds.Dispose();
				if (cells.IsCreated) cells.Dispose();
				if (faces.IsCreated) faces.Dispose();
				if (vertices.IsCreated) vertices.Dispose();
			}
		}

		public Voronoi3DGenerator(
			VoronoiGeneratorParameters generatorParameters,
			VoronoiMeshParameters meshParameters
		) {
			this.generatorParameters = generatorParameters;
			this.meshParameters = meshParameters;
		}
		
		public void Generate() {
			Random.InitState(generatorParameters.seed);
			
			var generatedCells = GenerateCells(
				PickSeeds(generatorParameters),
				generatorParameters
			);
			try {
				GenerateGameObjects(generatedCells, meshParameters);
			} finally {
				generatedCells.Dispose();
			}
		}

		private static VoronoiCells GenerateCells(
			NativeArray<CellIndex> indexAndSeeds,
			VoronoiGeneratorParameters generatorParameters
		) {
			var stream = new NativeStream(indexAndSeeds.Length, Allocator.Persistent);
			try {
				var job = new VoronoiJob(
					indexAndSeeds,
					generatorParameters,
					stream.AsWriter()
				);
				
				JobHandle jobHandle = default;
				jobHandle = job.ScheduleByRef(indexAndSeeds.Length, 1, jobHandle);
				jobHandle.Complete();

				return ReadCells(indexAndSeeds, stream);
			} catch {
				if (indexAndSeeds.IsCreated) indexAndSeeds.Dispose();
				throw;
			} finally {
				if (stream.IsCreated) stream.Dispose();
			}
		}

		private static VoronoiCells ReadCells(NativeArray<CellIndex> indexAndSeeds, NativeStream stream) {
			var cells = new NativeList<ConvexPolyhedron>(indexAndSeeds.Length, Allocator.Persistent);
			var faces = new NativeList<ConvexFace>(Mathf.Max(1, indexAndSeeds.Length * 6), Allocator.Persistent);
			var vertices = new NativeList<Vector3>(Mathf.Max(1, indexAndSeeds.Length * 24), Allocator.Persistent);
			try {
				var reader = stream.AsReader();
				for (var cellIndex = 0; cellIndex < indexAndSeeds.Length; cellIndex++) {
					reader.BeginForEachIndex(cellIndex);
					var faceCount = reader.Read<int>();
					var faceStart = faces.Length;
					for (var faceIndex = 0; faceIndex < faceCount; faceIndex++) {
						var normal = reader.Read<Vector3>();
						var vertexCount = reader.Read<int>();
						var vertexStart = vertices.Length;
						for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++) {
							vertices.Add(reader.Read<Vector3>());
						}
						faces.Add(new ConvexFace(vertexStart, vertexCount, normal));
					}

					cells.Add(new ConvexPolyhedron(faceStart, faceCount));
					reader.EndForEachIndex();
				}

				return new VoronoiCells(indexAndSeeds, cells, faces, vertices);
			} catch {
				if (cells.IsCreated) cells.Dispose();
				if (faces.IsCreated) faces.Dispose();
				if (vertices.IsCreated) vertices.Dispose();
				throw;
			}
		}

		private static void GenerateGameObjects(VoronoiCells generatedCells, VoronoiMeshParameters meshParameters) {
			for (var i = 0; i < generatedCells.cells.Length; i++) {
				var index = generatedCells.indexAndSeeds[i];
				var cell = generatedCells.cells[i];
				if (cell.IsEmpty) return;
				
				var cellGO = CreateCellGameObject(meshParameters, generatedCells, i);
				cellGO.name = $"Cell_{index.index.ToString()}";
			}
		}

		private static NativeArray<CellIndex> PickSeeds(
			VoronoiGeneratorParameters generatorParameters
		) {
			Vector3Int cellCount = generatorParameters.cellCount;
			Vector3 cubeSize = generatorParameters.cubeSize;
			bool uniform = generatorParameters.uniform;
			var totalSeeds = cellCount.x * cellCount.y * cellCount.z;
			var seeds = new NativeArray<CellIndex>(totalSeeds, Allocator.Persistent);
			var maxAttempts = Mathf.Max(200, totalSeeds * 50);

			var space = new Vector3(cubeSize.x / cellCount.x, cubeSize.y / cellCount.y, cubeSize.z / cellCount.z);
			for (var y = 0; y < cellCount.y; y++) {
				for (var x = 0; x < cellCount.x; x++) {
					for (var z = 0; z < cellCount.z; z++) { 
						var attempts = 0;
						while (attempts < maxAttempts) {
							attempts++;

							var center = new Vector3(
								space.x * 0.5f + x * space.x,
								space.y * 0.5f + y * space.y,
								space.z * 0.5f + z * space.z
							);

							var randomnessPower = uniform ? 0 : Mathf.Min(0.5f, Mathf.Max(0, cellCount.y - y - 1) * 0.1f);
							var randomness = new Vector3(
								Random.Range(-space.x * 0.5f, space.x * 0.5f),
								Random.Range(-space.y * 0.5f, space.y * 0.5f),
								Random.Range(-space.z * 0.5f, space.z * 0.5f)
							) * randomnessPower;
							var point = center + randomness;
							
							if (!IsPointInsideCube(point, cubeSize * 0.5f, cubeSize)) continue;
							if (!IsPointInsideCube(point, center, space)) continue;
							
							var seedIndex = ToSeedIndex(cellCount, new Vector3Int(x, y, z));
							seeds[seedIndex] = new CellIndex(new Vector3Int(x, y, z), point);
							break;
						}
					}
				}
			}
			
			return seeds;
		}

		private static int ToSeedIndex(Vector3Int cellCount, Vector3Int index) {
			return index.z + index.x * cellCount.z + index.y * cellCount.x * cellCount.z;
		}
		
		private static bool IsPointInsideCube(Vector3 point, Vector3 cubeCenter, Vector3 cubeSize) {
			if (point.x < cubeCenter.x - cubeSize.x * 0.5f || point.x > cubeCenter.x + cubeSize.x * 0.5f) return false;
			if (point.y < cubeCenter.y - cubeSize.y * 0.5f || point.y > cubeCenter.y + cubeSize.y * 0.5f) return false;
			if (point.z < cubeCenter.z - cubeSize.z * 0.5f || point.z > cubeCenter.z + cubeSize.z * 0.5f) return false;
			return true;
		}

		private static GameObject CreateCellGameObject(VoronoiMeshParameters parameters, VoronoiCells generatedCells, int cellIndex) {
			var index = generatedCells.indexAndSeeds[cellIndex];
			var cell = generatedCells.cells[cellIndex];
			var centroid = parameters.cellCenterPosition switch {
				VoronoiMeshParameters.CellCenterPosition.MassCenter => cell.ComputeCentroid(generatedCells.faces, generatedCells.vertices),
				VoronoiMeshParameters.CellCenterPosition.Index => index.index,
				_ => throw new ArgumentOutOfRangeException()
			} + parameters.cellCenterShift;

			GameObject cellGO;
			cellGO = parameters.cellPrefab == null ? new GameObject() : GameObject.Instantiate(parameters.cellPrefab);
			cellGO.transform.SetParent(parameters.parent, worldPositionStays: false);
			cellGO.transform.localPosition = centroid;
			cellGO.transform.localRotation = Quaternion.identity;
			cellGO.transform.localScale = Vector3.one;

			var keep = 1f - parameters.cellShrink;

			var tint = parameters.tintByCell
				? Color.HSVToRGB(Random.value, 0.45f, 0.95f)
				: Color.white;
			var baseMat = parameters.faceMaterial != null ? parameters.faceMaterial : CreateDefaultMaterial();
			baseMat.color = tint;

			for (var i = 0; i < cell.faceCount; i++) {
				var face = generatedCells.faces[cell.faceStart + i];
				var vertices = CreateLocalFaceVertices(face, generatedCells.vertices, centroid, keep);
				var faceCenter = ComputeCenter(vertices);

				var faceGO = new GameObject($"Face_{i}");
				faceGO.transform.SetParent(cellGO.transform, worldPositionStays: false);
				faceGO.transform.localPosition = faceCenter;
				faceGO.transform.localRotation = Quaternion.identity;
				faceGO.transform.localScale = Vector3.one;

				var mf = faceGO.AddComponent<MeshFilter>();
				var mr = faceGO.AddComponent<MeshRenderer>();
				mr.sharedMaterial = baseMat;
				mf.sharedMesh = BuildFaceMesh(face, vertices, faceCenter);
			}

			var meshCollider = cellGO.AddComponent<MeshCollider>();
			meshCollider.sharedMesh = Combine(cellGO.GetComponentsInChildren<MeshFilter>());
			return cellGO;
		}

		private static List<Vector3> CreateLocalFaceVertices(
			ConvexFace face, NativeList<Vector3> sourceVertices, Vector3 centroid, float keep
		) {
			var output = new List<Vector3>(face.vertexCount);
			for (var i = 0; i < face.vertexCount; i++) {
				var sourceVertex = sourceVertices[face.vertexStart + i];
				output.Add(Vector3.Lerp(centroid, sourceVertex, keep) - centroid);
			}

			return output;
		}

		private static Vector3 ComputeCenter(IReadOnlyList<Vector3> vertices) {
			var center = Vector3.zero;
			for (var i = 0; i < vertices.Count; i++) {
				center += vertices[i];
			}

			return vertices.Count > 0 ? center / vertices.Count : Vector3.zero;
		}

		private static Mesh BuildFaceMesh(ConvexFace face, IReadOnlyList<Vector3> vertices, Vector3 center) {
			var mesh = new Mesh { name = "VoronoiFace" };

			var meshVertices = new List<Vector3>(vertices.Count);
			for (var i = 0; i < vertices.Count; i++) {
				meshVertices.Add(vertices[i] - center);
			}
			mesh.SetVertices(meshVertices);

			var triCount = Mathf.Max(0, vertices.Count - 2);
			var tris = new int[triCount * 3];
			for (var i = 0; i < triCount; i++) {
				tris[i * 3 + 0] = 0;
				tris[i * 3 + 1] = i + 1;
				tris[i * 3 + 2] = i + 2;
			}
			mesh.SetTriangles(tris, 0);

			var normals = new Vector3[vertices.Count];
			for (var i = 0; i < normals.Length; i++) normals[i] = face.normal;
			mesh.SetNormals(normals);

			mesh.RecalculateBounds();
			return mesh;
		}

		private static Material CreateDefaultMaterial() {
			Shader shader = Shader.Find("Universal Render Pipeline/Lit");
			if (shader == null) shader = Shader.Find("Standard");
			var mat = new Material(shader) { name = "VoronoiDefault" };
			mat.color = new Color(0.75f, 0.75f, 0.8f, 1f);
			return mat;
		}

		private static Mesh Combine(MeshFilter[] meshFilters) {
			var instances = new CombineInstance[meshFilters.Length];

			for (var i = 0; i < meshFilters.Length; i++) {
				var meshFilter = meshFilters[i];
            
				instances[i] = new CombineInstance {
					mesh = meshFilter.sharedMesh,
					transform = Matrix4x4.Translate(meshFilter.transform.localPosition),
				};
			}

			var combinedMesh = new Mesh();
			combinedMesh.CombineMeshes(instances, mergeSubMeshes:true, useMatrices:true);
			return combinedMesh;
		}
		
		private struct VoronoiJob : IJobParallelFor {
			[WriteOnly] public NativeStream.Writer result;
			[ReadOnly] public NativeArray<CellIndex> indexAndSeeds;
			private readonly VoronoiGeneratorParameters parameters;

			public VoronoiJob(
				NativeArray<CellIndex> indexAndSeeds,
				VoronoiGeneratorParameters parameters,
				NativeStream.Writer result
			) {
				this.parameters = parameters;
				this.indexAndSeeds = indexAndSeeds;
				this.result = result;
			}

			public void Execute(int index) {
				var writer = result;
				writer.BeginForEachIndex(index);
				var seed = indexAndSeeds[index];
				var neighbourIndex = GetNeighbourSeeds(parameters.cellCount, seed.index);
				try {
					BuildCell(seed, neighbourIndex, parameters.cubeSize, ref writer);
				} finally {
					neighbourIndex.Dispose();
					writer.EndForEachIndex();
				}
			}
			
			private static NativeList<Vector3Int> GetNeighbourSeeds(
				Vector3Int cellCount,
				Vector3Int index
			) {
				var result = new NativeList<Vector3Int>(Allocator.Temp);
				for (var y = -1; y <= 1; y++) {
					for (var x = -1; x <= 1; x++) {
						for (var z = -1; z <= 1; z++) {
							var offset = new Vector3Int(x, y, z);
							if (offset == Vector3Int.zero) continue;
							var neighbourIndex = index + offset;
							if (!IsIndexInsideGrid(neighbourIndex, cellCount)) continue;
							result.Add(neighbourIndex);
						}
					}
				}

				return result;
			}

			private static bool IsIndexInsideGrid(Vector3Int index, Vector3Int cellCount) {
				return index.x >= 0 && index.x < cellCount.x
					&& index.y >= 0 && index.y < cellCount.y
					&& index.z >= 0 && index.z < cellCount.z;
			}
			
			private void BuildCell(
				CellIndex seed,
				NativeList<Vector3Int> neighbourIndexes,
				Vector3 cubeSize,
				ref NativeStream.Writer writer
			) {
				var poly = ConvexPolyhedronBuilder.CreateBox(cubeSize * 0.5f, cubeSize);
				try {
					for (var i = 0; i < neighbourIndexes.Length; i++) {
						var neighbourIndex = neighbourIndexes[i];
						var neighbourSeed = indexAndSeeds[ToSeedIndex(parameters.cellCount, neighbourIndex)];
						var vectorBetweenCenters = neighbourSeed.position - seed.position;
						var length = vectorBetweenCenters.magnitude;
						if (length < EPSILON) continue;
					
						var normal = vectorBetweenCenters.normalized;
						var middlePoint = (seed.position + neighbourSeed.position) * 0.5f;
						var clippingPlaneOffsetFromOrigin = Vector3.Dot(normal, middlePoint);
						poly.ClipByPlane(normal, clippingPlaneOffsetFromOrigin);
						if (poly.FaceCount != 0) continue;

						writer.Write(0);
						return;
					}

					poly.WriteTo(ref writer);
				} finally {
					poly.Dispose();
				}
			}
		}
	}
}
