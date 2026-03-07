using System;
using PrototUnity.Input;
using UnityEngine;

namespace PrototUnity.Character {
	public class CharacterSpeedController: MonoBehaviour {
		[SerializeField] private InputManager inputSystem;
		[SerializeField] CharacterMovementStats movementStats;

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
			UnityEngine.Debug.Log("walk");
		}
		
		private void OnSprintStartEvent() {
			UnityEngine.Debug.Log("start");
		}

		private void OnSprintEndEvent() {
			UnityEngine.Debug.Log("end");
		}
	}
}