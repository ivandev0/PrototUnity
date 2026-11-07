using System;
using UnityEngine;
using UnityEngine.Events;

namespace PrototUnity.CutThrough {
	public class TargetHiddenNotifier : MonoBehaviour {
		[SerializeField] private Transform target;
		[Tooltip("Distance from the target towards the camera where the cut starts")]
		[SerializeField] private float nearOffset = 0.5f;
		[SerializeField] private LayerMask obstacleMask;

		public Transform Target => target;
		public float NearOffset => nearOffset;
		public Vector3 Axis { get; private set; }

		public event UnityAction<bool> TargetBecomeHiddenEvent = delegate { };
		
		private bool isTargetHidden;
		
		private const float obstacleCheckRadius = 0.3f;

		private void LateUpdate() {
			CalculateAxis();
			CheckTarget();
		}

		private void CalculateAxis() {
			var mainCamera = UnityEngine.Camera.main;
			if (Target == null || mainCamera == null) {
				return;
			}

			var cameraPosition = mainCamera.transform.position;
			Axis = mainCamera.orthographic
				? -mainCamera.transform.forward
				: (cameraPosition - Target.position).normalized;
		}

		private void CheckTarget() {
			var result = IsTargetHidden();
			if (result == isTargetHidden) return;
			
			isTargetHidden = result;
			TargetBecomeHiddenEvent.Invoke(result);
		}

		private bool IsTargetHidden() {
			var mainCamera = UnityEngine.Camera.main;
			if (mainCamera == null) return false;
			
			var distanceToCamera = Vector3.Dot(mainCamera.transform.position - Target.position, Axis);
			if (distanceToCamera <= NearOffset) return false;

			var origin = Target.position + Axis * distanceToCamera;
			return Physics.SphereCast(
				origin, obstacleCheckRadius, -Axis, out _, distanceToCamera - NearOffset, obstacleMask, QueryTriggerInteraction.Ignore
			);
		}
	}
}
