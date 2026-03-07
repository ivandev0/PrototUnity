using UnityEngine;

namespace PrototUnity.Character {
	public class CharacterMovementStats : MonoBehaviour {
		[SerializeField] private float maxMovementSpeed;
		[SerializeField] private float maxRotationSpeed;
		
		public float MaxMovementSpeed {
			get => maxMovementSpeed;
			set => maxMovementSpeed = value;
		}

		public float MaxRotationSpeed => maxRotationSpeed;
	}
}