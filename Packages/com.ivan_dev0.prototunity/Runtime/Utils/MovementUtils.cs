using System;
using System.Collections;
using UnityEngine;

namespace PrototUnity.Utils {
	public static class MovementUtils {
		public static IEnumerator ParabolicMovement(
			float y0, float y1, float heightToReach, float duration, Action<float> onChange = null, Action onEnd = null
		) {
			var time = 0f;

			while (time < duration) {

				var a = (-4 * heightToReach + 2 * y0 + 2 * y1) / (duration * duration);
				var b = (4 * heightToReach - 3 * y0 - y1) / duration;
				var c = y0;
				var heightDiff = a * time * time + b * time + c;
				var newY = heightDiff;

				onChange?.Invoke(newY);
				time += Time.deltaTime;
				yield return null;
			}
			
			onEnd?.Invoke();
		}
		
		public static IEnumerator ParabolicMovement(
			Vector3 v0, Vector3 v1, float heightToReach, float duration, Action<Vector3> onChange = null, Action onEnd = null
		) {
			var time = 0f;
			var middlePoint = (v0 + v1) / 2;
			var heightPoint = new Vector3(middlePoint.x, heightToReach, middlePoint.z);

			while (time < duration) {

				var a = (-4 * heightPoint + 2 * v0 + 2 * v1) / (duration * duration);
				var b = (4 * heightPoint - 3 * v0 - v1) / duration;
				var c = v0;
				var heightDiff = a * time * time + b * time + c;
				var newV = heightDiff;

				onChange?.Invoke(newV);
				time += Time.deltaTime;
				yield return null;
			}
			
			onEnd?.Invoke();
		}
		
		// https://en.wikipedia.org/wiki/Damping
		// lambda - decay rate, more value means faster decay
		// omega - frequency, more value means more "peaks" will appear
		public static IEnumerator DumpingMovement(
			float amplitude, float lambda, float omega, float duration, Action<float> onChange, Action onEnd = null
		) {
			var time = 0f;

			while (time < duration) {
				// see plot for best result https://www.desmos.com/calculator/gg3rhjqzr8
				var newY = amplitude * Mathf.Exp(- lambda * time) * Mathf.Sin(omega * 2 * Mathf.PI * time);
				onChange.Invoke(newY);
				time += Time.deltaTime;
				yield return null;
			}
			
			onEnd?.Invoke();
		}

		public enum SpiralRadiusChangeMode {
			Linear, Fibonacci, Square, Logarithmic
		}

		public static IEnumerator SpiralMovement(
			Vector3 start,
			Vector3 end,
			float angularVelocity = 1f,
			float duration = 1f,
			SpiralRadiusChangeMode radiusChangeMode = SpiralRadiusChangeMode.Linear,
			Action<Vector3> onChange = null,
			Action onEnd = null
		) {
			yield return SpiralMovement(
				start,
				() => end,
				angularVelocity, duration, radiusChangeMode, onChange, onEnd
			);
		}
		
		public static IEnumerator SpiralMovement(
			Vector3 start,
			Func<Vector3> endFunc,
			float angularVelocity = 1f,
			float duration = 1f,
			SpiralRadiusChangeMode radiusChangeMode = SpiralRadiusChangeMode.Linear,
			Action<Vector3> onChange = null,
			Action onEnd = null
		) {
			var end = endFunc();
			var time = 0f;
			var radiusStart = Vector3.Distance(new Vector3(start.x, 0, start.z), new Vector3(end.x, 0, end.z));
			var radius = radiusStart;

			var angleStart = Vector3.SignedAngle(new Vector3(start.x - end.x, 0, start.z - end.z), Vector3.right, Vector3.up) * Mathf.Deg2Rad;
			var angleRad = angleStart;

			var heightStart = start.y;
			var height = heightStart;
			
			while (time < duration) {
				var value = new Vector3(Mathf.Cos(angleRad) * radius + end.x, height, Mathf.Sin(angleRad) * radius + end.z);
				angleRad += angularVelocity * Time.deltaTime;
				height = Mathf.Lerp(heightStart, end.y, time / duration);
				end = endFunc();

				switch (radiusChangeMode) {
					case SpiralRadiusChangeMode.Linear: 
						radius = (1 - time / duration) * radiusStart;
						break;
					case SpiralRadiusChangeMode.Fibonacci: 
						// https://en.wikipedia.org/wiki/Golden_spiral
						var startFibValue = GoldenSpiral(0);
						var currentFibValue = GoldenSpiral(1 - time / duration);
						var endFibValue = GoldenSpiral(1);
						radius = (currentFibValue - startFibValue) / (endFibValue - startFibValue) * radiusStart;
						break;
					case SpiralRadiusChangeMode.Square:
						var currentSqrtValue = Mathf.Sqrt(1 - time / duration);
						radius = currentSqrtValue * radiusStart;
						break;
					case SpiralRadiusChangeMode.Logarithmic:
						// https://en.wikipedia.org/wiki/Logarithmic_spiral
						var a = 1;
						var k = 1;
						var startLogValue = a;
						var currentLogValue = a * Mathf.Exp(k * (1 - time / duration));
						var endLogValue = a * Mathf.Exp(k);
						radius = (currentLogValue - startLogValue) / (endLogValue - startLogValue) * radiusStart;
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(radiusChangeMode), radiusChangeMode, null);
				}
				
				onChange?.Invoke(value);
				time += Time.deltaTime;
				yield return null;
			}
			
			onEnd?.Invoke();
			yield break;

			float GoldenSpiral(float angle) {
				return 1.61f * Mathf.Exp(2 * angle / Mathf.PI);
			}
		}
	}
}
