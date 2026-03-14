using System;
using System.Collections;
using System.Collections.Generic;
using PrototUnity.Utils;
using UnityEngine;

public class SpiralMovementComparison: MonoBehaviour {
	[SerializeField] private Vector3 end = Vector3.zero;
	[SerializeField] private float angularVelocity = 1f;
	[SerializeField] private float duration = 10f;
		
	private Dictionary<MovementUtils.SpiralRadiusChangeMode, List<Vector3>> dictionary = new();
	
	void Start() {
		StartCoroutine( Movement(transform.position));
	}

	private IEnumerator Movement(Vector3 start) {
		var frequency = 0.1f;
		var time = Time.time;
		yield return SpiralMovement(MovementUtils.SpiralRadiusChangeMode.Linear);
		yield return SpiralMovement(MovementUtils.SpiralRadiusChangeMode.Square);
		yield return SpiralMovement(MovementUtils.SpiralRadiusChangeMode.Fibonacci);
		yield return SpiralMovement(MovementUtils.SpiralRadiusChangeMode.Logarithmic);
		
		yield break;
		
		IEnumerator SpiralMovement(MovementUtils.SpiralRadiusChangeMode mode) {
			dictionary.Add(mode, new List<Vector3> { start });
			return MovementUtils.SpiralMovement(
				start, end,
				angularVelocity,
				duration,
				mode,
				vector3 => {
					transform.position = vector3;
					if (Time.time - time > frequency) {
						time = Time.time;
						dictionary[mode].Add(vector3);
					}
				},
				() => { }
			);
		}
	}

	private void OnDrawGizmos() {
		var colors = new Dictionary<MovementUtils.SpiralRadiusChangeMode, Color>() {
			{ MovementUtils.SpiralRadiusChangeMode.Linear, Color.red }, 
			{ MovementUtils.SpiralRadiusChangeMode.Square, Color.greenYellow }, 
			{ MovementUtils.SpiralRadiusChangeMode.Fibonacci, Color.gold }, 
			{ MovementUtils.SpiralRadiusChangeMode.Logarithmic, Color.gray }, 
		};
		
		foreach (var (mode, points) in dictionary) {
			Gizmos.color = colors[mode];
			foreach (var point in points) {
				Gizmos.DrawSphere(point, 0.3f);
			}
		}
	}
}