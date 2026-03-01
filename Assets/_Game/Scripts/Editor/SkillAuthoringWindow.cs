using System;
using System.Collections.Generic;
using CombatSystem.Data;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CombatSystem.Editor
{
    public class SkillAuthoringWindow : EditorWindow
    {
        private readonly List<SkillDefinition> allSkills = new List<SkillDefinition>(256);
        private readonly List<SkillDefinition> filteredSkills = new List<SkillDefinition>(256);
        private readonly List<SkillValidationMessage> validationMessages = new List<SkillValidationMessage>(64);

        private string searchKeyword = string.Empty;
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private Vector2 validationScroll;
        private SkillDefinition selectedSkill;
        private SerializedObject selectedSkillSerialized;
        private SerializedProperty stepsProperty;
        private ReorderableList stepsList;
        private int selectedStepIndex = -1;
        private bool showValidation;
        private GameDatabase databaseOverride;
        private UnitDefinition playerUnitOverride;

        [MenuItem("Combat/Tools/Skill Authoring Window")]
        public static void Open()
        {
            var window = GetWindow<SkillAuthoringWindow>("Skill Authoring");
            window.minSize = new Vector2(1100f, 600f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshSkillList();
        }

        private void OnGUI()
        {
            DrawToolbar();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSkillListPanel();
                DrawStepsPanel();
                DrawStepDetailPanel();
            }

            if (showValidation)
            {
                DrawValidationPanel();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Create Template", EditorStyles.toolbarButton, GUILayout.Width(120f)))
                {
                    CreateSkillTemplate();
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    RefreshSkillList();
                }

                GUILayout.Space(12f);
                DrawSearchField();
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawSearchField()
        {
            var textStyle = GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarTextField;
            var cancelStyle = GUI.skin.FindStyle("ToolbarSeachCancelButton") ?? EditorStyles.toolbarButton;
            var updated = GUILayout.TextField(searchKeyword, textStyle, GUILayout.Width(260f));
            if (!string.Equals(updated, searchKeyword, StringComparison.Ordinal))
            {
                searchKeyword = updated;
                FilterSkills();
            }

            if (GUILayout.Button(string.Empty, cancelStyle))
            {
                searchKeyword = string.Empty;
                GUI.FocusControl(null);
                FilterSkills();
            }
        }

        private void DrawSkillListPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(280f)))
            {
                EditorGUILayout.LabelField("Skills", EditorStyles.boldLabel);
                listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
                for (int i = 0; i < filteredSkills.Count; i++)
                {
                    var skill = filteredSkills[i];
                    if (skill == null)
                    {
                        continue;
                    }

                    var style = ReferenceEquals(skill, selectedSkill) ? EditorStyles.helpBox : EditorStyles.miniButton;
                    if (GUILayout.Button(skill.name, style))
                    {
                        SelectSkill(skill);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawStepsPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(380f)))
            {
                EditorGUILayout.LabelField("Step Timeline", EditorStyles.boldLabel);
                if (selectedSkill == null)
                {
                    EditorGUILayout.HelpBox("Select a skill to edit steps.", MessageType.Info);
                    return;
                }

                EnsureSerialized();
                selectedSkillSerialized.Update();
                stepsList?.DoLayoutList();

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = selectedStepIndex >= 0 && selectedStepIndex < stepsProperty.arraySize;
                    if (GUILayout.Button("Duplicate Step"))
                    {
                        DuplicateSelectedStep();
                    }

                    GUI.enabled = true;
                    if (GUILayout.Button("Validate"))
                    {
                        ValidateSelectedSkill();
                    }

                    if (GUILayout.Button("Sync To Database"))
                    {
                        SyncSelectedToDatabase();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Mount To Player"))
                    {
                        MountSelectedSkillToPlayer();
                    }

                    if (GUILayout.Button("Batch Bind Player Cues"))
                    {
                        if (!EditorApplication.ExecuteMenuItem("Combat/Tools/Skill Presentation/Bind Cues For Player Skills"))
                        {
                            Debug.LogWarning(
                                "[SkillAuthoringWindow] Menu not found: Combat/Tools/Skill Presentation/Bind Cues For Player Skills");
                        }
                    }

                    if (GUILayout.Button("Migrate Legacy (Selected)"))
                    {
                        MigrateLegacyToCues(selectedSkill);
                    }

                    if (GUILayout.Button("Migrate Legacy (All)"))
                    {
                        MigrateAllSkillsLegacyToCues();
                    }
                }

                selectedSkillSerialized.ApplyModifiedProperties();
            }
        }

        private void DrawStepDetailPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Step Detail", EditorStyles.boldLabel);
                if (selectedSkill == null)
                {
                    EditorGUILayout.HelpBox("Select a skill first.", MessageType.Info);
                    return;
                }

                EnsureSerialized();
                selectedSkillSerialized.Update();
                detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.ExpandHeight(true));

                EditorGUILayout.ObjectField("Skill", selectedSkill, typeof(SkillDefinition), false);
                databaseOverride = (GameDatabase)EditorGUILayout.ObjectField("Database", databaseOverride, typeof(GameDatabase), false);
                playerUnitOverride = (UnitDefinition)EditorGUILayout.ObjectField("Player Unit", playerUnitOverride, typeof(UnitDefinition), false);

                if (selectedStepIndex < 0 || selectedStepIndex >= stepsProperty.arraySize)
                {
                    EditorGUILayout.HelpBox("Select one step in the timeline to edit details.", MessageType.Info);
                }
                else
                {
                    var step = stepsProperty.GetArrayElementAtIndex(selectedStepIndex);
                    EditorGUILayout.PropertyField(step.FindPropertyRelative("trigger"));
                    EditorGUILayout.PropertyField(step.FindPropertyRelative("delay"));
                    EditorGUILayout.PropertyField(step.FindPropertyRelative("condition"));
                    EditorGUILayout.Space(6f);

                    EditorGUILayout.LabelField("Combat Effects", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(step.FindPropertyRelative("effects"), true);
                    EditorGUILayout.Space(6f);

                    EditorGUILayout.LabelField("Presentation Cues", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(step.FindPropertyRelative("presentationCues"), true);
                    EditorGUILayout.Space(6f);

                    EditorGUILayout.LabelField("Legacy Fields", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(step.FindPropertyRelative("animationTrigger"));
                    EditorGUILayout.PropertyField(step.FindPropertyRelative("vfxPrefab"));
                    EditorGUILayout.PropertyField(step.FindPropertyRelative("sfx"));
                }

                EditorGUILayout.EndScrollView();
                selectedSkillSerialized.ApplyModifiedProperties();
            }
        }

        private void DrawValidationPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
                validationScroll = EditorGUILayout.BeginScrollView(validationScroll, GUILayout.Height(160f));
                for (int i = 0; i < validationMessages.Count; i++)
                {
                    var msg = validationMessages[i];
                    var prefix = msg.Severity == SkillValidationSeverity.Error
                        ? "Error"
                        : msg.Severity == SkillValidationSeverity.Warning
                            ? "Warning"
                            : "Info";
                    EditorGUILayout.LabelField($"{prefix}: {msg.Message}");
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void RefreshSkillList()
        {
            allSkills.Clear();
            filteredSkills.Clear();

            var guids = AssetDatabase.FindAssets("t:SkillDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
                if (skill != null)
                {
                    allSkills.Add(skill);
                }
            }

            allSkills.Sort((a, b) => string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));
            FilterSkills();
            if (selectedSkill == null && filteredSkills.Count > 0)
            {
                SelectSkill(filteredSkills[0]);
            }
        }

        private void FilterSkills()
        {
            filteredSkills.Clear();
            var keyword = searchKeyword != null ? searchKeyword.Trim() : string.Empty;
            for (int i = 0; i < allSkills.Count; i++)
            {
                var skill = allSkills[i];
                if (skill == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(keyword)
                    && skill.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                filteredSkills.Add(skill);
            }
        }

        private void SelectSkill(SkillDefinition skill)
        {
            if (ReferenceEquals(selectedSkill, skill))
            {
                return;
            }

            selectedSkill = skill;
            selectedStepIndex = -1;
            showValidation = false;
            validationMessages.Clear();
            selectedSkillSerialized = null;
            stepsProperty = null;
            stepsList = null;
            EnsureSerialized();
        }

        private void EnsureSerialized()
        {
            if (selectedSkill == null)
            {
                return;
            }

            if (selectedSkillSerialized != null && selectedSkillSerialized.targetObject == selectedSkill && stepsList != null)
            {
                return;
            }

            selectedSkillSerialized = new SerializedObject(selectedSkill);
            stepsProperty = selectedSkillSerialized.FindProperty("steps");
            stepsList = new ReorderableList(selectedSkillSerialized, stepsProperty, true, true, true, true);
            stepsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Steps");
            stepsList.onSelectCallback = list => selectedStepIndex = list.index;
            stepsList.drawElementCallback = DrawStepElement;
            stepsList.onAddCallback = list =>
            {
                var index = list.serializedProperty.arraySize;
                list.serializedProperty.InsertArrayElementAtIndex(index);
                list.index = index;
                selectedStepIndex = index;
                selectedSkillSerialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(selectedSkill);
            };
            stepsList.onRemoveCallback = list =>
            {
                if (list.index < 0 || list.index >= list.serializedProperty.arraySize)
                {
                    return;
                }

                list.serializedProperty.DeleteArrayElementAtIndex(list.index);
                list.index = Mathf.Clamp(list.index - 1, 0, list.serializedProperty.arraySize - 1);
                selectedStepIndex = list.index;
                selectedSkillSerialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(selectedSkill);
            };
        }

        private void DrawStepElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (stepsProperty == null || index < 0 || index >= stepsProperty.arraySize)
            {
                return;
            }

            var element = stepsProperty.GetArrayElementAtIndex(index);
            var trigger = element.FindPropertyRelative("trigger");
            var delay = element.FindPropertyRelative("delay");
            var effects = element.FindPropertyRelative("effects");
            var cues = element.FindPropertyRelative("presentationCues");

            var label = $"[{index}] {trigger.enumDisplayNames[trigger.enumValueIndex]}  delay:{delay.floatValue:0.##}  fx:{effects.arraySize}  cue:{cues.arraySize}";
            EditorGUI.LabelField(rect, label);
        }

        private void DuplicateSelectedStep()
        {
            if (selectedSkill == null || stepsProperty == null || selectedStepIndex < 0 || selectedStepIndex >= stepsProperty.arraySize)
            {
                return;
            }

            selectedSkillSerialized.Update();
            stepsProperty.InsertArrayElementAtIndex(selectedStepIndex);
            selectedStepIndex++;
            selectedSkillSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedSkill);
            AssetDatabase.SaveAssets();
        }

        private void CreateSkillTemplate()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Skill Template",
                "Skill_NewTemplate",
                "asset",
                "Select location for new skill asset.");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            var folder = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            var targetingPath = $"{folder}/{fileName}_Targeting.asset";
            var effectPath = $"{folder}/{fileName}_Effect.asset";

            var targeting = ScriptableObject.CreateInstance<TargetingDefinition>();
            var effect = ScriptableObject.CreateInstance<EffectDefinition>();

            AssetDatabase.CreateAsset(skill, path);
            AssetDatabase.CreateAsset(targeting, targetingPath);
            AssetDatabase.CreateAsset(effect, effectPath);

            ConfigureTemplateTargeting(targeting);
            ConfigureTemplateEffect(effect);
            ConfigureTemplateSkill(skill, targeting, effect, fileName);

            EditorUtility.SetDirty(skill);
            EditorUtility.SetDirty(targeting);
            EditorUtility.SetDirty(effect);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RefreshSkillList();
            SelectSkill(skill);
            Selection.activeObject = skill;
        }

        private static void ConfigureTemplateTargeting(TargetingDefinition targeting)
        {
            var so = new SerializedObject(targeting);
            so.FindProperty("mode").enumValueIndex = (int)TargetingMode.Single;
            so.FindProperty("team").enumValueIndex = (int)TargetTeam.Enemy;
            so.FindProperty("origin").enumValueIndex = (int)TargetingOrigin.Caster;
            so.FindProperty("range").floatValue = 8f;
            so.FindProperty("maxTargets").intValue = 1;
            so.FindProperty("allowEmpty").boolValue = false;
            so.FindProperty("requireExplicitTarget").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureTemplateEffect(EffectDefinition effect)
        {
            var so = new SerializedObject(effect);
            so.FindProperty("effectType").enumValueIndex = (int)EffectType.Damage;
            so.FindProperty("damageType").enumValueIndex = (int)DamageType.Physical;
            so.FindProperty("value").floatValue = 20f;
            so.FindProperty("canCrit").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureTemplateSkill(SkillDefinition skill, TargetingDefinition targeting, EffectDefinition effect, string fileName)
        {
            var so = new SerializedObject(skill);
            so.FindProperty("id").stringValue = fileName;
            so.FindProperty("resourceCost").floatValue = 20f;
            so.FindProperty("cooldown").floatValue = 4f;
            so.FindProperty("targeting").objectReferenceValue = targeting;

            var steps = so.FindProperty("steps");
            steps.arraySize = 1;
            var step = steps.GetArrayElementAtIndex(0);
            step.FindPropertyRelative("trigger").enumValueIndex = (int)SkillStepTrigger.OnCastComplete;
            step.FindPropertyRelative("delay").floatValue = 0f;

            var effects = step.FindPropertyRelative("effects");
            effects.arraySize = 1;
            effects.GetArrayElementAtIndex(0).objectReferenceValue = effect;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private void ValidateSelectedSkill()
        {
            validationMessages.Clear();
            if (selectedSkill != null)
            {
                validationMessages.AddRange(SkillValidationService.ValidateSkill(selectedSkill));
            }

            showValidation = true;
        }

        private void SyncSelectedToDatabase()
        {
            if (selectedSkill == null)
            {
                return;
            }

            var database = databaseOverride != null ? databaseOverride : FindAnyDatabase();
            if (database == null)
            {
                Debug.LogWarning("[SkillAuthoringWindow] GameDatabase not found.");
                return;
            }

            var effects = new HashSet<EffectDefinition>();
            var buffs = new HashSet<BuffDefinition>();
            var projectiles = new HashSet<ProjectileDefinition>();
            var targetings = new HashSet<TargetingDefinition>();

            CollectSkillReferences(selectedSkill, effects, buffs, projectiles, targetings);
            var so = new SerializedObject(database);
            AddUniqueObject(so.FindProperty("skills"), selectedSkill);
            AddUniqueObject(so.FindProperty("targetings"), selectedSkill.Targeting);

            foreach (var effect in effects)
            {
                AddUniqueObject(so.FindProperty("effects"), effect);
            }

            foreach (var buff in buffs)
            {
                AddUniqueObject(so.FindProperty("buffs"), buff);
            }

            foreach (var projectile in projectiles)
            {
                AddUniqueObject(so.FindProperty("projectiles"), projectile);
            }

            foreach (var targeting in targetings)
            {
                AddUniqueObject(so.FindProperty("targetings"), targeting);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        private void MountSelectedSkillToPlayer()
        {
            if (selectedSkill == null)
            {
                return;
            }

            var playerUnit = playerUnitOverride != null ? playerUnitOverride : FindPlayerUnitDefinition();
            if (playerUnit == null)
            {
                Debug.LogWarning("[SkillAuthoringWindow] Player UnitDefinition not found.");
                return;
            }

            var so = new SerializedObject(playerUnit);
            var list = so.FindProperty("startingSkills");
            AddUniqueObject(list, selectedSkill);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(playerUnit);
            AssetDatabase.SaveAssets();
        }

        private void MigrateAllSkillsLegacyToCues()
        {
            for (int i = 0; i < allSkills.Count; i++)
            {
                MigrateLegacyToCues(allSkills[i]);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void MigrateLegacyToCues(SkillDefinition skill)
        {
            if (skill == null)
            {
                return;
            }

            var so = new SerializedObject(skill);
            var steps = so.FindProperty("steps");
            var changed = false;
            for (int i = 0; i < steps.arraySize; i++)
            {
                var step = steps.GetArrayElementAtIndex(i);
                var animationTrigger = step.FindPropertyRelative("animationTrigger").stringValue;
                var vfx = step.FindPropertyRelative("vfxPrefab").objectReferenceValue;
                var sfx = step.FindPropertyRelative("sfx").objectReferenceValue;
                if (string.IsNullOrWhiteSpace(animationTrigger) && vfx == null && sfx == null)
                {
                    continue;
                }

                var cues = step.FindPropertyRelative("presentationCues");
                if (cues.arraySize > 0)
                {
                    continue;
                }

                cues.arraySize = 1;
                var cue = cues.GetArrayElementAtIndex(0);
                cue.FindPropertyRelative("cueId").stringValue = "MigratedLegacyCue";
                cue.FindPropertyRelative("eventType").enumValueIndex = (int)PresentationEventType.StepExecuted;
                cue.FindPropertyRelative("anchorType").enumValueIndex = (int)PresentationAnchorType.Caster;
                cue.FindPropertyRelative("spawnSpace").enumValueIndex = (int)PresentationSpawnSpace.World;
                cue.FindPropertyRelative("animationTrigger").stringValue = animationTrigger;
                cue.FindPropertyRelative("vfxPrefab").objectReferenceValue = vfx;
                cue.FindPropertyRelative("sfx").objectReferenceValue = sfx;
                cue.FindPropertyRelative("maxLifetime").floatValue = 2f;
                cue.FindPropertyRelative("audioVolume").floatValue = 1f;
                cue.FindPropertyRelative("audioPitch").floatValue = 1f;
                cue.FindPropertyRelative("audioSpatialBlend").floatValue = 1f;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);
        }

        private static void AddUniqueObject(SerializedProperty listProperty, UnityEngine.Object value)
        {
            if (listProperty == null || !listProperty.isArray || value == null)
            {
                return;
            }

            for (int i = 0; i < listProperty.arraySize; i++)
            {
                var item = listProperty.GetArrayElementAtIndex(i);
                if (ReferenceEquals(item.objectReferenceValue, value))
                {
                    return;
                }
            }

            var index = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(index);
            listProperty.GetArrayElementAtIndex(index).objectReferenceValue = value;
        }

        private static GameDatabase FindAnyDatabase()
        {
            var guids = AssetDatabase.FindAssets("t:GameDatabase");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(path);
                if (db != null)
                {
                    return db;
                }
            }

            return null;
        }

        private static UnitDefinition FindPlayerUnitDefinition()
        {
            var guids = AssetDatabase.FindAssets("t:UnitDefinition");
            UnitDefinition fallback = null;
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
                if (unit == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = unit;
                }

                if (string.Equals(unit.Id, "Unit_Player", StringComparison.OrdinalIgnoreCase))
                {
                    return unit;
                }

                if (unit.name.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fallback = unit;
                }
            }

            return fallback;
        }

        private static void CollectSkillReferences(
            SkillDefinition skill,
            HashSet<EffectDefinition> effects,
            HashSet<BuffDefinition> buffs,
            HashSet<ProjectileDefinition> projectiles,
            HashSet<TargetingDefinition> targetings)
        {
            if (skill == null)
            {
                return;
            }

            if (skill.Targeting != null)
            {
                targetings.Add(skill.Targeting);
            }

            var steps = skill.Steps;
            if (steps == null)
            {
                return;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null || step.effects == null)
                {
                    continue;
                }

                for (int j = 0; j < step.effects.Count; j++)
                {
                    CollectEffectReferences(step.effects[j], effects, buffs, projectiles, targetings);
                }
            }
        }

        private static void CollectEffectReferences(
            EffectDefinition effect,
            HashSet<EffectDefinition> effects,
            HashSet<BuffDefinition> buffs,
            HashSet<ProjectileDefinition> projectiles,
            HashSet<TargetingDefinition> targetings)
        {
            if (effect == null || effects.Contains(effect))
            {
                return;
            }

            effects.Add(effect);
            if (effect.Buff != null)
            {
                buffs.Add(effect.Buff);
            }

            if (effect.Projectile != null)
            {
                projectiles.Add(effect.Projectile);
                var onHit = effect.Projectile.OnHitEffects;
                if (onHit != null)
                {
                    for (int i = 0; i < onHit.Count; i++)
                    {
                        CollectEffectReferences(onHit[i], effects, buffs, projectiles, targetings);
                    }
                }
            }

            if (effect.OverrideTargeting != null)
            {
                targetings.Add(effect.OverrideTargeting);
            }

            if (effect.TriggeredSkill != null)
            {
                CollectSkillReferences(effect.TriggeredSkill, effects, buffs, projectiles, targetings);
            }
        }
    }
}
