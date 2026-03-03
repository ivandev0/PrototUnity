using System;
using PrototUnity.Input;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PrototUnity.Camera {
	[Serializable] internal enum CameraRotationMode { Disabled, RotateAroundTarget };
	
	public class CameraRotationController: MonoBehaviour {
		[SerializeField] private InputManager inputSystem;
		[SerializeField] private CameraRotationMode rotationMode = CameraRotationMode.RotateAroundTarget;
		[SerializeField] private float rotationSensitivity = 0.5f;
		[SerializeField] private bool rotateOnClick = false;
		
		private CinemachineCamera cinemachineCamera;
		private CinemachineOrbitalFollow orbitalFollow;

		private void Setup() {
			if (inputSystem == null) {
				inputSystem = ScriptableObject.CreateInstance<DefaultInputManager>();
			}

			cinemachineCamera = GetComponent<CinemachineCamera>();
			orbitalFollow = cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();
		}

		private void OnEnable() {
			Setup();
			inputSystem.RotateEvent += OnRotate;
		}

		private void OnDisable() {
			inputSystem.RotateEvent -= OnRotate;
		}

		private void OnRotate(Vector2 vector) {
			if (rotationMode == CameraRotationMode.Disabled) return;
			if (rotateOnClick && !Mouse.current.rightButton.isPressed) return;
			orbitalFollow.HorizontalAxis.Value += vector.x * rotationSensitivity;
		}
	}
}