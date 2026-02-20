using System;
using System.Collections;
using System.Collections.Generic;
using CombatSystem.Core;
using CombatSystem.Data;
using UnityEngine;

namespace CombatSystem.Gameplay
{
    /// <summary>
    /// Boss 技能节奏控制器：按序列进行 telegraph + 施法。
    /// </summary>
    public class BossSkillScheduler : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private SkillUserComponent skillUser;
        [SerializeField] private Transform explicitTarget;
        [SerializeField] private string targetTag = "Player";
        [SerializeField] private Transform telegraphRoot;

        [Header("节奏")]
        [SerializeField] private List<BossSkillCycleEntry> skillCycle = new List<BossSkillCycleEntry>();
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float initialDelay = 1f;
        [SerializeField] private float retryInterval = 0.15f;

        [Header("Telegraph")]
        [SerializeField] private bool showGroundTelegraph = true;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.25f, 0.2f, 0.4f);
        [SerializeField] private float fallbackTelegraphRadius = 2f;
        [SerializeField] private float telegraphHeight = 0.02f;

        private Coroutine loopRoutine;

        public event Action<BossTelegraphEvent> TelegraphStarted;

        private void Reset()
        {
            skillUser = GetComponent<SkillUserComponent>();
        }

        private void OnEnable()
        {
            if (skillUser == null)
            {
                skillUser = GetComponent<SkillUserComponent>();
            }

            if (autoStart)
            {
                StartLoop();
            }
        }

        private void OnDisable()
        {
            StopLoop();
        }

        public void StartLoop()
        {
            if (loopRoutine != null)
            {
                return;
            }

            loopRoutine = StartCoroutine(RunLoop());
        }

        public void StopLoop()
        {
            if (loopRoutine == null)
            {
                return;
            }

            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        private IEnumerator RunLoop()
        {
            var delay = Mathf.Max(0f, initialDelay);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            var index = 0;
            while (isActiveAndEnabled)
            {
                if (skillCycle.Count == 0 || skillUser == null)
                {
                    yield return null;
                    continue;
                }

                if (index >= skillCycle.Count)
                {
                    index = 0;
                }

                var entry = skillCycle[index];
                index++;

                if (entry == null || entry.Skill == null)
                {
                    yield return null;
                    continue;
                }

                var target = ResolveTarget();
                if (entry.RequireTarget && target == null)
                {
                    yield return new WaitForSeconds(Mathf.Max(0.05f, retryInterval));
                    continue;
                }

                var telegraphDuration = Mathf.Max(0f, entry.TelegraphDuration);
                if (telegraphDuration > 0f)
                {
                    RaiseTelegraph(entry, target, telegraphDuration);
                    if (showGroundTelegraph)
                    {
                        SpawnTelegraphMarker(entry, target, telegraphDuration);
                    }

                    yield return new WaitForSeconds(telegraphDuration);
                }

                yield return TryCastEntry(entry, target);
            }
        }

        private IEnumerator TryCastEntry(BossSkillCycleEntry entry, Transform target)
        {
            var castWindow = Mathf.Max(0.05f, entry.CastRetryWindow);
            var retry = Mathf.Max(0.05f, retryInterval);
            var deadline = Time.time + castWindow;
            var casted = false;

            while (Time.time <= deadline)
            {
                if (!entry.RequireTarget && explicitTarget != null)
                {
                    target = explicitTarget;
                }
                else if (entry.RequireTarget && target == null)
                {
                    target = ResolveTarget();
                    if (target == null)
                    {
                        yield return new WaitForSeconds(retry);
                        continue;
                    }
                }

                casted = skillUser != null && skillUser.TryCast(entry.Skill, target != null ? target.gameObject : null);
                if (casted)
                {
                    break;
                }

                yield return new WaitForSeconds(retry);
            }

            var delay = casted ? entry.DelayAfterCast : entry.DelayOnFail;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            else
            {
                yield return null;
            }
        }

        private Transform ResolveTarget()
        {
            if (explicitTarget != null)
            {
                return explicitTarget;
            }

            if (!string.IsNullOrWhiteSpace(targetTag))
            {
                var tagged = PlayerUnitLocator.FindGameObjectWithTagSafe(targetTag);
                if (tagged != null)
                {
                    return tagged.transform;
                }
            }

            return null;
        }

        private void RaiseTelegraph(BossSkillCycleEntry entry, Transform target, float duration)
        {
            var position = target != null ? target.position : transform.position;
            TelegraphStarted?.Invoke(new BossTelegraphEvent(entry.Skill, position, duration));
        }

        private void SpawnTelegraphMarker(BossSkillCycleEntry entry, Transform target, float duration)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "BossTelegraph";
            marker.transform.SetParent(telegraphRoot != null ? telegraphRoot : null, true);
            var center = target != null ? target.position : transform.position;
            marker.transform.position = new Vector3(center.x, center.y + telegraphHeight, center.z);

            var radius = ResolveTelegraphRadius(entry.Skill);
            marker.transform.localScale = new Vector3(radius * 2f, telegraphHeight, radius * 2f);

            var collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", telegraphColor);
                block.SetColor("_BaseColor", telegraphColor);
                renderer.SetPropertyBlock(block);
            }

            Destroy(marker, Mathf.Max(0.1f, duration));
        }

        private float ResolveTelegraphRadius(SkillDefinition skill)
        {
            if (skill == null || skill.Targeting == null)
            {
                return Mathf.Max(0.5f, fallbackTelegraphRadius);
            }

            if (skill.Targeting.Radius > 0f)
            {
                return Mathf.Max(0.5f, skill.Targeting.Radius);
            }

            if (skill.Targeting.Range > 0f)
            {
                return Mathf.Max(0.5f, skill.Targeting.Range * 0.5f);
            }

            return Mathf.Max(0.5f, fallbackTelegraphRadius);
        }
    }

    [Serializable]
    public class BossSkillCycleEntry
    {
        [SerializeField] private SkillDefinition skill;
        [SerializeField] private float telegraphDuration = 0.8f;
        [SerializeField] private float castRetryWindow = 1.5f;
        [SerializeField] private float delayAfterCast = 1f;
        [SerializeField] private float delayOnFail = 0.4f;
        [SerializeField] private bool requireTarget = true;

        public SkillDefinition Skill => skill;
        public float TelegraphDuration => telegraphDuration;
        public float CastRetryWindow => castRetryWindow;
        public float DelayAfterCast => delayAfterCast;
        public float DelayOnFail => delayOnFail;
        public bool RequireTarget => requireTarget;
    }

    public readonly struct BossTelegraphEvent
    {
        public readonly SkillDefinition Skill;
        public readonly Vector3 Position;
        public readonly float Duration;

        public BossTelegraphEvent(SkillDefinition skill, Vector3 position, float duration)
        {
            Skill = skill;
            Position = position;
            Duration = duration;
        }
    }
}
