using UnityEngine;
using UnityEngine.InputSystem;

namespace SebLague.MarchingCubes {
	public class Terraformer : MonoBehaviour {
		public LayerMask terrainMask;

		public float terraformRadius = 5;

		Camera mainCamera;
		bool hasHit;
		Vector3 hitPoint;

		void Start() {
			mainCamera = Camera.main;
		}

		void Update() {
			hasHit = false;

			if (mainCamera == null) return;
			var cameraRay = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
			var results = new RaycastHit[5];
			Physics.RaycastNonAlloc(cameraRay, results, maxDistance: 100f, layerMask: terrainMask);
			foreach (var hit in results) {
				if (hit.collider == null) return;
				Terraform(hit.point);
				break;
			}
		}

		void Terraform(Vector3 terraformPoint) {
			hasHit = true;
			hitPoint = terraformPoint;

			// TODO
		}

		void OnDrawGizmos() {
			if (hasHit) {
				Gizmos.color = Color.green;
				Gizmos.DrawSphere(hitPoint, 0.25f);
			}
		}
	}
}