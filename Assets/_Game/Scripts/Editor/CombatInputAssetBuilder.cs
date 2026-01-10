using System.IO;
using CombatSystem.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatSystem.Editor
{
    /// <summary>
    /// 战斗系统输入动作资产生成器。
    /// 通过代码生成 InputActionAsset，确保与 CombatInputIds 常量保持同步。
    /// </summary>
    /// <remarks>
    /// 使用方式：
    /// - 菜单：Combat > Input > Generate Input Actions
    /// - 生成的资产位于 Assets/_Game/Input/CombatInputActions.inputactions
    /// - 如需修改绑定，在此类中修改后重新生成
    /// 
    /// 设计原则：
    /// - 代码优先：避免手动编辑 InputActionAsset，防止与代码常量不同步
    /// - 支持覆盖：已存在资产时会提示确认
    /// </remarks>
    public static class CombatInputAssetBuilder
    {
        #region 常量

        /// <summary>资产存放目录</summary>
        private const string AssetFolder = "Assets/_Game/Input";
        /// <summary>资产完整路径</summary>
        private const string AssetPath = AssetFolder + "/CombatInputActions.inputactions";

        #endregion

        #region 公开方法

        /// <summary>
        /// 生成输入动作资产。
        /// </summary>
        [MenuItem("Combat/Input/Generate Input Actions")]
        public static void GenerateInputActions()
        {
            // 确保目录存在
            EnsureFolder("Assets", "_Game");
            EnsureFolder("Assets/_Game", "Input");

            // 检查是否存在旧资产
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "Overwrite Input Actions?",
                        $"An InputActionAsset already exists at:\n{AssetPath}\n\nOverwrite it?",
                        "Overwrite",
                        "Cancel"))
                {
                    return;
                }

                AssetDatabase.DeleteAsset(AssetPath);
            }

            // 构建并保存资产
            var asset = BuildAsset();
            var json = asset.ToJson();
            File.WriteAllText(AssetPath, json);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

            // 选中新创建的资产
            var imported = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Selection.activeObject = imported != null ? imported : asset;
            EditorUtility.FocusProjectWindow();

            Debug.Log($"[CombatInputAssetBuilder] Input actions created at {AssetPath}");
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 构建完整的 InputActionAsset。
        /// </summary>
        private static InputActionAsset BuildAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();

            // 添加三个 ActionMap
            var gameplay = asset.AddActionMap(CombatInputIds.GameplayMap);
            BuildGameplayMap(gameplay);

            var ui = asset.AddActionMap(CombatInputIds.UIMap);
            BuildUiMap(ui);

            var debug = asset.AddActionMap(CombatInputIds.DebugMap);
            BuildDebugMap(debug);

            return asset;
        }

        /// <summary>
        /// 构建游戏玩法输入映射。
        /// 包含移动、瞄准、技能、取消、暂停等操作。
        /// </summary>
        private static void BuildGameplayMap(InputActionMap map)
        {
            // 移动：WASD + 方向键 + 手柄左摇杆
            var move = map.AddAction(CombatInputIds.Move, InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            // 瞄准点：鼠标/指针位置
            var aimPoint = map.AddAction(CombatInputIds.AimPoint, InputActionType.PassThrough);
            aimPoint.expectedControlType = "Vector2";
            aimPoint.AddBinding("<Pointer>/position");

            // 技能槽位 1-6
            AddSkillAction(map, CombatInputIds.Skill1, "<Keyboard>/digit1");
            AddSkillAction(map, CombatInputIds.Skill2, "<Keyboard>/digit2");
            AddSkillAction(map, CombatInputIds.Skill3, "<Keyboard>/digit3");
            AddSkillAction(map, CombatInputIds.Skill4, "<Keyboard>/digit4");
            AddSkillAction(map, CombatInputIds.Skill5, "<Keyboard>/digit5");
            AddSkillAction(map, CombatInputIds.Skill6, "<Keyboard>/digit6");

            // 取消：右键 + ESC + 手柄 B 键
            var cancel = map.AddAction(CombatInputIds.Cancel, InputActionType.Button);
            cancel.AddBinding("<Mouse>/rightButton");
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");

            // 暂停：ESC + 手柄 Start
            var pause = map.AddAction(CombatInputIds.Pause, InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");

            // 切换技能页：Tab + 手柄 RB
            var switchPage = map.AddAction(CombatInputIds.SwitchPage, InputActionType.Button);
            switchPage.AddBinding("<Keyboard>/tab");
            switchPage.AddBinding("<Gamepad>/rightShoulder");

            // 分页修饰键：Shift + 手柄 LB（反向切换）
            var pageModifier = map.AddAction(CombatInputIds.PageModifier, InputActionType.Button);
            pageModifier.AddBinding("<Keyboard>/leftShift");
            pageModifier.AddBinding("<Keyboard>/rightShift");
            pageModifier.AddBinding("<Gamepad>/leftShoulder");
        }

        /// <summary>
        /// 构建 UI 输入映射。
        /// 包含导航、确认、取消、指针等操作。
        /// </summary>
        private static void BuildUiMap(InputActionMap map)
        {
            // UI 导航
            var navigate = map.AddAction(CombatInputIds.UINavigate, InputActionType.Value);
            navigate.expectedControlType = "Vector2";
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            navigate.AddBinding("<Gamepad>/leftStick");
            navigate.AddBinding("<Gamepad>/dpad");

            // UI 确认
            var submit = map.AddAction(CombatInputIds.UISubmit, InputActionType.Button);
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/space");
            submit.AddBinding("<Gamepad>/buttonSouth");

            // UI 取消
            var cancel = map.AddAction(CombatInputIds.UICancel, InputActionType.Button);
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");

            // 暂停（UI 模式下也需要）
            var pause = map.AddAction(CombatInputIds.Pause, InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");

            // 指针位置
            var point = map.AddAction(CombatInputIds.UIPoint, InputActionType.PassThrough);
            point.expectedControlType = "Vector2";
            point.AddBinding("<Pointer>/position");

            // 点击
            var click = map.AddAction(CombatInputIds.UIClick, InputActionType.Button);
            click.AddBinding("<Pointer>/press");

            // 滚动
            var scroll = map.AddAction(CombatInputIds.UIScroll, InputActionType.PassThrough);
            scroll.expectedControlType = "Vector2";
            scroll.AddBinding("<Mouse>/scroll");
        }

        /// <summary>
        /// 构建调试输入映射。
        /// </summary>
        private static void BuildDebugMap(InputActionMap map)
        {
            // 切换调试覆盖层
            var toggleOverlay = map.AddAction(CombatInputIds.ToggleOverlay, InputActionType.Button);
            toggleOverlay.AddBinding("<Keyboard>/f3");
        }

        /// <summary>
        /// 添加技能动作的辅助方法。
        /// </summary>
        private static void AddSkillAction(InputActionMap map, string actionName, string binding)
        {
            var action = map.AddAction(actionName, InputActionType.Button);
            action.AddBinding(binding);
        }

        /// <summary>
        /// 确保文件夹存在，不存在则创建。
        /// </summary>
        private static string EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }

            return path;
        }

        #endregion
    }
}
