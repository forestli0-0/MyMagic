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
        private static readonly GUIContent CreateTemplateContent = new GUIContent(
            "Create Template",
            "创建一个基础技能模板：自动生成 Skill、Targeting、Effect 三个资产。");
        private static readonly GUIContent RefreshContent = new GUIContent(
            "Refresh",
            "重新扫描并刷新技能列表。");
        private static readonly GUIContent AreaTipsContent = new GUIContent(
            "Area Tips",
            "显示/隐藏各区域用途说明。");
        private static readonly GUIContent DuplicateStepContent = new GUIContent(
            "Duplicate Step",
            "复制当前选中的 Step（用于快速做多段技能）。");
        private static readonly GUIContent ValidateContent = new GUIContent(
            "Validate",
            "运行技能校验，检查空引用、配置缺失和常见错误。");
        private static readonly GUIContent SyncDatabaseContent = new GUIContent(
            "Sync To Database",
            "把当前技能及其依赖（Effect/Buff/Projectile/Targeting）同步到 GameDatabase。");
        private static readonly GUIContent MountToPlayerContent = new GUIContent(
            "Mount To Player",
            "把当前技能加入玩家 startingSkills 列表，便于进场测试。");
        private static readonly GUIContent BatchBindCuesContent = new GUIContent(
            "Batch Bind Player Cues",
            "对玩家技能批量生成/更新自动表现 Cue（Auto_*）。");
        private static readonly GUIContent AddDamageStepContent = new GUIContent(
            "+ Damage Step",
            "添加一个伤害类步骤模板（OnCastComplete）。");
        private static readonly GUIContent AddProjectileStepContent = new GUIContent(
            "+ Projectile Step",
            "添加一个投射物类步骤模板（OnCastComplete）。");
        private static readonly GUIContent AddBuffStepContent = new GUIContent(
            "+ Buff Step",
            "添加一个 Buff 类步骤模板（OnCastStart）。");
        private static readonly GUIContent AddCastCueContent = new GUIContent(
            "Add Cast Cue",
            "向当前步骤添加一个施法表现 Cue（StepExecuted/Caster）。");
        private static readonly GUIContent AddHitCueContent = new GUIContent(
            "Add Hit Cue",
            "向当前步骤添加一个命中表现 Cue（EffectAfterApply/PrimaryTarget）。");
        private static readonly GUIContent AddProjectileCueContent = new GUIContent(
            "Add Projectile Cue",
            "向当前步骤添加投射物表现 Cue（Spawn + Hit）。");

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
        private bool showAreaTips = true;
        private bool expandLayoutGuide = true;
        private GameDatabase databaseOverride;
        private UnitDefinition playerUnitOverride;

        private enum StepTemplateKind
        {
            Damage = 0,
            Projectile = 1,
            Buff = 2
        }

        private enum CueTemplateKind
        {
            Cast = 0,
            Hit = 1,
            ProjectilePair = 2
        }

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
            DrawLayoutGuide();

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
                if (GUILayout.Button(CreateTemplateContent, EditorStyles.toolbarButton, GUILayout.Width(120f)))
                {
                    CreateSkillTemplate();
                }

                if (GUILayout.Button(RefreshContent, EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    RefreshSkillList();
                }

                GUILayout.Space(12f);
                DrawSearchField();
                GUILayout.FlexibleSpace();
                showAreaTips = GUILayout.Toggle(showAreaTips, AreaTipsContent, EditorStyles.toolbarButton, GUILayout.Width(80f));
            }
        }

        private void DrawLayoutGuide()
        {
            if (!showAreaTips)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                expandLayoutGuide = EditorGUILayout.Foldout(expandLayoutGuide, "Layout Guide / 布局说明", true);
                if (!expandLayoutGuide)
                {
                    return;
                }

                EditorGUILayout.LabelField("1) 左侧 Skills：选择或搜索技能，决定当前编辑对象。");
                EditorGUILayout.LabelField("2) 中间 Step Timeline：管理技能步骤（触发时机、顺序、复制、校验、批量Cue）。");
                EditorGUILayout.LabelField("3) 右侧 Step Detail：编辑当前步骤的机制效果与表现 Cue。");
                EditorGUILayout.LabelField("4) 底部 Validation：显示错误/警告，优先修 Error。");
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
                if (showAreaTips)
                {
                    EditorGUILayout.HelpBox(
                        "用途：选择要编辑的技能。\n操作：上方搜索可过滤，Create Template 可新建模板技能。",
                        MessageType.None);
                }

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
                if (showAreaTips)
                {
                    EditorGUILayout.HelpBox(
                        "用途：编排步骤触发与顺序。\n建议流程：先配 steps/effects，再点 Validate，最后挂载到玩家。",
                        MessageType.None);
                }

                if (selectedSkill == null)
                {
                    EditorGUILayout.HelpBox("Select a skill to edit steps.", MessageType.Info);
                    return;
                }

                EnsureSerialized();
                selectedSkillSerialized.Update();
                stepsList?.DoLayoutList();
                DrawStepValidationLegend();

                var canDuplicate = HasValidSelectedStep();
                var canValidate = selectedSkill != null;
                var canSync = TryResolveDatabase(out _, out var syncReason);
                var canMountToPlayer = TryResolvePlayerUnit(out _, out var mountReason);
                var canBatchBind = selectedSkill != null;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawActionButton(DuplicateStepContent, canDuplicate, "需先在 Steps 列表中选中一个步骤。"))
                    {
                        DuplicateSelectedStep();
                    }

                    if (DrawActionButton(ValidateContent, canValidate, "当前没有可校验的技能。"))
                    {
                        ValidateSelectedSkill();
                    }

                    if (DrawActionButton(SyncDatabaseContent, canSync, syncReason))
                    {
                        SyncSelectedToDatabase();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawActionButton(MountToPlayerContent, canMountToPlayer, mountReason))
                    {
                        MountSelectedSkillToPlayer();
                    }

                    if (DrawActionButton(BatchBindCuesContent, canBatchBind, "当前没有选中技能。"))
                    {
                        if (!EditorApplication.ExecuteMenuItem("Combat/Tools/Skill Presentation/Bind Cues For Player Skills"))
                        {
                            Debug.LogWarning(
                                "[SkillAuthoringWindow] Menu not found: Combat/Tools/Skill Presentation/Bind Cues For Player Skills");
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(AddDamageStepContent))
                    {
                        AddStepTemplate(StepTemplateKind.Damage);
                    }

                    if (GUILayout.Button(AddProjectileStepContent))
                    {
                        AddStepTemplate(StepTemplateKind.Projectile);
                    }

                    if (GUILayout.Button(AddBuffStepContent))
                    {
                        AddStepTemplate(StepTemplateKind.Buff);
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
                if (showAreaTips)
                {
                    EditorGUILayout.HelpBox(
                        "用途：编辑当前选中 Step 的详细配置。\nCombat Effects 是机制层；Presentation Cues 是动画/特效/音效表现层。",
                        MessageType.None);
                }

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
                    EditorGUILayout.HelpBox("请先在中间 Steps 列表点击一行（如 [0] On Cast Start）后再编辑详情。", MessageType.Info);
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
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(AddCastCueContent))
                        {
                            AddCueTemplateToSelectedStep(CueTemplateKind.Cast);
                        }

                        if (GUILayout.Button(AddHitCueContent))
                        {
                            AddCueTemplateToSelectedStep(CueTemplateKind.Hit);
                        }

                        if (GUILayout.Button(AddProjectileCueContent))
                        {
                            AddCueTemplateToSelectedStep(CueTemplateKind.ProjectilePair);
                        }
                    }
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
                if (showAreaTips)
                {
                    EditorGUILayout.HelpBox(
                        "用途：展示 Validate 的结果。\n处理顺序：先 Error，再 Warning；Info 作为优化建议。",
                        MessageType.None);
                }

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
            EnsureStepSelection();
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
                var step = list.serializedProperty.GetArrayElementAtIndex(index);
                ResetStepProperty(step);
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
                EnsureStepSelection();
            };

            EnsureStepSelection();
        }

        private void EnsureStepSelection()
        {
            if (stepsProperty == null || stepsList == null || stepsProperty.arraySize <= 0)
            {
                selectedStepIndex = -1;
                if (stepsList != null)
                {
                    stepsList.index = -1;
                }

                return;
            }

            if (selectedStepIndex < 0 || selectedStepIndex >= stepsProperty.arraySize)
            {
                selectedStepIndex = 0;
            }

            stepsList.index = selectedStepIndex;
        }

        private void DrawStepValidationLegend()
        {
            EditorGUILayout.LabelField("实时校验： [OK] 无问题 / [Wn] 警告 / [En] 错误", EditorStyles.miniLabel);
        }

        private bool HasValidSelectedStep()
        {
            return stepsProperty != null
                   && selectedStepIndex >= 0
                   && selectedStepIndex < stepsProperty.arraySize;
        }

        private bool TryResolveDatabase(out GameDatabase database, out string reason)
        {
            if (selectedSkill == null)
            {
                database = null;
                reason = "当前没有选中技能。";
                return false;
            }

            database = databaseOverride != null ? databaseOverride : FindAnyDatabase();
            if (database == null)
            {
                reason = "未找到 GameDatabase，请先在右侧 Database 指定。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool TryResolvePlayerUnit(out UnitDefinition unit, out string reason)
        {
            if (selectedSkill == null)
            {
                unit = null;
                reason = "当前没有选中技能。";
                return false;
            }

            unit = playerUnitOverride != null ? playerUnitOverride : FindPlayerUnitDefinition();
            if (unit == null)
            {
                reason = "未找到玩家 UnitDefinition，请先在右侧 Player Unit 指定。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool DrawActionButton(GUIContent content, bool enabled, string disabledReason)
        {
            var drawContent = enabled ? content : BuildDisabledContent(content, disabledReason);
            EditorGUI.BeginDisabledGroup(!enabled);
            var clicked = GUILayout.Button(drawContent);
            EditorGUI.EndDisabledGroup();
            return enabled && clicked;
        }

        private static GUIContent BuildDisabledContent(GUIContent source, string disabledReason)
        {
            var tooltip = source != null ? source.tooltip : string.Empty;
            if (!string.IsNullOrWhiteSpace(disabledReason))
            {
                tooltip = string.IsNullOrWhiteSpace(tooltip)
                    ? $"不可用原因：{disabledReason}"
                    : $"{tooltip}\n不可用原因：{disabledReason}";
            }

            return new GUIContent(source != null ? source.text : string.Empty, tooltip);
        }

        private void AddStepTemplate(StepTemplateKind template)
        {
            if (selectedSkill == null || selectedSkillSerialized == null || stepsProperty == null)
            {
                return;
            }

            selectedSkillSerialized.Update();
            var index = stepsProperty.arraySize;
            stepsProperty.InsertArrayElementAtIndex(index);
            var step = stepsProperty.GetArrayElementAtIndex(index);
            ResetStepProperty(step);

            switch (template)
            {
                case StepTemplateKind.Damage:
                    step.FindPropertyRelative("trigger").enumValueIndex = (int)SkillStepTrigger.OnCastComplete;
                    AppendCueTemplate(step.FindPropertyRelative("presentationCues"), CueTemplateKind.Cast);
                    AppendCueTemplate(step.FindPropertyRelative("presentationCues"), CueTemplateKind.Hit);
                    break;
                case StepTemplateKind.Projectile:
                    step.FindPropertyRelative("trigger").enumValueIndex = (int)SkillStepTrigger.OnCastComplete;
                    AppendCueTemplate(step.FindPropertyRelative("presentationCues"), CueTemplateKind.Cast);
                    AppendCueTemplate(step.FindPropertyRelative("presentationCues"), CueTemplateKind.ProjectilePair);
                    break;
                case StepTemplateKind.Buff:
                    step.FindPropertyRelative("trigger").enumValueIndex = (int)SkillStepTrigger.OnCastStart;
                    AppendCueTemplate(step.FindPropertyRelative("presentationCues"), CueTemplateKind.Cast);
                    break;
            }

            selectedStepIndex = index;
            if (stepsList != null)
            {
                stepsList.index = index;
            }

            selectedSkillSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedSkill);
        }

        private static void ResetStepProperty(SerializedProperty step)
        {
            if (step == null)
            {
                return;
            }

            step.FindPropertyRelative("trigger").enumValueIndex = (int)SkillStepTrigger.OnCastComplete;
            step.FindPropertyRelative("delay").floatValue = 0f;
            step.FindPropertyRelative("condition").objectReferenceValue = null;

            var effects = step.FindPropertyRelative("effects");
            if (effects != null)
            {
                effects.arraySize = 0;
            }

            var cues = step.FindPropertyRelative("presentationCues");
            if (cues != null)
            {
                cues.arraySize = 0;
            }
        }

        private void AddCueTemplateToSelectedStep(CueTemplateKind template)
        {
            if (!HasValidSelectedStep() || selectedSkillSerialized == null)
            {
                return;
            }

            selectedSkillSerialized.Update();
            var step = stepsProperty.GetArrayElementAtIndex(selectedStepIndex);
            if (step == null)
            {
                return;
            }

            var cues = step.FindPropertyRelative("presentationCues");
            AppendCueTemplate(cues, template);

            selectedSkillSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedSkill);
        }

        private static void AppendCueTemplate(SerializedProperty cues, CueTemplateKind template)
        {
            if (cues == null || !cues.isArray)
            {
                return;
            }

            switch (template)
            {
                case CueTemplateKind.Cast:
                    AddCueElement(cues, "Template_Cast", PresentationEventType.StepExecuted, PresentationAnchorType.Caster, false);
                    break;
                case CueTemplateKind.Hit:
                {
                    var cue = AddCueElement(cues, "Template_Hit", PresentationEventType.EffectAfterApply, PresentationAnchorType.PrimaryTarget, false);
                    if (cue != null)
                    {
                        cue.FindPropertyRelative("filterByEffectType").boolValue = true;
                        cue.FindPropertyRelative("effectTypeFilter").enumValueIndex = (int)EffectType.Damage;
                    }

                    break;
                }
                case CueTemplateKind.ProjectilePair:
                    AddCueElement(cues, "Template_ProjectileSpawn", PresentationEventType.ProjectileSpawn, PresentationAnchorType.Projectile, true);
                    AddCueElement(cues, "Template_ProjectileHit", PresentationEventType.ProjectileHit, PresentationAnchorType.PrimaryTarget, false);
                    break;
            }
        }

        private static SerializedProperty AddCueElement(
            SerializedProperty cues,
            string cueId,
            PresentationEventType eventType,
            PresentationAnchorType anchorType,
            bool followAnchor)
        {
            var index = cues.arraySize;
            cues.InsertArrayElementAtIndex(index);
            var cue = cues.GetArrayElementAtIndex(index);
            if (cue == null)
            {
                return null;
            }

            cue.FindPropertyRelative("cueId").stringValue = cueId;
            cue.FindPropertyRelative("eventType").enumValueIndex = (int)eventType;
            cue.FindPropertyRelative("anchorType").enumValueIndex = (int)anchorType;
            cue.FindPropertyRelative("spawnSpace").enumValueIndex = (int)PresentationSpawnSpace.World;
            cue.FindPropertyRelative("anchorChildPath").stringValue = string.Empty;
            cue.FindPropertyRelative("positionOffset").vector3Value = Vector3.zero;
            cue.FindPropertyRelative("rotationOffset").vector3Value = Vector3.zero;
            cue.FindPropertyRelative("filterByEffectType").boolValue = false;
            cue.FindPropertyRelative("effectTypeFilter").enumValueIndex = (int)EffectType.Damage;
            cue.FindPropertyRelative("effectFilter").objectReferenceValue = null;
            cue.FindPropertyRelative("animationTrigger").stringValue = string.Empty;
            cue.FindPropertyRelative("vfxPrefab").objectReferenceValue = null;
            cue.FindPropertyRelative("followAnchor").boolValue = followAnchor;
            cue.FindPropertyRelative("maxLifetime").floatValue = 1.5f;
            cue.FindPropertyRelative("sfx").objectReferenceValue = null;
            cue.FindPropertyRelative("audioBus").enumValueIndex = (int)AudioBusType.Sfx;
            cue.FindPropertyRelative("audioVolume").floatValue = 1f;
            cue.FindPropertyRelative("audioPitch").floatValue = 1f;
            cue.FindPropertyRelative("audioSpatialBlend").floatValue = 1f;
            cue.FindPropertyRelative("worldPosition").vector3Value = Vector3.zero;
            return cue;
        }

        private static string BuildStepIssueBadge(SkillStep step)
        {
            CountStepIssues(step, out var errorCount, out var warningCount);
            if (errorCount > 0)
            {
                return $"[E{errorCount}]";
            }

            if (warningCount > 0)
            {
                return $"[W{warningCount}]";
            }

            return "[OK]";
        }

        private static void CountStepIssues(SkillStep step, out int errorCount, out int warningCount)
        {
            errorCount = 0;
            warningCount = 0;

            if (step == null)
            {
                errorCount++;
                return;
            }

            var hasEffects = step.effects != null && step.effects.Count > 0;
            var hasCuePayload = HasCuePayload(step.presentationCues);
            if (!hasEffects && !hasCuePayload)
            {
                warningCount++;
            }

            if (step.presentationCues != null)
            {
                for (int i = 0; i < step.presentationCues.Count; i++)
                {
                    var cue = step.presentationCues[i];
                    if (cue == null)
                    {
                        warningCount++;
                        continue;
                    }

                    if (!cue.HasPayload)
                    {
                        warningCount++;
                    }

                    if (cue.maxLifetime < 0f)
                    {
                        warningCount++;
                    }
                }
            }

            if (step.effects == null)
            {
                return;
            }

            for (int i = 0; i < step.effects.Count; i++)
            {
                var effect = step.effects[i];
                if (effect == null)
                {
                    errorCount++;
                    continue;
                }

                switch (effect.EffectType)
                {
                    case EffectType.Projectile:
                        if (effect.Projectile == null)
                        {
                            errorCount++;
                        }

                        break;
                    case EffectType.ApplyBuff:
                    case EffectType.RemoveBuff:
                        if (effect.Buff == null)
                        {
                            errorCount++;
                        }

                        break;
                    case EffectType.TriggerSkill:
                        if (effect.TriggeredSkill == null)
                        {
                            warningCount++;
                        }

                        break;
                    case EffectType.Summon:
                        if (effect.SummonPrefab == null && effect.SummonUnit == null)
                        {
                            errorCount++;
                        }

                        break;
                }
            }
        }

        private static bool HasCuePayload(List<SkillPresentationCue> cues)
        {
            if (cues == null || cues.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                if (cue != null && cue.HasPayload)
                {
                    return true;
                }
            }

            return false;
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
            var stepData = selectedSkill != null && selectedSkill.Steps != null && index < selectedSkill.Steps.Count
                ? selectedSkill.Steps[index]
                : null;
            var badge = BuildStepIssueBadge(stepData);
            var label = $"{badge} [{index}] {trigger.enumDisplayNames[trigger.enumValueIndex]}  delay:{delay.floatValue:0.##}  fx:{effects.arraySize}  cue:{cues.arraySize}";
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
