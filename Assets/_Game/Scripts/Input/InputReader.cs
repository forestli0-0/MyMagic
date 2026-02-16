using System;
using CombatSystem.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatSystem.Input
{
    /// <summary>
    /// 统一输入读取器，基于 Unity Input System 实现。
    /// 负责读取玩家输入并通过事件分发给各个消费者。
    /// </summary>
    /// <remarks>
    /// 职责：
    /// - 管理 InputActionAsset 的生命周期（启用/禁用 ActionMap）
    /// - 根据 UI 模式切换 Gameplay/UI 输入映射
    /// - 将原始输入转换为事件，避免消费者直接依赖 Input System
    /// 
    /// 使用方式：
    /// - 挂载在场景中的持久对象上
    /// - 其他组件通过订阅事件获取输入
    /// - 支持自动查找 UIManager 以响应输入模式切换
    /// </remarks>
    public class InputReader : MonoBehaviour
    {
        #region 序列化字段

        [Header("Assets")]
        [Tooltip("输入动作资产（由 CombatInputAssetBuilder 生成）")]
        [SerializeField] private InputActionAsset actions;

        [Header("UI Bridge")]
        [Tooltip("UI 管理器引用，用于监听输入模式切换")]
        [SerializeField] private UIManager uiManager;
        [Tooltip("是否自动查找 UIManager")]
        [SerializeField] private bool autoFindUiManager = true;

        #endregion

        #region 公开属性

        /// <summary>当前移动输入向量（归一化）</summary>
        public Vector2 Move { get; private set; }
        /// <summary>当前指针/鼠标屏幕坐标</summary>
        public Vector2 AimPoint { get; private set; }
        /// <summary>当前使用的输入动作资产</summary>
        public InputActionAsset Actions => actions;
        /// <summary>最近一次 Pause 按键触发时的输入模式</summary>
        public UIInputMode LastPauseInputMode { get; private set; } = UIInputMode.Gameplay;
        /// <summary>最近一次在 UI 模式下触发 Pause 的时间（unscaled）</summary>
        public float LastUiPauseTime { get; private set; } = float.NegativeInfinity;

        #endregion

        #region 事件

        /// <summary>移动输入变化时触发</summary>
        public event Action<Vector2> MoveChanged;
        /// <summary>瞄准点变化时触发</summary>
        public event Action<Vector2> AimPointChanged;
        /// <summary>技能键按下时触发（参数为技能槽位索引 0-5）</summary>
        public event Action<int> SkillStarted;
        /// <summary>技能键松开时触发（参数为技能槽位索引 0-5）</summary>
        public event Action<int> SkillCanceled;
        /// <summary>取消键按下时触发（右键/ESC）</summary>
        public event Action CancelPerformed;
        /// <summary>暂停键按下时触发（ESC）</summary>
        public event Action PausePerformed;
        /// <summary>物品栏按下时触发（I）</summary>
        public event Action InventoryPerformed;
        /// <summary>切换技能页时触发（参数为方向：1=下一页，-1=上一页）</summary>
        public event Action<int> SwitchPage;
        /// <summary>切换调试覆盖层时触发（F3）</summary>
        public event Action ToggleOverlay;

        #endregion

        #region 私有字段

        // ActionMap 缓存
        private InputActionMap gameplayMap;
        private InputActionMap uiMap;
        private InputActionMap debugMap;

        // Action 缓存
        private InputAction moveAction;
        private InputAction aimPointAction;
        private InputAction cancelAction;
        private InputAction pauseAction;
        private InputAction pauseActionUi;
        private InputAction inventoryAction;
        private InputAction inventoryActionUi;
        private InputAction switchPageAction;
        private InputAction pageModifierAction;
        private InputAction toggleOverlayAction;
        private InputAction[] skillActions;

        // 状态标记
        private bool initialized;
        private bool callbacksBound;

        // 技能动作名称列表
        private static readonly string[] SkillActionNames =
        {
            CombatInputIds.Skill1,
            CombatInputIds.Skill2,
            CombatInputIds.Skill3,
            CombatInputIds.Skill4,
            CombatInputIds.Skill5,
            CombatInputIds.Skill6
        };

        #endregion

        /// <summary>检查分页修饰键是否按下（Shift）</summary>
        private bool IsPageModifierHeld => pageModifierAction != null && pageModifierAction.IsPressed();

        #region Unity 生命周期

        /// <summary>
        /// 初始化时自动查找 UIManager。
        /// </summary>
        private void Awake()
        {
            if (autoFindUiManager && uiManager == null)
            {
                uiManager = UIRoot.Instance != null ? UIRoot.Instance.Manager : FindFirstObjectByType<UIManager>();
            }
        }

        /// <summary>
        /// 启用时初始化 Action 并绑定回调。
        /// </summary>
        private void OnEnable()
        {
            if (!initialized)
            {
                Initialize();
            }

            if (initialized && !callbacksBound)
            {
                BindCallbacks();
            }

            if (uiManager != null)
            {
                uiManager.InputModeChanged += HandleInputModeChanged;
                EnableForMode(uiManager.CurrentInputMode);
            }
            else
            {
                EnableForMode(UIInputMode.Gameplay);
            }
        }

        /// <summary>
        /// 禁用时取消 UI 事件订阅并禁用所有 ActionMap。
        /// </summary>
        private void OnDisable()
        {
            if (uiManager != null)
            {
                uiManager.InputModeChanged -= HandleInputModeChanged;
            }

            DisableAllMaps();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 从 InputActionAsset 解析所有 ActionMap 和 Action 引用。
        /// </summary>
        private void Initialize()
        {
            if (actions == null)
            {
                Debug.LogWarning("[InputReader] InputActionAsset is not assigned.", this);
                return;
            }

            gameplayMap = actions.FindActionMap(CombatInputIds.GameplayMap, false);
            uiMap = actions.FindActionMap(CombatInputIds.UIMap, false);
            debugMap = actions.FindActionMap(CombatInputIds.DebugMap, false);

            if (gameplayMap == null)
            {
                Debug.LogWarning("[InputReader] Gameplay map not found in InputActionAsset.", this);
                return;
            }

            moveAction = gameplayMap.FindAction(CombatInputIds.Move, false);
            aimPointAction = gameplayMap.FindAction(CombatInputIds.AimPoint, false);
            cancelAction = gameplayMap.FindAction(CombatInputIds.Cancel, false);
            pauseAction = gameplayMap.FindAction(CombatInputIds.Pause, false);
            inventoryAction = gameplayMap.FindAction(CombatInputIds.Inventory, false);
            switchPageAction = gameplayMap.FindAction(CombatInputIds.SwitchPage, false);
            pageModifierAction = gameplayMap.FindAction(CombatInputIds.PageModifier, false);

            skillActions = new InputAction[SkillActionNames.Length];
            for (var i = 0; i < SkillActionNames.Length; i++)
            {
                skillActions[i] = gameplayMap.FindAction(SkillActionNames[i], false);
            }

            if (debugMap != null)
            {
                toggleOverlayAction = debugMap.FindAction(CombatInputIds.ToggleOverlay, false);
            }

            if (uiMap != null)
            {
                pauseActionUi = uiMap.FindAction(CombatInputIds.Pause, false);
                inventoryActionUi = uiMap.FindAction(CombatInputIds.Inventory, false);
            }

            initialized = true;
        }

        public void SetActions(InputActionAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            if (actions == asset && initialized)
            {
                return;
            }

            actions = asset;
            initialized = false;
            callbacksBound = false;

            if (!isActiveAndEnabled)
            {
                return;
            }

            Initialize();
            if (initialized && !callbacksBound)
            {
                BindCallbacks();
            }

            var mode = uiManager != null ? uiManager.CurrentInputMode : UIInputMode.Gameplay;
            EnableForMode(mode);
        }

        /// <summary>
        /// 绑定所有输入回调。仅在首次启用时执行。
        /// </summary>
        private void BindCallbacks()
        {
            if (moveAction != null)
            {
                moveAction.performed += HandleMove;
                moveAction.canceled += HandleMoveCanceled;
            }

            if (aimPointAction != null)
            {
                aimPointAction.performed += HandleAimPoint;
            }

            if (cancelAction != null)
            {
                cancelAction.performed += HandleCancel;
            }

            if (pauseAction != null)
            {
                pauseAction.performed += HandlePause;
            }

            if (pauseActionUi != null && pauseActionUi != pauseAction)
            {
                pauseActionUi.performed += HandlePause;
            }

            if (inventoryAction != null)
            {
                inventoryAction.performed += HandleInventory;
            }

            if (inventoryActionUi != null && inventoryActionUi != inventoryAction)
            {
                inventoryActionUi.performed += HandleInventory;
            }

            if (switchPageAction != null)
            {
                switchPageAction.performed += HandleSwitchPage;
            }

            if (toggleOverlayAction != null)
            {
                toggleOverlayAction.performed += HandleToggleOverlay;
            }

            if (skillActions != null)
            {
                for (var i = 0; i < skillActions.Length; i++)
                {
                    var action = skillActions[i];
                    if (action == null)
                    {
                        continue;
                    }

                    var index = i;
                    action.started += ctx => OnSkillStarted(index);
                    action.canceled += ctx => OnSkillCanceled(index);
                }
            }

            callbacksBound = true;
        }

        /// <summary>
        /// 技能键按下回调。
        /// </summary>
        private void OnSkillStarted(int index)
        {
            SkillStarted?.Invoke(index);
        }

        /// <summary>
        /// 技能键松开回调。
        /// </summary>
        private void OnSkillCanceled(int index)
        {
            SkillCanceled?.Invoke(index);
        }

        #endregion

        #region 输入模式切换

        /// <summary>
        /// 根据 UI 输入模式启用/禁用对应的 ActionMap。
        /// </summary>
        /// <param name="mode">目标输入模式</param>

        private void EnableForMode(UIInputMode mode)
        {
            if (mode == UIInputMode.UI)
            {
                gameplayMap?.Disable();
                uiMap?.Enable();
                UpdateMove(Vector2.zero);
            }
            else
            {
                uiMap?.Disable();
                gameplayMap?.Enable();
                SyncGameplayState();
            }

            debugMap?.Enable();
        }

        /// <summary>
        /// 禁用所有 ActionMap。
        /// </summary>
        private void DisableAllMaps()
        {
            gameplayMap?.Disable();
            uiMap?.Disable();
            debugMap?.Disable();
        }

        /// <summary>
        /// UI 输入模式变化回调。
        /// </summary>
        private void HandleInputModeChanged(UIInputMode mode)
        {
            EnableForMode(mode);
        }

        #endregion

        #region 输入回调处理

        /// <summary>
        /// 移动输入回调。
        /// </summary>

        private void HandleMove(InputAction.CallbackContext context)
        {
            UpdateMove(context.ReadValue<Vector2>());
        }

        /// <summary>
        /// 移动输入取消回调。
        /// </summary>
        private void HandleMoveCanceled(InputAction.CallbackContext context)
        {
            UpdateMove(Vector2.zero);
        }

        /// <summary>
        /// 更新移动状态并触发事件。
        /// </summary>

        private void UpdateMove(Vector2 value)
        {
            Move = value;
            MoveChanged?.Invoke(value);
        }

        /// <summary>
        /// 切换到 Gameplay 模式时同步当前输入状态。
        /// </summary>
        private void SyncGameplayState()
        {
            if (moveAction != null)
            {
                UpdateMove(moveAction.ReadValue<Vector2>());
            }

            if (aimPointAction != null)
            {
                AimPoint = aimPointAction.ReadValue<Vector2>();
                AimPointChanged?.Invoke(AimPoint);
            }
        }

        private void HandleAimPoint(InputAction.CallbackContext context)
        {
            AimPoint = context.ReadValue<Vector2>();
            AimPointChanged?.Invoke(AimPoint);
        }

        private void HandleCancel(InputAction.CallbackContext context)
        {
            CancelPerformed?.Invoke();
        }

        private void HandlePause(InputAction.CallbackContext context)
        {
            var mode = ResolveCurrentInputMode();
            LastPauseInputMode = mode;
            if (mode == UIInputMode.UI)
            {
                LastUiPauseTime = Time.unscaledTime;
            }

            PausePerformed?.Invoke();
        }

        private void HandleInventory(InputAction.CallbackContext context)
        {
            InventoryPerformed?.Invoke();
        }

        /// <summary>
        /// 切换技能页回调。根据 Shift 键决定方向。
        /// </summary>
        private void HandleSwitchPage(InputAction.CallbackContext context)
        {
            var delta = IsPageModifierHeld ? -1 : 1;
            SwitchPage?.Invoke(delta);
        }

        /// <summary>
        /// 切换调试覆盖层回调。
        /// </summary>
        private void HandleToggleOverlay(InputAction.CallbackContext context)
        {
            ToggleOverlay?.Invoke();
        }

        private UIInputMode ResolveCurrentInputMode()
        {
            if (uiManager != null)
            {
                return uiManager.CurrentInputMode;
            }

            if (uiMap != null && uiMap.enabled && (gameplayMap == null || !gameplayMap.enabled))
            {
                return UIInputMode.UI;
            }

            return UIInputMode.Gameplay;
        }

        #endregion
    }
}
