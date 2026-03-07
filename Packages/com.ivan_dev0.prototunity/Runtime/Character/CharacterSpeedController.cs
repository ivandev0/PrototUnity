using System;
using PrototUnity.Input;
using UnityEngine;

namespace PrototUnity.Character {
	public class CharacterSpeedController: MonoBehaviour {
		[SerializeField] private InputManager inputSystem;
		[SerializeField] CharacterMovementStats movementStats;

		[SerializeField] private float sprintSpeed;
		[SerializeField] private float runSpeed;
		[SerializeField] private float walkSpeed;
		
		private enum MovementState { Walk, Run, Sprint }
		private  MovementState movementState;

		private void OnEnable() {
			if (inputSystem == null) {
				inputSystem = ScriptableObject.CreateInstance<DefaultInputManager>();
			}
			
			inputSystem.WalkRunSwitchEvent += OnWalkRunSwitchEvent;
			inputSystem.SprintStartEvent += OnSprintStartEvent;
			inputSystem.SprintEndEvent += OnSprintEndEvent;
		}

		private void OnDisable() {
			inputSystem.WalkRunSwitchEvent -= OnWalkRunSwitchEvent;
			inputSystem.SprintStartEvent -= OnSprintStartEvent;
			inputSystem.SprintEndEvent -= OnSprintEndEvent;
		}

		private void OnWalkRunSwitchEvent() {
			if (movementState != MovementState.Walk) {
				movementState = MovementState.Walk;
				movementStats.MaxMovementSpeed = walkSpeed;
			} else {
				movementState = MovementState.Run;
				movementStats.MaxMovementSpeed = runSpeed;
			}
		}
		
		private void OnSprintStartEvent() {
			movementState = MovementState.Sprint; 
			movementStats.MaxMovementSpeed = sprintSpeed;
		}

		private void OnSprintEndEvent() {
			movementState = MovementState.Run; 
			movementStats.MaxMovementSpeed = runSpeed;
		}
	}
}