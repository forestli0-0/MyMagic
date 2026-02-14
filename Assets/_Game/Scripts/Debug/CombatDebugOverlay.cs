using System.Text;
using CombatSystem.Core;
using CombatSystem.Gameplay;
using CombatSystem.Input;
using CombatSystem.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CombatSystem.Debugging
{
    public class CombatDebugOverlay : MonoBehaviour
    {
        [SerializeField] private Text outputText;
        [SerializeField] private Graphic background;
        [SerializeField] private bool visible = true;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private bool autoFindInputReader = true;
        [SerializeField] private float refreshInterval = 0.25f;
        [SerializeField] private float autoResolveInterval = 0.5f;

        [Header("Target")]
        [SerializeField] private UnitRoot targetUnit;
        [SerializeField] private HealthComponent health;
        [SerializeField] private ResourceComponent resource;
        [SerializeField] private BuffController buffs;
        [SerializeField] private CooldownComponent cooldown;
        [SerializeField] private SkillUserComponent skillUser;

        [Header("Systems")]
        [SerializeField] private ProjectilePool projectilePool;
        [SerializeField] private FloatingTextManager floatingText;

        private float nextRefreshTime;
        private float fpsTimer;
        private int fpsFrames;
        private float lastFps;
        private float nextResolveTime;
        private readonly StringBuilder builder = new StringBuilder(256);

        private void Awake()
        {
            ResolveInputReader();
            ResolveReferences(true);
            SetVisible(visible);
        }

        private void OnEnable()
        {
            ResolveInputReader();
            if (inputReader != null)
            {
                inputReader.ToggleOverlay += HandleToggleOverlay;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.ToggleOverlay -= HandleToggleOverlay;
            }
        }

        private void Update()
        {
            if (!visible || outputText == null)
            {
                return;
            }

            ResolveReferences(false);

            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;

            if (Time.unscaledTime >= nextRefreshTime)
            {
                lastFps = fpsTimer > 0f ? fpsFrames / fpsTimer : 0f;
                fpsFrames = 0;
                fpsTimer = 0f;
                nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
                RefreshText();
            }
        }

        private void RefreshText()
        {
            builder.Length = 0;
            builder.Append("FPS: ").Append(Mathf.RoundToInt(lastFps)).Append('\n');

            var unitName = targetUnit != null ? targetUnit.name : "None";
            builder.Append("Unit: ").Append(unitName).Append('\n');

            if (health != null)
            {
                builder.Append("HP: ")
                    .Append(Mathf.RoundToInt(health.Current))
                    .Append('/')
                    .Append(Mathf.RoundToInt(health.Max))
                    .Append('\n');
            }

            if (resource != null)
            {
                builder.Append("MP: ")
                    .Append(Mathf.RoundToInt(resource.Current))
                    .Append('/')
                    .Append(Mathf.RoundToInt(resource.Max))
                    .Append('\n');
            }

            if (buffs != null)
            {
                builder.Append("Buffs: ").Append(buffs.ActiveBuffs.Count).Append('\n');
            }

            if (skillUser != null)
            {
                builder.Append("Casting: ");
                if (skillUser.IsCasting && skillUser.CurrentSkill != null)
                {
                    builder.Append(skillUser.CurrentSkill.DisplayName);
                }
                else
                {
                    builder.Append("None");
                }

                builder.Append('\n');
            }

            if (cooldown != null)
            {
                builder.Append("Cooldowns: ").Append(cooldown.ActiveCooldownCount).Append('\n');
            }

            if (projectilePool != null)
            {
                builder.Append("Projectiles: ")
                    .Append(projectilePool.ActiveCount)
                    .Append('/')
                    .Append(projectilePool.PooledCount)
                    .Append('\n');
            }

            if (floatingText != null)
            {
                builder.Append("FloatingText: ")
                    .Append(floatingText.ActiveCount)
                    .Append('/')
                    .Append(floatingText.PooledCount)
                    .Append('\n');
            }

            outputText.text = builder.ToString();
        }

        private void SetVisible(bool state)
        {
            if (outputText != null)
            {
                outputText.enabled = state;
            }

            if (background != null)
            {
                background.enabled = state;
            }
        }

        private void ResolveReferences(bool force)
        {
            if (!force && Time.unscaledTime < nextResolveTime)
            {
                return;
            }

            var needsResolve = force ||
                               !PlayerUnitLocator.IsPlayerUnit(targetUnit) ||
                               health == null ||
                               resource == null ||
                               buffs == null ||
                               cooldown == null ||
                               skillUser == null ||
                               projectilePool == null ||
                               floatingText == null;
            if (!needsResolve)
            {
                return;
            }

            nextResolveTime = Time.unscaledTime + Mathf.Max(0.1f, autoResolveInterval);

            if (!PlayerUnitLocator.IsPlayerUnit(targetUnit))
            {
                targetUnit = null;
            }

            if (targetUnit == null)
            {
                targetUnit = PlayerUnitLocator.FindPlayerUnit();
            }

            if (targetUnit != null)
            {
                health = targetUnit.GetComponent<HealthComponent>();
                resource = targetUnit.GetComponent<ResourceComponent>();
                buffs = targetUnit.GetComponent<BuffController>();
                cooldown = targetUnit.GetComponent<CooldownComponent>();
                skillUser = targetUnit.GetComponent<SkillUserComponent>();
            }
            else
            {
                health = null;
                resource = null;
                buffs = null;
                cooldown = null;
                skillUser = null;
            }

            if (projectilePool == null)
            {
                projectilePool = FindFirstObjectByType<ProjectilePool>(FindObjectsInactive.Include);
            }

            if (floatingText == null)
            {
                floatingText = FindFirstObjectByType<FloatingTextManager>(FindObjectsInactive.Include);
            }
        }

        private void ResolveInputReader()
        {
            if (!autoFindInputReader || inputReader != null)
            {
                return;
            }

            inputReader = FindFirstObjectByType<InputReader>();
        }

        private void HandleToggleOverlay()
        {
            visible = !visible;
            SetVisible(visible);
        }
    }
}
