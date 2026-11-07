using UnityEngine;

namespace PrototUnity.CutThrough {
	/// <summary>
	/// Feeds a material that uses the CutThrough shader sub graph: cuts a cylinder from the target
	/// towards the main camera when the target is hidden behind obstacles.
	/// </summary>
	public class CutThroughController : MonoBehaviour {
		[SerializeField] private TargetHiddenNotifier notifier;
		[Tooltip("Material with the CutThrough shader sub graph")]
		[SerializeField] private Material material;
		[Tooltip("Radius of the cut when it is fully faded in")]
		[SerializeField] private float radius = 2.5f;
		[SerializeField] private float edgeSoftness = 0.75f;
		[SerializeField] private float fadeSpeed = 4f;

		// Fade of the cut in [0, 1], scales the radius
		private float strength;
		private bool isTargetHidden;
		
		private static readonly int targetPositionID = Shader.PropertyToID("_CutThroughTargetPosition");
		private static readonly int axisID = Shader.PropertyToID("_CutThroughAxis");
		private static readonly int radiusID = Shader.PropertyToID("_CutThroughRadius");
		private static readonly int nearOffsetID = Shader.PropertyToID("_CutThroughNearOffset");
		private static readonly int edgeSoftnessID = Shader.PropertyToID("_CutThroughEdgeSoftness");

		private float CurrentRadius => radius * strength;

		private void OnEnable() {
			notifier.TargetBecomeHiddenEvent += OnTargetChangeVisibility;
		}

		private void OnDisable() {
			notifier.TargetBecomeHiddenEvent -= OnTargetChangeVisibility;
			
			strength = 0;
			material.SetFloat(radiusID, CurrentRadius);
		}
		
		private void OnTargetChangeVisibility(bool hidden) {
			isTargetHidden = hidden;
		}

		private void LateUpdate() {
			var mainCamera = UnityEngine.Camera.main;
			if (mainCamera == null) {
				strength = 0;
				material.SetFloat(radiusID, CurrentRadius);
				return;
			}

			var targetStrength = isTargetHidden ? 1f : 0f;
			strength = Mathf.MoveTowards(strength, targetStrength, fadeSpeed * Time.deltaTime);

			material.SetVector(targetPositionID, notifier.Target.position);
			material.SetVector(axisID, notifier.Axis);
			material.SetFloat(radiusID, CurrentRadius);
			material.SetFloat(nearOffsetID, notifier.NearOffset);
			material.SetFloat(edgeSoftnessID, edgeSoftness);
		}
	}
}
