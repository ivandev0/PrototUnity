using System;
using PrototUnity.Input;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PrototUnity.Camera {
	[Serializable] public enum CameraViewMode { TopDown, FollowTarget, RotateAroundTarget };
	
	public class CameraRotationController: MonoBehaviour {
		[SerializeField] private InputManager inputSystem;
		[SerializeField] private CameraViewMode viewMode = CameraViewMode.RotateAroundTarget;
		[SerializeField] private float rotationSensitivity = 0.5f;
		[SerializeField] private bool rotateOnClick = false;
		
		public CameraViewMode ViewMode => viewMode;

		private CinemachineOrbitalFollow orbitalFollow;

		private void Setup() {
			switch (viewMode) {
				case CameraViewMode.TopDown:
					throw new NotImplementedException();
					break;
				case CameraViewMode.FollowTarget:
					orbitalFollow = gameObject.AddComponent<CinemachineOrbitalFollow>();
					orbitalFollow.TrackerSettings = new TrackerSettings {
						BindingMode = BindingMode.LockToTargetOnAssign,
						PositionDamping = new Vector3(0.1f, 0.1f, 0.1f),
					};
					orbitalFollow.TargetOffset = new Vector3(0, 2f, 0);
					orbitalFollow.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
					orbitalFollow.Radius = 3f;
					orbitalFollow.VerticalAxis.Range = new Vector2(17.5f, 17.5f);
					
					var hardLookAt = gameObject.AddComponent<CinemachineHardLookAt>();
					hardLookAt.LookAtOffset = new Vector3(0, 1.5f, 0);
					break;
				case CameraViewMode.RotateAroundTarget:
					orbitalFollow = gameObject.AddComponent<CinemachineOrbitalFollow>();
					orbitalFollow.TrackerSettings = new TrackerSettings {
						BindingMode = BindingMode.LockToTargetOnAssign,
						PositionDamping = new Vector3(0.1f, 0.1f, 0.1f),
					};
					orbitalFollow.TargetOffset = new Vector3(0, 2f, 0);
					orbitalFollow.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
					orbitalFollow.Radius = 3f;
					orbitalFollow.VerticalAxis.Range = new Vector2(17.5f, 17.5f);
					
					hardLookAt = gameObject.AddComponent<CinemachineHardLookAt>();
					hardLookAt.LookAtOffset = new Vector3(0, 1.5f, 0);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void Start() {
			Setup();
		}

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
			if (viewMode == CameraViewMode.TopDown) return;
			if (rotateOnClick && !Mouse.current.rightButton.isPressed) return;
			orbitalFollow.HorizontalAxis.Value += vector.x * rotationSensitivity;
		}
	}
}