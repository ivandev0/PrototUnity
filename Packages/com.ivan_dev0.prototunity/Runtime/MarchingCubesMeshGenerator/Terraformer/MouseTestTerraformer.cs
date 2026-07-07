using UnityEngine;
using UnityEngine.InputSystem;

namespace PrototUnity.MarchingCubesMeshGenerator.Terraformer {
	public class MouseTestTerraformer : MonoBehaviour {
		[SerializeField] private AbstractMeshGenerator meshGenerator;
		[SerializeField] private LayerMask terrainMask;

		[SerializeField] private float terraformWeight = 1;
		[SerializeField] private float terraformRadius = 5;

		private UnityEngine.Camera mainCamera;
		private bool hasHit;
		private Vector3 hitPoint;

		void Start() {
			mainCamera = UnityEngine.Camera.main;
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

		private void Terraform(Vector3 terraformPoint) {
			hasHit = true;
			hitPoint = terraformPoint;

			if (Mouse.current.leftButton.isPressed) {
				meshGenerator.Terraform(hitPoint, terraformWeight, terraformRadius);
			} else if (Mouse.current.rightButton.isPressed) {
				meshGenerator.Terraform(hitPoint, -terraformWeight, terraformRadius);
			}
		}

		private void OnDrawGizmos() {
			if (hasHit) {
				Gizmos.color = Color.green;
				Gizmos.DrawSphere(hitPoint, 0.25f);
			}
		}
	}
}