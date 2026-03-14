using PrototUnity.Utils;
using UnityEngine;

public class SpiralMovementExample: MonoBehaviour {
	[SerializeField] private Vector3 end = Vector3.zero;
	[SerializeField] private float angularVelocity = 1f;
	[SerializeField] private float duration = 10f;
	[SerializeField] private MovementUtils.SpiralRadiusChangeMode radiusChangeMode = MovementUtils.SpiralRadiusChangeMode.Linear;
		
	void Start() {
		StartCoroutine(
			MovementUtils.SpiralMovement(
				transform.position, end,
				angularVelocity,
				duration, 
				radiusChangeMode,
				vector3 => transform.position = vector3,
				() => {}
			)
		);
	}
}