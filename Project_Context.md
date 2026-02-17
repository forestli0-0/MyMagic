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

## 5. 输入与控制（已完成）
基于 Unity Input System 实现统一输入抽象层，支持键鼠/手柄多设备输入。

**核心组件：**
- `InputReader`: 统一输入读取器，管理 ActionMap 生命周期，通过事件分发输入
- `CombatInputAssetBuilder`: 编辑器工具，代码生成 InputActionAsset

**输入映射 (ActionMap)：**
- `Gameplay`: 移动、瞄准、技能(1-6)、取消、暂停、切换技能页
- `UI`: 导航、确认、取消、指针、点击、滚动
- `Debug`: 切换调试覆盖层(F3)

**设计原则：**
- 事件驱动：消费者订阅 `InputReader` 事件，不直接依赖 Input System API
- UI 模式感知：根据 `UIManager.InputMode` 自动切换 Gameplay/UI 映射
- 可重绑定：绑定配置集中于 `CombatInputAssetBuilder`，修改后重新生成即可

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

## 8. 7日MVP任务清单（防跑偏）

### 8.1 Scope Lock（必须遵守）
- 仅做 3 个场景：Town / Field / Boss
- 仅 1 条主线 + 1 条支线
- 装备 10-15 件，稀有度 3 档，词缀 4-6 个
- 技能总量控制在 6-8 个（含基础攻击）
- 只做 1 套可完整通关的循环（10-15 分钟）

### 8.2 里程碑与任务（Day 1-7）

**Day 1: 核心循环与场景流转**
- [x] 创建 Town/Field/Boss 三场景并打通传送点
- [x] LevelDefinition + LevelFlowController（出生点/回城点/关卡状态）
- [x] 保存/读取扩展：场景名 + 出生点 Id
- [x] 主菜单/存档/继续流程串通
- [x] 体验检查：进入游戏->完成一次场景切换

**Day 2: 成长系统**
- [x] ProgressionDefinition（经验曲线/升级所需/属性点）
- [x] PlayerProgression（经验、等级、属性点、事件派发）
- [x] 属性面板 UI（加点/升级提示/经验条）
- [x] 与 StatsComponent 接入（升级即时生效）
- [x] 保存/读取扩展：等级/经验/已分配属性点

**Day 3: 物品与装备**
- [x] ItemDefinition / AffixDefinition / ItemInstance
- [x] InventoryComponent（背包、堆叠、容量）
- [x] EquipmentComponent（槽位/加成/穿脱）
- [x] 背包与装备 UI（拖拽或点击交互）
- [x] 保存/读取扩展：背包与装备

**Day 4: 掉落与商店**
- [x] LootTableDefinition + LootDropper（掉落权重）
- [x] LootPickup（拾取/自动拾取可选）
- [x] 货币系统（单一货币）
- [x] VendorDefinition + VendorService + UI（买/卖）
- [x] 掉落与经济数值首轮校准

**Day 5: 任务系统**
- [x] QuestDefinition（目标/步骤/奖励）
- [x] QuestTracker（状态、事件监听）
- [x] 任务触发器（区域/击杀/对话占位）
- [x] 实现 1 主线 + 1 支线
- [x] 保存/读取扩展：任务状态

**Day 6: 敌人生态与 Boss**
- [x] EncounterDefinition（刷怪/精英）
- [x] 精英词缀（属性修正/技能增强）
- [x] Boss 技能 2-3 个（节奏/易读 telegraph）
- [x] AI 行为包补完（追击/技能释放/撤退可选）
- [x] Field/Boss 关卡难度首轮调平

**Day 7: 体验与收尾**
- [x] 关键战斗反馈（打击音/命中特效/镜头轻微抖动）
- [x] UI 动效（技能/掉落/任务更新提示）
- [x] 性能检查（池化、避免 Update GC）
- [x] Bug 修复 + 文档补齐（README/操作说明）
- [x] 录制演示视频脚本与流程

### 8.3 美术与表现任务清单（跨越绘画）
- [ ] 确定视觉基调与调色板（Town/Field/Boss 各 1 套）
- [ ] 场景占位资源（地面/墙体/障碍/传送点）
- [ ] 角色/怪物/ Boss 占位模型或替代素材
- [ ] UI 关键图标（技能/装备/任务/货币）
- [ ] 关键技能 VFX（基础攻击/核心技能/ Boss 技能）
- [ ] 基础音效（命中/施法/拾取/界面确认）

### 8.4 MVP 验收标准（完成定义）
- [x] 从主菜单进入游戏，完成一次 Town->Field->Boss 循环
- [x] 角色成长与装备变化可感知，数值不崩
- [x] 任务可接取/完成/领奖励
- [x] 存档可完整恢复：场景/角色/装备/任务/货币
- [x] 10-15 分钟完整通关一次，无明显卡关

## 9. 跨会话交接（每次会话更新）

### 9.1 当前状态快照
- 已具备：数据驱动战斗体系、基础 AI FSM、HUD/UI 框架、样例生成工具与样例场景
- 已新增：LevelDefinition/LevelFlowController/LevelPortal/LevelSpawnPoint 与 PlayableFlowGenerator（已运行）
- 已新增：ProgressionDefinition/PlayerProgression/经验 HUD/击杀经验/存档成长数据
- 已新增：背包/装备 UI 拖拽交互，背包槽位固定化（支持交换/合并/指定槽位落点）
- 已新增：Day4 掉落与商店链路（LootTable/LootDropper/LootPickup/Currency/Vendor）
- 已新增：Vendor UI 交互增强（双击买卖、拖拽买卖、选中高亮与详情、Esc 关闭）
- 已新增：Day5 任务系统链路（QuestDefinition/QuestTracker/QuestGiverTrigger/QuestObjectiveTrigger/QuestJournal+QuestGiver UI）
- 已新增：Day6 敌人生态与 Boss 机制（EncounterDefinition/EnemyAffixDefinition/EncounterDirector/BossSkillScheduler）
- 已新增：Day7 战斗反馈（DamageApplied 事件链 / HitFlashReceiver / CombatFeedbackController / CameraShakeController）
- 已新增：Day7 UI 动效反馈（HudToastOverlay / QuestTrackerHUD 脉冲 / SkillSlotUI 施法脉冲 / LootPickup 提示事件）
- 已新增：Day7 性能烟测（Day7PerformanceSmokeTests，Field/Boss 基线采样日志）
- 已修复：主菜单返回流程/Continue 进入场景卡住问题（UI/HUD/TimeScale）
- 已修复：存档加载物品与装备（GameDatabase items 索引补齐）
- 已修复：CastBar 未施法常驻显示（`Heal` 条常显）问题
- 现有场景：`Assets/Scenes/MainMenu.unity`、`Assets/Scenes/Town.unity`、`Assets/Scenes/Field.unity`、`Assets/Scenes/Boss.unity`
- 存档范围：位置/血量/资源/场景名 + 出生点 Id + 等级/经验/属性点 + 背包/装备
- 自动化验证：Day4/Day5/Day6 PlayMode 测试 + Day7 性能烟测已建立并通过

### 9.2 当前正在做/下一步（优先级顺序）
- MVP 验收走查已完成（8.4 清单全部通过），进入里程碑收尾与后续内容规划

### 9.3 待确认/风险
- 角色职业与战斗主题：已定为「符文守望者 Rune Warden」（火焰/奥术风格）
- 视角/操控手感：俯视 ARPG，鼠标点击/指向
- 美术资源来源：Unity Asset Store 免费资源（见 9.5）

### 9.4 关键入口与工具
- 样例场景：`Assets/Scenes/SampleScene.unity`
- 生成工具：`Assets/_Game/Scripts/Editor/CombatSampleGenerator.cs`
- 关卡流转生成：`Assets/_Game/Scripts/Editor/PlayableFlowGenerator.cs`
- 战斗数据库：`Assets/_Game/Scripts/Data/GameDatabase.cs`

### 9.5 资产清单（已锁定，可跨会话复用）
**角色**
- GanzSe FREE Low Poly Modular Character
  https://assetstore.unity.com/packages/3d/characters/humanoids/fantasy/ganzse-free-low-poly-modular-character-321521
- Creative Characters FREE - Animated Low Poly 3D Models
  https://assetstore.unity.com/packages/3d/characters/humanoids/creative-characters-free-animated-low-poly-3d-models-304841

**场景/地形**
- Blacksmith's Forge
  https://assetstore.unity.com/packages/3d/environments/fantasy/blacksmith-s-forge-17785
- STYLIZED Fantasy Armory - Low Poly 3D Art
  https://assetstore.unity.com/packages/3d/environments/fantasy/stylized-fantasy-armory-low-poly-3d-art-249203
- Stylized House Interior
  https://assetstore.unity.com/packages/3d/environments/stylized-house-interior-224331
- Free Low Poly Nature Forest
  https://assetstore.unity.com/packages/3d/environments/landscapes/free-low-poly-nature-forest-205742
- Low-Poly Simple Nature Pack
  https://assetstore.unity.com/packages/3d/environments/landscapes/low-poly-simple-nature-pack-162153
- (UNL) Ultimate Nature Lite
  https://assetstore.unity.com/packages/3d/environments/unl-ultimate-nature-lite-176906

**武器/道具**
- Stylized Newbie Weapons Pack
  https://assetstore.unity.com/packages/3d/props/weapons/stylized-newbie-weapons-pack-200709

**VFX**
- Free Fire VFX - URP
  https://assetstore.unity.com/packages/vfx/particles/fire-explosions/free-fire-vfx-urp-266226
- Trails VFX - URP
  https://assetstore.unity.com/packages/vfx/trails-vfx-urp-242574

**UI 图标**
- Clean Vector Icons
  https://assetstore.unity.com/packages/2d/gui/icons/clean-vector-icons-132084

**天空盒**
- Fantasy Skybox FREE
  https://assetstore.unity.com/packages/2d/textures-materials/sky/fantasy-skybox-free-18353

**音效**
- Free Sound Effects Pack
  https://marketplace.unity.com/packages/audio/sound-fx/free-sound-effects-pack-155776

## 10. 调试与排错规范（必须遵守）
**目标**：避免“猜测式修复”，用可复现日志快速定位根因，并在修复后清理痕迹。

**流程**
- 明确“问题边界”：发生场景、触发步骤、预期与实际差异。
- 只在关键路径加日志（生命周期入口/流程分支/失败点），统一 tag，避免散点式输出。
- 日志过长时允许写入文件（例如 `Logs/xxx.log`），包含时间戳与关键上下文（scene、spawnId、候选对象数量等）。
- 收集日志 -> 锁定根因 -> 修复 -> 立即清理调试代码与日志文件。

**模板（示例）**
- Tag：`[System][SubSystem]`
- 关键字段：`scene`、`levelId`、`spawnId`、`candidateCount`、`resolvedId`

**清理要求**
- Debug 输出必须可开关（布尔开关或编译宏）。
- 修复完成后默认关闭并移除临时日志文件。
