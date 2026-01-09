# Unity Project Guidelines: Playable Action RPG (Diablo2-like)

## 1. Role & Goal
你是 Unity 高级架构师。当前目标已从“战斗系统 Demo”转为“可完整游玩的 ARPG”，核心体验对标暗黑2：城镇 -> 野外/地城 -> 任务 -> Boss -> 掉落 -> 回城 -> 继续推进。重点是系统化、可拓展、可持续迭代。

## 2. Core Architectural Rules (STRICTLY FOLLOW)
* **Data-Driven:** 所有静态配置（技能、BUFF、掉落、关卡、任务、物品）必须使用 `ScriptableObject`。MonoBehaviour 只做运行时逻辑。
* **Composition Over Inheritance:** 避免深继承，用接口 + 组件组合。
* **Event-Driven Communication:** UI/系统订阅事件，不直接依赖玩家对象。
* **Zero GC in Hot Paths:** Update/战斗循环无分配；投射物/特效/飘字/掉落物全部池化。
* **SOLID:** 单一职责，避免巨型类。
* **Industry-Grade Design:** 系统设计需遵循成熟行业规范，禁止“小作坊式”的补丁式修改；任何改动需有明确设计依据、可扩展性与一致性。

## 3. Tech Stack
* Unity 2022.3 LTS (URP)
* C# (Latest supported)
* Odin Inspector (可选)
* DoTween (UI/镜头动画)

## 4. Folder Structure
Scripts: `Assets/_Game/Scripts/`
ScriptableObjects: `Assets/_Game/ScriptableObjects/`

---

# Playable Game Design Spec (CN)

## 1. 当前完成状态（已具备）
- 数据驱动技能/BUFF/目标/效果/修正器体系
- 事件总线、核心战斗组件、基础 AI FSM
- HUD/UI 框架（Screen/Modal/Overlay/HUD）
- 示例生成工具与样例场景

## 2. 新目标：完整可游玩 ARPG 体验
- **核心循环**：城镇 -> 任务/指引 -> 多段地图推进 -> 精英/小Boss -> 大Boss -> 结算/掉落 -> 回城 -> 继续推进
- **玩家成长**：等级、属性点、技能解锁/升级、装备成长
- **装备与掉落**：稀有度、词缀、随机掉落、鉴定/分解/售卖
- **关卡推进**：Act 概念、多张地图、Boss 关卡、传送点/存档点
- **任务系统**：主线 + 支线；奖励物品/经验/解锁功能
- **经济系统**：货币、商店、修理/购买/出售
- **体验完善**：镜头、音效、反馈、难度曲线与数值平衡

## 3. 数据驱动配置清单 (ScriptableObject)
- **GameDatabase**: 集中注册与运行时索引
- **Combat**: Stat/Tag/Skill/Buff/Effect/Condition/Modifier/Projectile/Targeting
- **Content**:
  - ItemDefinition: 物品基础数据
  - AffixDefinition: 词缀/随机属性
  - LootTableDefinition: 掉落表/掉落权重
  - QuestDefinition: 任务目标与奖励
  - LevelDefinition: 关卡/地图配置
  - EncounterDefinition: 怪群/刷怪配置
  - VendorDefinition: 商店售卖清单
  - ProgressionDefinition: 等级曲线/属性成长

## 4. 运行时核心组件 (新增与扩展)
- **PlayerProgression**: 等级/经验/属性点/技能解锁
- **InventoryComponent**: 背包、堆叠、占格
- **EquipmentComponent**: 装备槽位与属性加成
- **LootDropper**: 掉落生成与拾取逻辑
- **QuestTracker**: 任务状态与事件监听
- **LevelFlowController**: 地图切换、出生点、Boss 结算
- **VendorService**: 商店交易
- **SaveService**: 角色/进度/物品/任务存档

## 5. 输入与控制（需要整理）
- 现有输入直接调用 `Input.*`，需抽象为统一输入层（便于手柄/键鼠/可重绑）。
- 目标：使用Input System 统一对接项目所有输入。

## 6. 技术路线 (阶段性交付)
- Phase A: 游戏主循环 + 场景流转（城镇/地城/Boss）+ 保存/读取
- Phase B: 角色成长系统（等级/属性/技能解锁）
- Phase C: 掉落与装备（背包/装备/词缀/商店）
- Phase D: 任务系统与关卡配置（主线/支线/触发器）
- Phase E: 敌人生态与 Boss 机制扩展（精英词缀、AI 行为包）
- Phase F: 反馈与体验（音效、镜头、UI 动效、掉落表现）
- Phase G: 平衡与性能优化（数值、掉落率、池化与 GC）

## 7. 默认假设
- AI 使用 NavMeshAgent
- 伤害公式先用: Base + Scale + Modifiers - Resistance
