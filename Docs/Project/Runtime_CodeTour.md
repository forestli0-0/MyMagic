# Runtime Code Tour（第一轮）

这份文档用于“边清理边熟悉代码”，只覆盖运行时主链路，不讲美术与资源导入细节。

## 1. 先看哪些文件（推荐顺序）
1. `Assets/_Game/Scripts/Gameplay/LevelFlowController.cs`
2. `Assets/_Game/Scripts/UI/Core/UIRoot.cs`
3. `Assets/_Game/Scripts/UI/Core/UIManager.cs`
4. `Assets/_Game/Scripts/Gameplay/SkillUserComponent.cs`
5. `Assets/_Game/Scripts/Persistence/SaveGameManager.cs`
6. `Assets/_Game/Scripts/UI/Inventory/InventoryScreen.cs`
7. `Assets/_Game/Scripts/UI/Character/CharacterScreen.cs`

## 2. 场景与玩家生命周期
- 场景切换入口在 `LevelFlowController.StartNewGame/LoadLevel`。
- 实际加载在 `LoadLevelInternal`，会决定是否缓存当前玩家状态。
- 场景加载完成后走 `HandleSceneLoaded`：
  - 解析关卡定义
  - 找/生玩家
  - 放到出生点
  - 应用缓存或读档状态
  - 确保 InGame UI 与死亡流程

核心价值：
- 把“场景流转 + 玩家跨场景状态”放在一个控制器内，避免每个场景各自拼接逻辑。

## 3. UI 分层与输入模式
- `UIRoot` 负责 4 层 Canvas（Screen/HUD/Modal/Overlay）和合并重复 UIRoot。
- `UIManager` 维护 Screen/Modal 栈，控制输入模式（Gameplay/UI）以及 HUD 可见策略。
- 菜单热键：
  - `GameplayMenuHotkey`：Tab/Esc/方向键/手柄页签切换。
  - `QuestJournalHotkey`：J 打开任务日志。

本轮清理点：
- 新增 `Assets/_Game/Scripts/UI/Core/UIHotkeyUtility.cs`
  - 统一“解析 UIManager”与“兜底找 Gameplay Screen”逻辑
  - 减少热键脚本重复代码，便于后续维护

## 4. 战斗与技能主链路
- `SkillUserComponent.TryCast` 是技能释放总入口。
- 释放流程：校验 -> 目标收集 -> 扣资源/进冷却 -> 调度 SkillStep -> 执行 Effect。
- 冷却展示由 `SkillBarUI` 读 `CooldownComponent` 并刷新槽位。

阅读要点：
- 这是高频路径，改动必须谨慎控制分配与事件频率。
- UI 抖动/刷新异常通常是“事件触发过密 + 视觉组件全量重建”导致。

## 5. 存档链路
- `SaveGameManager.Capture`：抓取玩家与任务状态。
- `SaveGameManager.Apply`：按“背包 -> 装备 -> 技能 -> 任务”顺序回放状态。
- `LevelFlowController` 与 `SaveGameManager` 通过场景名、出生点、玩家快照协作。

## 6. 目前高优先清理区
- `InventoryScreen`（体量大，UI/交互/数据拼在一起）
- `CharacterScreen`（展示逻辑较多，建议拆分子面板绑定器）
- `SkillUserComponent`（职责多，后续可拆“施法状态机/步骤调度”）
- `LevelFlowController`（继续拆分“玩家生成”“状态缓存”“UI接管”）

## 7. 下一轮建议（不改玩法）
1. 拆 `InventoryScreen` 的“筛选/排序/槽位拖拽/详情面板”4 个子控制器。
2. 给 `SkillUserComponent` 增加更清晰的内部阶段注释与统计埋点开关。
3. 建立统一 Runtime Debug 开关（避免分散日志污染线上体验）。

## 8. 第二轮已完成（Inventory 拆分）
- `InventoryScreen` 已改为 `partial`，减少单文件“超长+多职责”问题。
- 新增 `Assets/_Game/Scripts/UI/Inventory/InventoryScreen.Sorting.cs`：
  - 负责筛选、搜索、排序、稀有度筛选、排序弹层（Sort Picker）逻辑。
  - 包括 `BuildFilteredDisplayIndices`、`CompareFilteredItemIndex`、`CompareOrganizeItems` 等核心方法。
- 新增 `Assets/_Game/Scripts/UI/Inventory/DropdownOverlayFix.cs`：
  - 将 Dropdown 运行时层级修正组件独立，避免混在 `InventoryScreen.cs` 末尾。

拆分后的阅读顺序（Inventory）：
1. `InventoryScreen.cs`：入口、订阅、选择/拖拽主流程
2. `InventoryScreen.Sorting.cs`：筛选/排序/搜索与排序弹层
3. `InventoryGridUI.cs` + `EquipmentPanelUI.cs`：格子与装备位渲染交互
