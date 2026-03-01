using System.Collections;
using System.Collections.Generic;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// 轻量级 3D 音源池（MVP）。
    /// </summary>
    public class AudioEmitterPool : MonoBehaviour
    {
        private readonly Stack<AudioSource> available = new Stack<AudioSource>(16);
        private readonly Dictionary<int, uint> versions = new Dictionary<int, uint>(64);

        public AudioSource Play(
            AudioClip clip,
            Vector3 position,
            AudioBusType bus,
            float volume,
            float pitch,
            float spatialBlend,
            Transform parent = null)
        {
            if (clip == null)
            {
                return null;
            }

            var emitter = GetOrCreateEmitter();
            emitter.transform.SetParent(parent, false);
            emitter.transform.position = position;
            emitter.clip = clip;
            emitter.volume = Mathf.Clamp01(volume);
            emitter.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            emitter.spatialBlend = Mathf.Clamp01(spatialBlend);
            emitter.loop = false;
            emitter.gameObject.name = $"AudioEmitter_{bus}";
            emitter.Play();

            var id = emitter.GetInstanceID();
            var version = 1U;
            if (versions.TryGetValue(id, out var existing))
            {
                version = existing + 1U;
            }

            versions[id] = version;
            var duration = clip.length / Mathf.Max(0.1f, Mathf.Abs(emitter.pitch));
            StartCoroutine(ReleaseAfter(emitter, version, duration));
            return emitter;
        }

        public void Release(AudioSource emitter)
        {
            if (emitter == null)
            {
                return;
            }

            var id = emitter.GetInstanceID();
            if (versions.TryGetValue(id, out var version))
            {
                versions[id] = version + 1U;
            }
            else
            {
                versions[id] = 1U;
            }

            emitter.Stop();
            emitter.clip = null;
            emitter.transform.SetParent(transform, false);
            emitter.gameObject.SetActive(false);
            available.Push(emitter);
        }

        private IEnumerator ReleaseAfter(AudioSource emitter, uint version, float seconds)
        {
            if (emitter == null)
            {
                yield break;
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, seconds));
            if (emitter == null)
            {
                yield break;
            }

            var id = emitter.GetInstanceID();
            if (!versions.TryGetValue(id, out var currentVersion) || currentVersion != version)
            {
                yield break;
            }

            Release(emitter);
        }

        private AudioSource GetOrCreateEmitter()
        {
            AudioSource source = null;
            while (available.Count > 0 && source == null)
            {
                source = available.Pop();
            }

            if (source == null)
            {
                var go = new GameObject("AudioEmitter");
                go.transform.SetParent(transform, false);
                source = go.AddComponent<AudioSource>();
            }
            else
            {
                source.gameObject.SetActive(true);
            }

            return source;
        }
    }
}
