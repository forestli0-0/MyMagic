# Day 4 Milestone: 掉落与商店

Date: 2026-02-07
Phase: Phase C (掉落与装备)
Status: Completed

## Scope
- LootTableDefinition + LootDropper（掉落权重）
- LootPickup（掉落拾取）
- CurrencyComponent（单一货币）
- VendorDefinition + VendorService + Vendor UI（买/卖）
- 掉落与经济首轮联调

## Delivered
- 数据与运行时：
  - `Assets/_Game/Scripts/Data/LootTableDefinition.cs`
  - `Assets/_Game/Scripts/Data/VendorDefinition.cs`
  - `Assets/_Game/Scripts/Gameplay/LootDropper.cs`
  - `Assets/_Game/Scripts/Gameplay/LootPickup.cs`
  - `Assets/_Game/Scripts/Gameplay/CurrencyComponent.cs`
  - `Assets/_Game/Scripts/Gameplay/VendorService.cs`
  - `Assets/_Game/Scripts/Gameplay/VendorTrigger.cs`
- UI 与交互：
  - `Assets/_Game/Scripts/UI/Vendor/VendorScreen.cs`
  - `Assets/_Game/Scripts/UI/Vendor/VendorListUI.cs`
  - `Assets/_Game/Scripts/UI/Vendor/VendorItemSlotUI.cs`
  - 支持按钮买卖、双击买卖、拖拽买卖、选中高亮、详情面板、`Esc/Cancel` 关闭
- 编辑器工具：
  - `Assets/_Game/Scripts/Editor/Day4SetupUtility.cs`
  - 菜单项：
    - `Combat/Day4/Setup Assets (Loot/Vendor/Pickup)`
    - `Combat/Day4/Setup Player Prefab (Currency)`
    - `Combat/Day4/Setup Scene Loot (Health -> LootDropper)`
    - `Combat/Day4/Setup Vendor NPC & UI`

## Validation
- PlayMode 测试覆盖：
  - `Assets/Tests/PlayMode/Day4SystemsTests.cs`
  - 覆盖货币增减、掉落货币条目、商店买卖核心逻辑
- 本轮代码构建：
  - `dotnet build "Combat System.sln" -nologo -v minimal` 通过（0 error）

## Notes
- `Setup Vendor NPC & UI` 会在当前打开场景创建/更新 `VendorNPC`、`VendorService`、`VendorScreen`。
- 该步骤应只在 `Vendor` 场景执行，避免污染 `Town` 场景。

## Next
- 进入 Day 5：任务系统（QuestDefinition / QuestTracker / 触发器 / 存档扩展）。
