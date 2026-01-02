# Unity Project Guidelines: Data-Driven Combat System

## 1. Role & Goal
You are a Senior Unity Architect assisting in creating a highly decoupled, data-driven Roguelite combat demo. The focus is on **Clean Architecture**, **ScriptableObject workflows**, and **Memory Optimization**.

## 2. Core Architectural Rules (STRICTLY FOLLOW)
* **Data-Driven:** All static data (Skill damage, cooldowns, icons, Buff effects) MUST be defined in `ScriptableObject`. Never hardcode magic numbers in MonoBehaviours.
* **Composition Over Inheritance:** Do NOT use deep inheritance trees (e.g., `BaseUnit -> Human -> Player`). Use Interfaces (e.g., `IDamageable`, `ISkillUser`) and Components.
* **Event-Driven Communication:** Systems should communicate via C# Events (`Action`, `Func`) to avoid tight coupling. For example, the UI should listen to `PlayerHealth.OnHealthChanged`, not reference the Player directly.
* **Zero Garbage Collection (GC):** In the battle loop (`Update`), avoid `new` allocations. Use **Object Pooling** for projectiles, effects, and damage numbers.
* **SOLID Principles:** Ensure classes have a Single Responsibility.

## 3. Tech Stack
* Unity 2022.3 LTS (URP)
* C# (Latest supported version)
* Odin Inspector (Optional, but assume standard Inspector for now)
* DoTween (for simple animations)

## 4. Folder Structure
Save scripts in `Assets/_Game/Scripts/`.
Save config files in `Assets/_Game/ScriptableObjects/`.

---

# Combat System Design Spec (CN)

## 1. 目标与约束
- 即时 3D、包含普攻与位移、法力资源、技能独立冷却
- 支持多段效果、触发器、堆叠层数、技能/BUFF 交互
- AI 使用状态机，UI 需求为完整 HUD
- 数据驱动、事件驱动、低 GC、组合式设计

## 2. 数据驱动配置清单 (ScriptableObject)
- GameDatabase: 集中注册与运行时索引 (Stat/Tag/Unit/Skill/Buff/Target/AI/HUD)
- StatDefinition: 属性定义与默认值
- TagDefinition: 标签系统 (技能、BUFF、单位筛选/交互)
- UnitDefinition: Prefab、基础属性、初始技能、普攻、AIProfile
- SkillDefinition: CD/法力/施法阶段/目标规则/SkillStep 列表
- SkillStep: 触发时机/延时/效果列表/动画与特效 cue
- EffectDefinition: Damage/Heal/ApplyBuff/RemoveBuff/Projectile/Move/Resource/Summon/TriggerSkill/Conditional
- ConditionDefinition: 概率、标签、血量阈值、拥有 Buff、受击类型等
- ModifierDefinition: 属性或参数修正器，支持 Tag 过滤与层叠
- BuffDefinition: 持续、层数规则、触发器、修正器、Tick 间隔
- ProjectileDefinition: 速度/寿命/命中/穿透/命中特效
- TargetingDefinition: 单体/范围/锥形/链式/随机/优先级
- AIProfile: 状态机配置与技能选择规则
- HUDConfig: HUD 布局与槽位设定

## 3. 运行时核心组件 (组合式)
- UnitRoot: 挂载配置并初始化
- StatsComponent: 运行时属性块 + 修正器叠加
- HealthComponent / ResourceComponent: 生命/法力与事件
- CooldownComponent: 技能冷却状态
- SkillUserComponent: 技能释放入口与校验
- BuffController: Buff 实例管理与触发器驱动
- TargetingSystem: 目标筛选与排序 (池化列表)
- EffectExecutor: 统一执行 EffectDefinition
- DamageSystem: 伤害公式与结算
- MovementComponent: 对接 IMovementDriver
- CombatEventHub: 事件总线 (UI/系统订阅)

## 4. 技能执行管线
1) 产生 SkillRequest (输入/AI)
2) SkillUser 校验资源/冷却
3) 创建 SkillContext (池化)，收集修正器
4) TargetingSystem 选择目标
5) 触发 OnCastStart，调度 SkillStep
6) EffectExecutor 应用效果
7) 触发 OnHit/OnDamage/OnKill/OnSkillCast 事件
8) 扣资源/开始冷却/UI 刷新

## 5. Buff 与触发器设计
- Buff 堆叠规则: Refresh / Extend / Independent，MaxStacks 可控
- 触发器: OnApply/OnExpire/OnTick/OnHit/OnDamaged/OnSkillCast/OnKill
- 交互机制: Tag + Condition + Modifier 实现技能与 Buff 交互

## 6. 目标选择与命中
- 目标逻辑完全数据化
- 投射物命中回调触发效果序列

## 7. AI 状态机
- 状态: Idle/Chase/Attack/CastSkill/Retreat
- 技能选择: 基于距离/冷却/条件/权重

## 8. UI HUD
- 血条、法力、技能栏与冷却、Buff 列表、施法条、飘字、战斗日志
- UI 仅订阅事件，不直接依赖 Player

## 9. 性能与内存
- Update 内避免 new、无 LINQ
- 投射物/特效/飘字对象池化
- 统一时间调度器管理延时与 Tick

## 10. 技术路线 (阶段性交付)
- Phase 0: 设计规格确认与命名规范
- Phase 1: GameDatabase + 基础 ScriptableObject
- Phase 2: 核心运行时组件与事件总线
- Phase 3: 技能管线 + 目标选择 + 效果执行
- Phase 4: Buff 系统 + 触发器 + 修正器叠加
- Phase 5: AI FSM + 技能选择逻辑
- Phase 6: 完整 HUD 与反馈
- Phase 7: 性能优化与调试工具

## 11. 默认假设
- AI 使用 NavMeshAgent
- 伤害公式先用: Base + Scale + Modifiers - Resistance
