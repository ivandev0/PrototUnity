using UnityEngine;

namespace PrototUnity.PointsGenerators.TextureViewer {
	public class TextureViewer3D : MonoBehaviour {
		[SerializeField] 
		[Range(0, 1)] private float sliceDepth;
		[SerializeField] private AbstractTextureGenerator textureRenderer;
		[SerializeField] private Shader textureShader;

		private Material material;
		private RenderTexture renderTexture;
		
		private static readonly int depth = Shader.PropertyToID("sliceDepth");
		private static readonly int displayTexture = Shader.PropertyToID("DisplayTexture");

		void Start() {
			material = new Material(textureShader);
			GetComponent<MeshRenderer>().material = material;
			renderTexture = textureRenderer.GenerateTexture();
		}

		void Update() {
			material.SetFloat(depth, sliceDepth);
			material.SetTexture(displayTexture, renderTexture);
		}
	}
}