using System;
using PrototUnity.Input;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PrototUnity.Camera {
	[Serializable] public enum CameraViewMode { TopDown, ThirdPerson, FirstPerson };
	
	public class CameraSetup: MonoBehaviour {
		[SerializeField] private CameraViewMode viewMode = CameraViewMode.ThirdPerson;
		// [SerializeField] private bool followsTarget = true;

		private void Setup() {
			switch (viewMode) {
				case CameraViewMode.TopDown:
					throw new NotImplementedException();
					
				case CameraViewMode.FirstPerson:
					var follow = gameObject.AddComponent<CinemachineFollow>();
					follow.TrackerSettings = new TrackerSettings() {
						BindingMode = BindingMode.LockToTarget,
						AngularDampingMode = AngularDampingMode.Euler,
						RotationDamping = Vector3.zero,
						PositionDamping = Vector3.zero,
					};
					follow.FollowOffset = new Vector3(0, 1.5f, 0.1f);
					
					var panTilt = gameObject.AddComponent<CinemachinePanTilt>();
					panTilt.ReferenceFrame = CinemachinePanTilt.ReferenceFrames.ParentObject;
					panTilt.RecenterTarget = CinemachinePanTilt.RecenterTargetModes.AxisCenter;
					break;
				case CameraViewMode.ThirdPerson:
					var orbitalFollow = gameObject.AddComponent<CinemachineOrbitalFollow>();
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
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void Start() {
			Setup();
		}
	}
}