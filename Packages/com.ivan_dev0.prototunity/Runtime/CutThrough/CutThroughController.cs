using UnityEngine;

namespace PrototUnity.CutThrough {
	/// <summary>
	/// Feeds a material that uses the CutThrough shader sub graph: cuts a cylinder from the target
	/// towards the main camera when the target is hidden behind obstacles.
	/// </summary>
	public class CutThroughController : MonoBehaviour {
		[SerializeField] private Transform target;
		[Tooltip("Material with the CutThrough shader sub graph")]
		[SerializeField] private Material material;
		[Tooltip("Radius of the cut when it is fully faded in")]
		[SerializeField] private float radius = 2.5f;
		[Tooltip("Distance from the target towards the camera where the cut starts")]
		[SerializeField] private float nearOffset = 0.5f;
		[SerializeField] private LayerMask obstacleMask;
		[SerializeField] private float fadeSpeed = 4f;

		// Fade of the cut in [0, 1], scales the radius
		private float strength;
		private Vector3 axis;

		private const float obstacleCheckRadius = 0.3f;
		
		private static readonly int targetPositionID = Shader.PropertyToID("_CutThroughTargetPosition");
		private static readonly int axisID = Shader.PropertyToID("_CutThroughAxis");
		private static readonly int radiusID = Shader.PropertyToID("_CutThroughRadius");
		private static readonly int nearOffsetID = Shader.PropertyToID("_CutThroughNearOffset");

		private float CurrentRadius => radius * strength;

		private void OnDisable() {
			strength = 0;
			material.SetFloat(radiusID, CurrentRadius);
		}

		private void LateUpdate() {
			var mainCamera = UnityEngine.Camera.main;
			if (target == null || mainCamera == null) {
				strength = 0;
				material.SetFloat(radiusID, CurrentRadius);
				return;
			}

			var cameraPosition = mainCamera.transform.position;
			axis = mainCamera.orthographic
				? -mainCamera.transform.forward
				: (cameraPosition - target.position).normalized;

			var targetStrength = IsTargetHidden(cameraPosition) ? 1f : 0f;
			strength = Mathf.MoveTowards(strength, targetStrength, fadeSpeed * Time.deltaTime);

			material.SetVector(targetPositionID, target.position);
			material.SetVector(axisID, axis);
			material.SetFloat(radiusID, CurrentRadius);
			material.SetFloat(nearOffsetID, nearOffset);
		}

		private bool IsTargetHidden(Vector3 cameraPosition) {
			var distanceToCamera = Vector3.Dot(cameraPosition - target.position, axis);
			if (distanceToCamera <= nearOffset) return false;

			var origin = target.position + axis * distanceToCamera;
			return Physics.SphereCast(
				origin, obstacleCheckRadius, -axis, out _, distanceToCamera - nearOffset, obstacleMask, QueryTriggerInteraction.Ignore
			);
		}
	}
}
