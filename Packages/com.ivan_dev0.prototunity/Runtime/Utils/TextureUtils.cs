using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace PrototUnity.Utils {
	public static class TextureUtils {
		// Source: https://discussions.unity.com/t/merging-multiple-textures-sprites-into-one-texture/721574
		public static Sprite Combine(Sprite[] sprites) {
			if (sprites.Length == 0) {
				Debug.Log("Not enough sprites to combine");
				return null;
			}

			var width = (int) sprites[0].rect.width;
			var height = (int) sprites[0].rect.height;

			var targetTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
			// targetTexture.filterMode = FilterMode.Point;
			var targetPixels = targetTexture.GetPixels();
			for (var i = 0; i < targetPixels.Length; i++) targetPixels[i] = Color.clear; // default pixels are not set

			foreach (var sprite in sprites) {
				if ((int) sprite.rect.width != width || (int) sprite.rect.height != height) {
					Debug.LogError("Cannot combine textures of different sizes");
					return null;
				}
				
				var sourcePixels = ExtractTexture(sprite).GetPixels();
				for (var index = 0; index < sourcePixels.Length; index++) {
					var source = sourcePixels[index];
					if (source.a > 0) {
						targetPixels[index] = source;
					}
				}
			}

			targetTexture.SetPixels(targetPixels);
			targetTexture.Apply(false, true); // read/write is disabled in 2nd param to free up memory
			return Sprite.Create(
				targetTexture,
				new Rect(new Vector2(), new Vector2(width, height)),
				new Vector2(),
				1,
				0,
				SpriteMeshType.FullRect
			);
		}

		private static Texture2D ExtractTexture(Sprite sprite) {
			var croppedTexture = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height);
			var pixels = sprite.texture.GetPixels(
				(int)sprite.rect.x,
				(int)sprite.rect.y,
				(int)sprite.rect.width,
				(int)sprite.rect.height
			);
			croppedTexture.SetPixels(pixels);
			croppedTexture.Apply();
			return croppedTexture;
		}
		
		public static RenderTexture CreateClearTexture(int width, int height, int depth, GraphicsFormat format, string name = "texture3D") {
			const int numBitsInDepthBuffer = 0;
			var texture = new RenderTexture(width, height, numBitsInDepthBuffer, format) {
				volumeDepth = depth,
				dimension = TextureDimension.Tex3D,
				enableRandomWrite = true,
				name = name
			};
			texture.Create();

			// Clear the texture
			var commandBuffer = new CommandBuffer { name = "Clear dug texture" };
			commandBuffer.SetRenderTarget(texture, 0, CubemapFace.Unknown, -1);
			commandBuffer.ClearRenderTarget(false, true, Color.clear);
			Graphics.ExecuteCommandBuffer(commandBuffer);
			commandBuffer.Release();

			return texture;
		}
	}
}