using System.Collections.Generic;
using UnityEngine;

namespace PrototUnity.Utils {
	public class GameObjectsPool {
		private readonly GameObject prefab;
		private readonly List<GameObject> pool;

		public GameObjectsPool(GameObject prefab) {
			this.prefab = prefab;
			pool = new List<GameObject>();
		}

		public List<GameObject> Get(int amount) {
			while (pool.Count < amount) {
				pool.Add(Object.Instantiate(prefab));
			}

			return pool.GetRange(0, amount);
		}
		
		public void HideAll() {
			foreach (var gameObject in pool) {
				gameObject.SetActive(false);
			}
		}

		public void Clear() {
			foreach (var gameObject in pool) {
				Object.Destroy(gameObject);
			}

			pool.Clear();
		}
	}
}