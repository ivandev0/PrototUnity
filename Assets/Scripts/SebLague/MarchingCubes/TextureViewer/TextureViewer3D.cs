using UnityEngine;

namespace SebLague.MarchingCubes.TextureViewer {
	public class TextureViewer3D : MonoBehaviour {
		[SerializeField] private int numPointsPerAxis;
		[SerializeField] private float radius;
		
		[SerializeField] 
		[Range(0, 1)] private float sliceDepth;
		[SerializeField] private RenderSphere textureRenderer;

		private Material material;
		private RenderTexture renderTexture;
		
		private static readonly int depth = Shader.PropertyToID("sliceDepth");
		private static readonly int displayTexture = Shader.PropertyToID("DisplayTexture");

		void Start() {
			material = GetComponentInChildren<MeshRenderer>().material;
			renderTexture = textureRenderer.Generate(numPointsPerAxis, radius);
		}

		public void Display() {
		}

		void Update() {
			material.SetFloat(depth, sliceDepth);
			material.SetTexture(displayTexture, renderTexture);
		}
	}
}