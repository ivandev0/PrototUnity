using PrototUnity.Camera;
using PrototUnity.Input;
using UnityEngine;

namespace PrototUnity.Character {
	public class CharacterMovement: MonoBehaviour {
		[SerializeField] private InputManager inputSystem;
		[SerializeField] private CharacterController characterController;
		[SerializeField] private CameraRotationController cameraRotationController;

		[SerializeField] private CharacterMovementStats movementStats;
		[SerializeField] private float gravityAcceleration = -9.81f;
		
		private Vector3 moveDirection = Vector3.zero;
		private float movementInputAmount = 0;
		private Vector3 gravityVelocity = Vector3.zero;
		
		private UnityEngine.Camera mainCamera;

		private void Setup() {
			mainCamera = UnityEngine.Camera.main;
			if (inputSystem == null) {
				inputSystem = ScriptableObject.CreateInstance<DefaultInputManager>();
			}
		}
		
		private void OnEnable() {
			Setup();
			inputSystem.MoveEvent += OnMove;
		}

		private void OnDisable() {
			inputSystem.MoveEvent -= OnMove;
		}

		private void OnMove(Vector2 vector) {
			var correctedX = vector.x * mainCamera.transform.right;
			var correctedZ = vector.y * mainCamera.transform.forward;

			var combinedInput = correctedZ + correctedX;
			// normalize so diagonal movement isn't twice as fast
			moveDirection = new Vector3(combinedInput.normalized.x, 0, combinedInput.normalized.z);

			// make sure the input doesn't go negative or above 1;
			var inputMagnitude = Mathf.Abs(vector.y) + Mathf.Abs(vector.x);
			movementInputAmount = Mathf.Clamp01(inputMagnitude);
		}

		private void FixedUpdate() {
			Rotate();
			Move();
		}

		private void Move() {
			var moveAmount = moveDirection * movementStats.MaxMovementSpeed * movementInputAmount;
			
			if (characterController.isGrounded) {
				gravityVelocity = Vector3.up * gravityAcceleration;
			} else {
				gravityVelocity += Vector3.up * gravityAcceleration;
			}

			characterController.Move((moveAmount + gravityVelocity) * Time.fixedDeltaTime);
		}

		private void Rotate() {
			if (cameraRotationController.ViewMode == CameraViewMode.RotateAroundTarget) {
				RotateToCamera();
			} else {
				RotateToMoveDirection();
			}
		}

		private void RotateToMoveDirection() {
			if (moveDirection == Vector3.zero) return;
			var rot = Quaternion.LookRotation(moveDirection);
			var targetRotation = Quaternion.Slerp(transform.rotation, rot, Time.fixedDeltaTime * movementInputAmount * movementStats.MaxRotationSpeed);
			transform.rotation = targetRotation;
		}

		private void RotateToCamera() {
			if (moveDirection == Vector3.zero) return;
			transform.forward = new Vector3(mainCamera.transform.forward.x, transform.forward.y, mainCamera.transform.forward.z);
		}
	}
}