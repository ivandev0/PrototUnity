using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace PrototUnity.Progress {
	public class Progressable: MonoBehaviour {
		[SerializeField] private float initial;
		[SerializeField] private float total;
		[SerializeField] private bool destroyOnDeath = true;
	
		public float Total { get; private set; }
		public float Current { get; private set; }
	
		public event UnityAction<Progressable, EventArgs> ProgressChangedEvent = delegate {  };
		public event UnityAction<Progressable> ObjectDestroyedEvent = delegate {  };

		public struct EventArgs {
			public float delta;
		
			public bool IsFirstHit(Progressable progress) {
				return delta < 0 && -delta + progress.Current >= progress.Total;
			}
			
			public bool IsRestoredToFull(Progressable progress) {
				return delta > 0 && delta + progress.Current >= progress.Total;
			}
		}
	
		private void Awake() {
			StartCoroutine(Init());
		}

		private IEnumerator Init() {
			Total = total;
			Current = initial;
			yield return null;
			ProgressChangedEvent.Invoke(this, new EventArgs { delta = 0 });
		}

		public void Decrease(float power) {
			ChangeDurability(-power);
		}

		public void Increase(float power) {
			ChangeDurability(power);
		}

		private void ChangeDurability(float delta) {
			Current = Math.Clamp(Current + delta, 0, Total);
			ProgressChangedEvent.Invoke(this, new EventArgs { delta = delta });
		
			if (Current <= 0) {
				ObjectDestroyedEvent.Invoke(this);
				if (destroyOnDeath) Destroy(gameObject);
			}
		}

		public void SetNewTotal(float newTotal) {
			Total = newTotal;
			ProgressChangedEvent.Invoke(this, new EventArgs { delta = 0 });
		}
	}
}
