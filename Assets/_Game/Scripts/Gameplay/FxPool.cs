using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 轻量级 VFX 对象池（MVP）。
    /// </summary>
    public class FxPool : MonoBehaviour
    {
        [SerializeField] private float fallbackLifetime = 2f;

        private readonly Dictionary<GameObject, Stack<GameObject>> availableByPrefab = new Dictionary<GameObject, Stack<GameObject>>(32);
        private readonly Dictionary<int, RuntimeHandle> activeHandles = new Dictionary<int, RuntimeHandle>(128);

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                return null;
            }

            if (!availableByPrefab.TryGetValue(prefab, out var stack))
            {
                stack = new Stack<GameObject>(8);
                availableByPrefab[prefab] = stack;
            }

            GameObject instance = null;
            while (stack.Count > 0 && instance == null)
            {
                instance = stack.Pop();
            }

            if (instance == null)
            {
                instance = Instantiate(prefab, position, rotation, parent);
            }
            else
            {
                instance.transform.SetParent(parent, false);
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.transform.localScale = prefab.transform.localScale;
                instance.SetActive(true);
            }

            var id = instance.GetInstanceID();
            var version = 1U;
            if (activeHandles.TryGetValue(id, out var existing))
            {
                version = existing.Version + 1U;
            }

            activeHandles[id] = new RuntimeHandle(prefab, version);
            RestartParticleSystems(instance);
            return instance;
        }

        public GameObject Play(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            float maxLifetime = 0f)
        {
            var instance = Spawn(prefab, position, rotation, parent);
            if (instance == null)
            {
                return null;
            }

            var id = instance.GetInstanceID();
            if (!activeHandles.TryGetValue(id, out var handle))
            {
                return instance;
            }

            var lifetime = maxLifetime > 0f ? maxLifetime : ResolveLifetime(instance);
            StartCoroutine(ReleaseAfter(instance, handle.Prefab, handle.Version, lifetime));
            return instance;
        }

        public void Release(GameObject prefab, GameObject instance)
        {
            if (prefab == null || instance == null)
            {
                return;
            }

            if (!availableByPrefab.TryGetValue(prefab, out var stack))
            {
                stack = new Stack<GameObject>(8);
                availableByPrefab[prefab] = stack;
            }

            var id = instance.GetInstanceID();
            if (activeHandles.TryGetValue(id, out var handle))
            {
                activeHandles[id] = new RuntimeHandle(handle.Prefab, handle.Version + 1U);
            }

            instance.transform.SetParent(transform, false);
            instance.SetActive(false);
            stack.Push(instance);
        }

        private IEnumerator ReleaseAfter(GameObject instance, GameObject prefab, uint version, float seconds)
        {
            if (instance == null || prefab == null)
            {
                yield break;
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, seconds));
            if (instance == null)
            {
                yield break;
            }

            var id = instance.GetInstanceID();
            if (!activeHandles.TryGetValue(id, out var handle) || handle.Version != version)
            {
                yield break;
            }

            Release(prefab, instance);
        }

        private float ResolveLifetime(GameObject instance)
        {
            var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            var max = 0f;
            for (int i = 0; i < particleSystems.Length; i++)
            {
                var ps = particleSystems[i];
                if (ps == null)
                {
                    continue;
                }

                var main = ps.main;
                var duration = Mathf.Max(0f, main.duration + main.startLifetime.constantMax);
                if (duration > max)
                {
                    max = duration;
                }
            }

            return max > 0f ? max : Mathf.Max(0.1f, fallbackLifetime);
        }

        private static void RestartParticleSystems(GameObject instance)
        {
            var particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                var ps = particleSystems[i];
                if (ps == null)
                {
                    continue;
                }

                ps.Clear(true);
                ps.Play(true);
            }
        }

        private readonly struct RuntimeHandle
        {
            public readonly GameObject Prefab;
            public readonly uint Version;

            public RuntimeHandle(GameObject prefab, uint version)
            {
                Prefab = prefab;
                Version = version;
            }
        }
    }
}
