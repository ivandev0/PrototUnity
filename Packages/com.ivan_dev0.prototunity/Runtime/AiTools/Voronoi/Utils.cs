using Unity.Collections;
using UnityEngine;

namespace PrototUnity.AiTools.Voronoi {
	public struct CellIndex {
		public readonly Vector3Int index;
		public readonly Vector3 position;

		public CellIndex(Vector3Int index, Vector3 position) {
			this.index = index;
			this.position = position;
		}
	}
	
	public static class Utils {
		public static CellIndex[] PickSeeds(
			VoronoiGeneratorParameters generatorParameters
		) {
			Vector3Int cellCount = generatorParameters.cellCount;
			Vector3 cubeSize = generatorParameters.cubeSize;
			var totalSeeds = cellCount.x * cellCount.y * cellCount.z;
			var seeds = new CellIndex[totalSeeds];
			var maxAttempts = Mathf.Max(200, totalSeeds * 50);

			var space = new Vector3(cubeSize.x / cellCount.x, cubeSize.y / cellCount.y, cubeSize.z / cellCount.z);
			for (var y = 0; y < cellCount.y; y++) {
				for (var x = 0; x < cellCount.x; x++) {
					for (var z = 0; z < cellCount.z; z++) {
						var attempts = 0;
						while (attempts < maxAttempts) {
							attempts++;
							var point = generatorParameters.seedModifier.Invoke(generatorParameters, new Vector3Int(x, y, z));

							var center = new Vector3(
								space.x * 0.5f + x * space.x,
								space.y * 0.5f + y * space.y,
								space.z * 0.5f + z * space.z
							);

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

		public static int ToSeedIndex(Vector3Int cellCount, Vector3Int index) {
			return index.z + index.x * cellCount.z + index.y * cellCount.x * cellCount.z;
		}

		public static bool IsPointInsideCube(Vector3 point, Vector3 cubeCenter, Vector3 cubeSize) {
			if (point.x < cubeCenter.x - cubeSize.x * 0.5f || point.x > cubeCenter.x + cubeSize.x * 0.5f) return false;
			if (point.y < cubeCenter.y - cubeSize.y * 0.5f || point.y > cubeCenter.y + cubeSize.y * 0.5f) return false;
			if (point.z < cubeCenter.z - cubeSize.z * 0.5f || point.z > cubeCenter.z + cubeSize.z * 0.5f) return false;
			return true;
		}
	}
}