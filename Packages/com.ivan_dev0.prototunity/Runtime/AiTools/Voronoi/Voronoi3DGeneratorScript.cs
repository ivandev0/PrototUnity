using UnityEngine;

namespace PrototUnity.AiTools.Voronoi {
	public class Voronoi3DGeneratorScript : MonoBehaviour {
		[Header("Generation")] [SerializeField]
		private int seed = 12345;

		[SerializeField] private Vector3Int cellCount = new(1, 1, 1);
		[SerializeField] private Vector3 cubeSize = new(10f, 10f, 10f);

		[Tooltip("If enabled, all cells are generated uniformly")] [SerializeField]
		private bool uniform = true;
		
		[Header("Appearance")]
		[Tooltip("Shrinks each cell around its centroid so adjacent cells don't touch.")]
		[Range(0f, 0.2f)]
		[SerializeField]
		private float cellShrink = 0.02f;

		[SerializeField] private Material faceMaterial;
		[SerializeField] private GameObject cellPrefab;

		[Tooltip("If true, each face gets its own material instance tinted by the cell color.")] [SerializeField]
		private bool tintByCell = true;

		[ContextMenu("Generate")]
		public void Generate() {
			new Voronoi3DGenerator(seed, cellCount, cubeSize, uniform, cellShrink, faceMaterial, cellPrefab, tintByCell, transform)
				.Generate();
		}

		[ContextMenu("Clear")]
		public void Clear() {
			for (var i = transform.childCount - 1; i >= 0; i--) {
				var child = transform.GetChild(i).gameObject;
				if (Application.isPlaying) Destroy(child);
				else DestroyImmediate(child);
			}
		}
	}
}