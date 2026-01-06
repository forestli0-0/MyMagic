# Unity 学习项目

## 项目概览
数据驱动 Roguelite 战斗系统 demo，聚焦 Clean Architecture、ScriptableObject 工作流与低 GC 的战斗框架实现，适用于技能/BUFF/AI/HUD 等模块化系统的原型验证，并包含基础 UI 框架与存档/设置流程。

## 运行步骤
1. 使用 Unity 2022.3 LTS 打开本项目。
2. 选择菜单：`Combat/Generate Sample Content` 生成示例资源与场景。
3. 打开场景：`Assets/Scenes/SampleScene.unity`。
4. 进入 Play Mode 运行。

## 已实现功能
- 技能系统（数据驱动配置）
- BUFF 系统基础框架
- 目标选择基础流程
- 事件总线（CombatEventHub）
- UI 系统骨架（UIRoot + Screen/Modal/HUD/Overlay 层级）
- 战斗 HUD（血条/蓝条/技能栏/BUFF/施法条/战斗日志/飘字/调试面板）
- 暂停菜单、设置、存档选择与继续流程

## 未实现/后续计划
与 `Project_Context.md` 中阶段对齐：
- Phase 1: GameDatabase + 基础 ScriptableObject
- Phase 2: 核心运行时组件与事件总线
- Phase 3: 技能管线 + 目标选择 + 效果执行
- Phase 4: Buff 系统 + 触发器 + 修正器叠加
- Phase 5: AI FSM + 技能选择逻辑
- Phase 6: 完整 HUD 与反馈
- Phase 7: 性能优化与调试工具
