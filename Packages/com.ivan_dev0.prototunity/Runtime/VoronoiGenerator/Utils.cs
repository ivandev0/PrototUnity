using UnityEngine;

namespace PrototUnity.VoronoiGenerator {
	public static class Utils {
		public static Texture3D CreateRandomColors(int seed, int width, int height, int depth) {
			Random.InitState(seed);
			var colors = new Color[width * height * depth];

			for (var z = 0; z < depth; z++) {
				for (var y = 0; y < height; y++) {
					for (var x = 0; x < width; x++) {
						var index = x + y * width + z * width * height;
						colors[index] = Color.HSVToRGB(Random.value, 0.45f, 0.95f);
					}
				}
			}

			var texture = new Texture3D(width, height, depth, TextureFormat.ARGB32, false) {
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Repeat,
				name = $"RandomTexture3D_{width}x{height}x{depth}"
			};

			texture.SetPixels(colors);
			texture.Apply();
			return texture;
		}
	}
}