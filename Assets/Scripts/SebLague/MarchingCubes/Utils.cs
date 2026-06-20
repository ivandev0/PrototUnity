using UnityEngine;

namespace SebLague.MarchingCubes {
	public static class Utils {
		public static RenderTexture CreateRenderCubeTexture(int size, string name = "texture3D") {
			const int numBitsInDepthBuffer = 0;
			var texture = new RenderTexture(size, size, numBitsInDepthBuffer) {
				volumeDepth = size,
				dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
				enableRandomWrite = true,
				graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat,
				wrapMode = TextureWrapMode.Repeat,
				filterMode = FilterMode.Bilinear,
				name = name
			};

			texture.Create();

			return texture;
		}
	}
}