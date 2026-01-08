using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CombatSystem.Core;
using CombatSystem.Data;
using CombatSystem.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CombatSystem.EditorTools
{
    /// <summary>
    /// 控制系统编辑器测试运行器。
    /// </summary>
    /// <remarks>
    /// 提供一键运行控制系统单元测试的功能，覆盖以下核心机制：
    /// - ControlRules 控制规则映射正确性
    /// - Tenacity 韧性减少控制持续时间
    /// - Immunity 免疫阻止控制效果
    /// - Cleanse 净化驱散逻辑
    /// - Interrupt 施法打断
    /// 
    /// 使用方式：
    /// Unity 编辑器菜单 Tools > Combat > Run Control System Tests
    /// 
    /// 测试结果将输出到 Console 并保存至 Logs/ 目录。
    /// </remarks>
    public static class CombatControlSystemTestRunner
    {
        private const string MenuPath = "Tools/Combat/Run Control System Tests";

        /// <summary>
        /// 运行所有控制系统测试。
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Run()
        {
            var report = new List<string>();
            var pass = 0;
            var fail = 0;

            GameObject caster = null;

            try
            {
                ForceImportAssets();
                caster = CreateTestUnit("ControlTest_Caster");

                var stats = caster.GetComponent<StatsComponent>();
                var health = caster.GetComponent<HealthComponent>();
                var resource = caster.GetComponent<ResourceComponent>();
                var cooldown = caster.GetComponent<CooldownComponent>();
                var buffs = caster.GetComponent<BuffController>();
                var skillUser = caster.GetComponent<SkillUserComponent>();

                health.Initialize();
                resource.Initialize();

                var buffStun = LoadAsset<BuffDefinition>("Assets/_Game/ScriptableObjects/Buffs/Buff_TestStun.asset");
                var buffRoot = LoadAsset<BuffDefinition>("Assets/_Game/ScriptableObjects/Buffs/Buff_TestRoot.asset");
                var buffSuppression = LoadAsset<BuffDefinition>("Assets/_Game/ScriptableObjects/Buffs/Buff_TestSuppression.asset");
                var buffStoneSkin = LoadAsset<BuffDefinition>("Assets/_Game/ScriptableObjects/Buffs/Buff_StoneSkin.asset");
                var skillMagicWard = LoadAsset<SkillDefinition>("Assets/_Game/ScriptableObjects/Skills/Skill_MagicWard.asset");
                var statTenacity = LoadAsset<StatDefinition>("Assets/_Game/ScriptableObjects/Stats/Stat_Tenacity.asset");

                if (!CheckAssets(report, ref pass, ref fail, buffStun, buffRoot, buffSuppression, buffStoneSkin, skillMagicWard, statTenacity))
                {
                    DumpReport(report, pass, fail);
                    return;
                }

                LogBuffDefinition(report, "Buff_TestStun", buffStun);
                LogBuffDefinition(report, "Buff_TestRoot", buffRoot);
                LogBuffDefinition(report, "Buff_TestSuppression", buffSuppression);
                LogBuffDefinition(report, "Buff_StoneSkin", buffStoneSkin);
                LogInfo(report, $"Tenacity affectable: Stun={ControlRules.IsTenacityAffectable(ControlType.Stun)}, Root={ControlRules.IsTenacityAffectable(ControlType.Root)}, Suppression={ControlRules.IsTenacityAffectable(ControlType.Suppression)}");
                LogInfo(report, $"Cleanseable: Stun={ControlRules.IsCleanseable(ControlType.Stun)}, Root={ControlRules.IsCleanseable(ControlType.Root)}, Suppression={ControlRules.IsCleanseable(ControlType.Suppression)}");

                // ControlRules mapping checks.
                ExpectFlag(report, ref pass, ref fail, ControlType.Stun, ControlFlag.BlocksCasting, true, "Stun blocks casting");
                ExpectFlag(report, ref pass, ref fail, ControlType.Root, ControlFlag.BlocksMovement, true, "Root blocks movement");
                ExpectFlag(report, ref pass, ref fail, ControlType.Root, ControlFlag.BlocksCasting, false, "Root does not block casting");
                ExpectFlag(report, ref pass, ref fail, ControlType.Silence, ControlFlag.InterruptsCasting, true, "Silence interrupts casting");
                ExpectFlag(report, ref pass, ref fail, ControlType.Disarm, ControlFlag.BlocksBasicAttack, true, "Disarm blocks basic attacks");
                ExpectFlag(report, ref pass, ref fail, ControlType.Slow, ControlFlag.BlocksMovement, false, "Slow does not block movement");

                // Tenacity tests.
                stats.SetValue(statTenacity, 0.5f);
                ApplySingleBuff(buffs, buffStun);
                LogInfo(report, $"Active buffs after stun: {GetActiveBuffSummary(buffs)}");
                var stunDuration = GetBuffRemainingDuration(buffs, buffStun);
                ExpectApprox(report, ref pass, ref fail, stunDuration, buffStun.Duration * 0.5f, 0.05f, "Tenacity reduces stun duration");
                buffs.RemoveBuff(buffStun);

                ApplySingleBuff(buffs, buffSuppression);
                LogInfo(report, $"Active buffs after suppression: {GetActiveBuffSummary(buffs)}");
                var suppressionDuration = GetBuffRemainingDuration(buffs, buffSuppression);
                LogInfo(report, $"Suppression duration (expected {buffSuppression.Duration:0.00}): {suppressionDuration:0.00}");
                ExpectApprox(report, ref pass, ref fail, suppressionDuration, buffSuppression.Duration, 0.05f, "Tenacity does not reduce suppression");
                buffs.RemoveBuff(buffSuppression);

                // Immunity tests.
                buffs.ApplyBuff(buffStoneSkin);
                buffs.ApplyBuff(buffStun);
                Expect(report, ref pass, ref fail, !buffs.HasControlFlag(ControlFlag.BlocksCasting), "Stun is ignored with immunity");
                buffs.RemoveBuff(buffStun);
                buffs.RemoveBuff(buffStoneSkin);

                // Cleanse tests.
                buffs.ApplyBuff(buffStun);
                buffs.ApplyBuff(buffSuppression);
                LogInfo(report, $"Active buffs before cleanse: {GetActiveBuffSummary(buffs)}");
                var cleanseRemoved = buffs.Cleanse(false, true, true, null);
                LogInfo(report, $"Cleanse removed count: {cleanseRemoved}");
                LogInfo(report, $"Active buffs after cleanse: {GetActiveBuffSummary(buffs)}");
                Expect(report, ref pass, ref fail, !buffs.HasControl(ControlType.Stun), "Cleanse removes stun");
                Expect(report, ref pass, ref fail, buffs.HasControl(ControlType.Suppression), "Cleanse does not remove suppression");
                buffs.RemoveBuff(buffSuppression);

                // Interrupt tests.
                var interrupted = false;
                skillUser.SkillCastInterrupted += _ => interrupted = true;
                var castStarted = skillUser.TryCast(skillMagicWard, caster);
                Expect(report, ref pass, ref fail, castStarted, "Cast starts for interrupt test");
                buffs.ApplyBuff(buffStun);
                Expect(report, ref pass, ref fail, interrupted, "Stun interrupts casting");
                Expect(report, ref pass, ref fail, !skillUser.IsCasting, "Casting stops after interrupt");
                buffs.RemoveBuff(buffStun);

                // Sanity check: root blocks movement flag, but does not block casting.
                buffs.ApplyBuff(buffRoot);
                LogInfo(report, $"Active buffs after root: {GetActiveBuffSummary(buffs)}");
                LogInfo(report, $"Active control flags after root: {GetActiveFlagSummary(buffs)}");
                Expect(report, ref pass, ref fail, buffs.HasControlFlag(ControlFlag.BlocksMovement), "Root blocks movement flag");
                Expect(report, ref pass, ref fail, !buffs.HasControlFlag(ControlFlag.BlocksCasting), "Root does not block casting flag");
                buffs.RemoveBuff(buffRoot);
            }
            finally
            {
                if (caster != null)
                {
                    UnityEngine.Object.DestroyImmediate(caster);
                }
            }

            DumpReport(report, pass, fail);
        }

        /// <summary>
        /// 创建用于测试的临时单位对象。
        /// </summary>
        /// <param name="name">单位名称</param>
        /// <returns>配置好组件的测试单位</returns>
        private static GameObject CreateTestUnit(string name)
        {
            var go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;

            go.AddComponent<UnitRoot>();
            go.AddComponent<StatsComponent>();
            go.AddComponent<HealthComponent>();
            go.AddComponent<ResourceComponent>();
            go.AddComponent<CooldownComponent>();
            go.AddComponent<UnitTagsComponent>();
            go.AddComponent<TeamComponent>();
            go.AddComponent<BuffController>();
            go.AddComponent<SkillUserComponent>();

            InvokeReset(go.GetComponent<StatsComponent>());
            InvokeReset(go.GetComponent<HealthComponent>());
            InvokeReset(go.GetComponent<ResourceComponent>());
            InvokeReset(go.GetComponent<CooldownComponent>());
            InvokeReset(go.GetComponent<BuffController>());
            InvokeReset(go.GetComponent<SkillUserComponent>());

            return go;
        }

        /// <summary>
        /// 通过反射调用组件的 Reset 方法进行初始化。
        /// </summary>
        private static void InvokeReset(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            var method = target.GetType().GetMethod("Reset", BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        /// <summary>
        /// 从指定路径加载资源。
        /// </summary>
        private static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        /// <summary>
        /// 强制重新导入测试所需的资源。
        /// </summary>
        private static void ForceImportAssets()
        {
            var paths = new[]
            {
                "Assets/_Game/ScriptableObjects/Buffs/Buff_TestStun.asset",
                "Assets/_Game/ScriptableObjects/Buffs/Buff_TestRoot.asset",
                "Assets/_Game/ScriptableObjects/Buffs/Buff_TestSuppression.asset",
                "Assets/_Game/ScriptableObjects/Buffs/Buff_StoneSkin.asset",
                "Assets/_Game/ScriptableObjects/Skills/Skill_MagicWard.asset",
                "Assets/_Game/ScriptableObjects/Stats/Stat_Tenacity.asset"
            };

            for (int i = 0; i < paths.Length; i++)
            {
                AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceUpdate);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// 检查必需资源是否全部加载成功。
        /// </summary>
        private static bool CheckAssets(List<string> report, ref int pass, ref int fail, params UnityEngine.Object[] assets)
        {
            var ok = true;
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] != null)
                {
                    continue;
                }

                ok = false;
                Fail(report, ref fail, "Required asset missing");
            }

            if (ok)
            {
                Pass(report, ref pass, "Required assets loaded");
            }

            return ok;
        }

        /// <summary>
        /// 应用单个 Buff（先移除再添加，确保干净状态）。
        /// </summary>
        private static void ApplySingleBuff(BuffController buffs, BuffDefinition buff)
        {
            buffs.RemoveBuff(buff);
            buffs.ApplyBuff(buff);
        }

        private static float GetBuffRemainingDuration(BuffController buffs, BuffDefinition buff)
        {
            var list = GetActiveBuffList(buffs);
            if (list == null)
            {
                return 0f;
            }

            var now = Time.time;
            for (int i = 0; i < list.Count; i++)
            {
                var instance = list[i];
                var def = GetBuffInstanceField<BuffDefinition>(instance, "Definition");
                if (def != buff)
                {
                    continue;
                }

                var endTime = GetBuffInstanceField<float>(instance, "EndTime");
                if (endTime < 0f)
                {
                    return -1f;
                }

                return Mathf.Max(0f, endTime - now);
            }

            return 0f;
        }

        private static IList GetActiveBuffList(BuffController buffs)
        {
            var field = typeof(BuffController).GetField("activeBuffs", BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(buffs) as IList;
        }

        private static T GetBuffInstanceField<T>(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName);
            if (field == null)
            {
                return default;
            }

            return (T)field.GetValue(instance);
        }

        /// <summary>
        /// 验证控制类型是否具有指定标记。
        /// </summary>
        private static void ExpectFlag(List<string> report, ref int pass, ref int fail, ControlType type, ControlFlag flag, bool expected, string label)
        {
            var actual = ControlRules.HasFlag(type, flag);
            Expect(report, ref pass, ref fail, actual == expected, label);
        }

        /// <summary>
        /// 验证浮点数是否在允许误差范围内。
        /// </summary>
        private static void ExpectApprox(List<string> report, ref int pass, ref int fail, float actual, float expected, float tolerance, string label)
        {
            var ok = Mathf.Abs(actual - expected) <= tolerance;
            Expect(report, ref pass, ref fail, ok, $"{label} (expected {expected:0.00}, got {actual:0.00})");
        }

        /// <summary>
        /// 通用断言方法，根据条件记录通过或失败。
        /// </summary>
        private static void Expect(List<string> report, ref int pass, ref int fail, bool condition, string label)
        {
            if (condition)
            {
                Pass(report, ref pass, label);
            }
            else
            {
                Fail(report, ref fail, label);
            }
        }

        /// <summary>
        /// 记录测试通过。
        /// </summary>
        private static void Pass(List<string> report, ref int pass, string message)
        {
            pass++;
            report.Add($"PASS: {message}");
        }

        /// <summary>
        /// 记录测试失败。
        /// </summary>
        private static void Fail(List<string> report, ref int fail, string message)
        {
            fail++;
            report.Add($"FAIL: {message}");
        }

        /// <summary>
        /// 输出测试报告到 Console 并保存到文件。
        /// </summary>
        private static void DumpReport(IReadOnlyList<string> report, int pass, int fail)
        {
            Debug.Log($"[ControlSystemTests] Completed. Pass: {pass}, Fail: {fail}");
            for (int i = 0; i < report.Count; i++)
            {
                Debug.Log($"[ControlSystemTests] {report[i]}");
            }

            var logPath = WriteReportToFile(report, pass, fail);
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                Debug.Log($"[ControlSystemTests] Log saved to: {logPath}");
            }
        }

        /// <summary>
        /// 将测试报告写入文件。
        /// </summary>
        /// <returns>日志文件路径，失败返回空字符串</returns>
        private static string WriteReportToFile(IReadOnlyList<string> report, int pass, int fail)
        {
            try
            {
                var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var logDir = Path.Combine(root, "Logs");
                Directory.CreateDirectory(logDir);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var logPath = Path.Combine(logDir, $"ControlSystemTests_{timestamp}.log");

                using (var writer = new StreamWriter(logPath, false))
                {
                    writer.WriteLine($"Control System Tests - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"Pass: {pass}, Fail: {fail}");
                    writer.WriteLine();
                    for (int i = 0; i < report.Count; i++)
                    {
                        writer.WriteLine(report[i]);
                    }
                }

                return logPath;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ControlSystemTests] Failed to write log: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 记录信息日志。
        /// </summary>
        private static void LogInfo(List<string> report, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            report.Add($"INFO: {message}");
        }

        /// <summary>
        /// 记录 Buff 定义的详细信息。
        /// </summary>
        private static void LogBuffDefinition(List<string> report, string label, BuffDefinition buff)
        {
            if (buff == null)
            {
                LogInfo(report, $"{label}: null");
                return;
            }

            var controls = FormatControlList(buff.ControlEffects);
            var immunities = FormatControlList(buff.ControlImmunities);
            var flags = FormatControlFlags(buff.ControlEffects);
            var info = $"{label}: name={buff.name}, id={buff.Id}, debuff={buff.IsDebuff}, dispellable={buff.Dispellable}, duration={buff.Duration:0.00}, stacking={buff.StackingRule}, maxStacks={buff.MaxStacks}, controls=[{controls}], immunities=[{immunities}], flags=[{flags}]";
            LogInfo(report, info);
        }

        /// <summary>
        /// 格式化控制类型列表为字符串。
        /// </summary>
        private static string FormatControlList(IReadOnlyList<ControlType> controls)
        {
            if (controls == null || controls.Count == 0)
            {
                return "None";
            }

            var values = new List<string>(controls.Count);
            for (int i = 0; i < controls.Count; i++)
            {
                values.Add(controls[i].ToString());
            }

            return string.Join(", ", values);
        }

        /// <summary>
        /// 格式化控制标记信息（用于调试输出）。
        /// </summary>
        private static string FormatControlFlags(IReadOnlyList<ControlType> controls)
        {
            if (controls == null || controls.Count == 0)
            {
                return "None";
            }

            var entries = new List<string>();
            for (int i = 0; i < controls.Count; i++)
            {
                var control = controls[i];
                if (control == ControlType.All)
                {
                    continue;
                }

                entries.Add($"{control}:{FormatFlagList(control)}");
            }

            return entries.Count > 0 ? string.Join(" | ", entries) : "None";
        }

        private static string FormatFlagList(ControlType control)
        {
            var flags = new List<string>();
            foreach (ControlFlag flag in Enum.GetValues(typeof(ControlFlag)))
            {
                if (flag == ControlFlag.None)
                {
                    continue;
                }

                if (ControlRules.HasFlag(control, flag))
                {
                    flags.Add(flag.ToString());
                }
            }

            return flags.Count > 0 ? string.Join(",", flags) : "None";
        }

        /// <summary>
        /// 获取当前激活的控制标记摘要。
        /// </summary>
        private static string GetActiveFlagSummary(BuffController buffs)
        {
            if (buffs == null)
            {
                return "None";
            }

            var flags = new List<string>();
            foreach (ControlFlag flag in Enum.GetValues(typeof(ControlFlag)))
            {
                if (flag == ControlFlag.None)
                {
                    continue;
                }

                if (buffs.HasControlFlag(flag))
                {
                    flags.Add(flag.ToString());
                }
            }

            return flags.Count > 0 ? string.Join(", ", flags) : "None";
        }

        /// <summary>
        /// 获取当前激活 Buff 的详细摘要。
        /// </summary>
        private static string GetActiveBuffSummary(BuffController buffs)
        {
            var list = GetActiveBuffList(buffs);
            if (list == null || list.Count == 0)
            {
                return "None";
            }

            var entries = new List<string>(list.Count);
            var now = Time.time;
            for (int i = 0; i < list.Count; i++)
            {
                var instance = list[i];
                var def = GetBuffInstanceField<BuffDefinition>(instance, "Definition");
                var stacks = GetBuffInstanceField<int>(instance, "Stacks");
                var endTime = GetBuffInstanceField<float>(instance, "EndTime");
                var remaining = endTime > 0f ? Mathf.Max(0f, endTime - now).ToString("0.00") : "permanent";
                var name = def != null ? def.name : "null";
                var controls = def != null ? FormatControlList(def.ControlEffects) : "n/a";
                var debuff = def != null ? def.IsDebuff.ToString() : "n/a";
                var dispellable = def != null ? def.Dispellable.ToString() : "n/a";
                entries.Add($"{name} (stacks {stacks}, remaining {remaining}, debuff {debuff}, dispellable {dispellable}, controls [{controls}])");
            }

            return string.Join(" | ", entries);
        }
    }
}
