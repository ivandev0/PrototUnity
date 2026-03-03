using System;
using UnityEngine;

namespace PrototUnity.Character {
	public class CharacterAnimation : MonoBehaviour {
		[SerializeField] private Animator animator;
		[SerializeField] private CharacterController characterController;
		
		private static readonly int velocityX = Animator.StringToHash("Velocity X");
		private static readonly int velocityZ = Animator.StringToHash("Velocity Z");

		private void Update() {
			// Todo normalize velocity
			var relativeVelocity = transform.InverseTransformVector(characterController.velocity);
			animator.SetFloat(velocityX, relativeVelocity.x);
			animator.SetFloat(velocityZ, relativeVelocity.z);
		}
	}
}