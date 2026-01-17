# Unity 学习项目

## 项目概览
本项目已从“战斗系统 demo”升级为“可完整游玩的 ARPG 目标（暗黑2式流程）”。核心方向：数据驱动战斗 + 关卡推进 + 掉落与成长 + 任务与城镇循环，逐步落地可持续迭代的完整游戏结构。

## 运行步骤
1. 使用 Unity 2022.3 LTS 打开本项目。
2. 选择菜单：`Combat/Generate Playable Flow` 生成可游玩流程与场景。
3. 打开场景：`Assets/Scenes/MainMenu.unity`。
4. 进入 Play Mode 运行（主菜单 -> New Game/Continue）。

## 已实现功能（战斗系统基础）
- 技能系统（数据驱动配置）
- BUFF 系统基础框架
- 目标选择基础流程
- 事件总线（CombatEventHub）
- UI 系统骨架（UIRoot + Screen/Modal/HUD/Overlay 层级）
- 战斗 HUD（血条/蓝条/技能栏/BUFF/施法条/战斗日志/飘字/调试面板）
- 暂停菜单、设置、存档选择与继续流程

## 当前目标与路线
与 `Project_Context.md` 中阶段对齐：
- Phase A: 游戏主循环 + 场景流转（城镇/地城/Boss）+ 保存/读取
- Phase B: 角色成长系统（等级/属性/技能解锁）
- Phase C: 掉落与装备（背包/装备/词缀/商店）
- Phase D: 任务系统与关卡配置（主线/支线/触发器）
- Phase E: 敌人生态与 Boss 机制扩展（精英词缀、AI 行为包）
- Phase F: 反馈与体验（音效、镜头、UI 动效、掉落表现）
- Phase G: 平衡与性能优化（数值、掉落率、池化与 GC）
