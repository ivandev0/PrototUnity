using PrototUnity.Input;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PrototUnity.Camera {
	public class CameraRotationController: MonoBehaviour {
		[SerializeField] private InputManager inputSystem;
		[SerializeField] private float rotationSensitivity = 0.5f;
		[SerializeField] private bool rotateOnClick = false;
		// [SerializeField] private bool allowMovement = false;
		
		private CinemachineOrbitalFollow orbitalFollow;
		private CinemachinePanTilt panTilt;

		private void OnEnable() {
			if (inputSystem == null) {
				inputSystem = ScriptableObject.CreateInstance<DefaultInputManager>();
			}
			inputSystem.RotateEvent += OnRotate;
		}

		private void OnDisable() {
			inputSystem.RotateEvent -= OnRotate;
		}

		private void OnRotate(Vector2 vector) {
			if (rotateOnClick && !Mouse.current.rightButton.isPressed) return;
			
			if (orbitalFollow == null && panTilt == null) {
				orbitalFollow = GetComponent<CinemachineOrbitalFollow>(); 
				panTilt = GetComponent<CinemachinePanTilt>();
			}

			if (orbitalFollow != null) { 
				orbitalFollow.HorizontalAxis.Value += vector.x * rotationSensitivity;
			} else if (panTilt != null) {
				panTilt.PanAxis.Value += vector.x * rotationSensitivity;
			}
		}
	}
}