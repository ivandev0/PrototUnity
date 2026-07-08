using UnityEngine;

namespace PrototUnity.MarchingCubesMeshGenerator.RegularMesh { 
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
	public class RegularMeshGenerator : AbstractMeshGenerator {
		private MeshFilter meshFilter;
		private MeshCollider meshCollider;

		protected override void BeforePointGeneration() {
			meshFilter = GetComponent<MeshFilter>();
			meshCollider = GetComponent<MeshCollider>();
		}

		protected override void AfterMeshGeneration() {
			meshCollider.sharedMesh = mesh; 
			meshFilter.mesh = mesh;
			OnMeshChanged(mesh);
		}

		public override void Render() {
			// nothing
		}
	}
}