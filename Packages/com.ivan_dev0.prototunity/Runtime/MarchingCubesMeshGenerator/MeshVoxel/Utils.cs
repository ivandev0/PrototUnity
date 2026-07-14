using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PrototUnity.MarchingCubesMeshGenerator.MeshVoxel {
	public static class Utils {
		public static Texture2D GetRandomHeights(int width, int height, Vector2 range) {
			return GenerateTexture(width, height, (_, _) => Random.value * (range.y - range.x) + range.x);
		}
		
		public static Texture2D GetEmptyTexture(int width, int height) {
			return GenerateTexture(width, height, (_, _) => 0f);
		}
		
		private static Texture2D GenerateTexture(int width, int height, Func<int, int, float> getPixelData) {
			var data = new float[width * height];

			for (var y = 0; y < height; y++) {
				for (var x = 0; x < width; x++) {
					var index = x + y * width;
					data[index] = getPixelData.Invoke(x, y);
				}
			}

			var texture = new Texture2D(width, height, TextureFormat.RFloat, false) {
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Repeat,
				name = $"EmptyTexture2D_{width}x{height}"
			};

			texture.SetPixelData(data, mipLevel: 0);
			texture.Apply(updateMipmaps: false);
			return texture;
		}
	}
}