using UnityEngine;

namespace PrototUnity.PointsGenerators {
	public abstract class AbstractTextureGenerator : MonoBehaviour {
		[SerializeField] private int numPointsPerAxis;
		public int NumPointsPerAxis => numPointsPerAxis;
		
		public abstract RenderTexture GenerateTexture();
		
		protected static RenderTexture CreateRenderCubeTexture(int size, string name = "texture3D") {
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