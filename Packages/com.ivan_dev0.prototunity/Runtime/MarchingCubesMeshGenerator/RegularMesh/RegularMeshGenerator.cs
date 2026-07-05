using UnityEngine;

namespace PrototUnity.MarchingCubesMeshGenerator.RegularMesh { 
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
	public class RegularMeshGenerator : AbstractMeshGenerator {
		private MeshFilter meshFilter;
		private MeshCollider meshCollider;

		public override void BeforePointGeneration() {
			meshFilter = GetComponent<MeshFilter>();
			meshCollider = GetComponent<MeshCollider>();
		}

		public override void AfterMeshGeneration() {
			meshCollider.sharedMesh = mesh; 
			meshFilter.mesh = mesh;
		}

		public override void Render() {
			// nothing
		}
	}
}