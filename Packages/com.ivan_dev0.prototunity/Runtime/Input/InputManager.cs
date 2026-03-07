using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace PrototUnity.Input {
	public abstract class InputManager: ScriptableObject { 
		// Assign delegate{} to events to initialise them with an empty delegate
		// so we can skip the null check when we use them
	
		public event UnityAction<Vector2> MoveEvent = delegate { };
		public event UnityAction<Vector2> RotateEvent = delegate { };
		public event UnityAction JumpStartEvent = delegate { };
		public event UnityAction JumpEndEvent = delegate { };
		public event UnityAction WalkRunSwitchEvent = delegate { };
		public event UnityAction SprintStartEvent = delegate { };
		public event UnityAction SprintEndEvent = delegate { };
		
		protected void CallMoveEvent(Vector2 movement) {
			MoveEvent.Invoke(movement);
		}
		
		protected void CallRotateEvent(Vector2 movement) {
			RotateEvent.Invoke(movement);
		}
		
		protected void CallJumpStartEvent() {
			JumpStartEvent.Invoke();
		}
		
		protected void CallJumpEndEvent() {
			JumpEndEvent.Invoke();
		}

		protected void CallWalkRunSwitchEvent() {
			WalkRunSwitchEvent.Invoke();
		}
		
		protected void CallSprintStartEvent() {
			SprintStartEvent.Invoke();
		}
		
		protected void CallSprintEndEvent() {
			SprintEndEvent.Invoke();
		}
	}
	
	public class DefaultInputManager : InputManager {
		private void OnEnable() {
			InitializeMoveAction();
			InitializeOtherMovementActions();
			InitializeLookAction();
			InitializeJumpAction();
		}

		private void InitializeMoveAction() {
			var moveAction = new InputAction("move", binding: "<Gamepad>/rightStick");
			moveAction.AddCompositeBinding("Dpad")
				.With("Up", "<Keyboard>/w")
				.With("Down", "<Keyboard>/s")
				.With("Left", "<Keyboard>/a")
				.With("Right", "<Keyboard>/d");
			moveAction.performed += ctx => CallMoveEvent(ctx.ReadValue<Vector2>());
			moveAction.canceled += ctx => CallMoveEvent(ctx.ReadValue<Vector2>());
			moveAction.Enable();
		}

		private void InitializeLookAction() {
			var lookAction = new InputAction("look", binding: "<Gamepad>/leftStick");
			lookAction.AddBinding("<Mouse>/delta");
			lookAction.performed += ctx => CallRotateEvent(ctx.ReadValue<Vector2>());
			lookAction.canceled += ctx => CallRotateEvent(ctx.ReadValue<Vector2>());
			lookAction.Enable();
		}

		private void InitializeJumpAction() {
			var jumpAction = new InputAction("jump", binding: "<Keyboard>/space");
			jumpAction.performed += _ => CallJumpStartEvent();
			jumpAction.canceled += _ => CallJumpEndEvent();
			jumpAction.Enable();
		}

		private void InitializeOtherMovementActions() {
			var walkRunSwitchAction = new InputAction("walkRunSwitch", binding: "<Keyboard>/backquote");
			walkRunSwitchAction.performed += _ => CallWalkRunSwitchEvent();
			walkRunSwitchAction.Enable();

			var sprintAction = new InputAction("sprint", binding: "<Keyboard>/Shift");
			sprintAction.performed += _ => CallSprintStartEvent();
			sprintAction.canceled += _ => CallSprintEndEvent();
			sprintAction.Enable();
		}
	}
}