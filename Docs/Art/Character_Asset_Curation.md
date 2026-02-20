# Character Asset Curation (Imported Packs)

## 整理结果
已将你导入的角色资源统一归档到：
- `Assets/ThirdParty/Characters/AssetHunts_Mannequin`
- `Assets/ThirdParty/Characters/KevinIglesias_HumanAnimations`
- `Assets/ThirdParty/Characters/ExplosiveLLC_RPGAnimPack`
- `Assets/ThirdParty/Characters/JC_LP_MedievalCharacters_LITE`

并已在 `.gitignore` 中忽略上述目录，避免 Git 出现数千文件待提交。

## 资源盘点（可用于当前项目）

### 1) 模型来源
- Mannequin（通用人形）
  - `Assets/ThirdParty/Characters/AssetHunts_Mannequin/GameDev Essential Kit - Mannequin/Asset/Mannequin_Man.prefab`
- RPG 角色（自带完整战斗动作同骨架）
  - `Assets/ThirdParty/Characters/ExplosiveLLC_RPGAnimPack/RPG Character Mecanim Animation Pack FREE/Prefabs/Character/RPG-Character.prefab`
- 低多边形中世纪角色（风格模型）
  - `Assets/ThirdParty/Characters/JC_LP_MedievalCharacters_LITE/Prefabs/SM_MedievalMaleLite_01.prefab`

### 2) 动作来源（优先级）
- 战斗动作优先用 RPG Pack（覆盖 Attack/Hit/Knockdown/Idle/Run）
  - 目录：
  - `Assets/ThirdParty/Characters/ExplosiveLLC_RPGAnimPack/RPG Character Mecanim Animation Pack FREE/Animations/Unarmed`
  - 推荐最小集：
    - Idle: `RPG-Character@Unarmed-Idle.FBX`
    - Run: `RPG-Character@Unarmed-Run-Forward.FBX`
    - Attack: `RPG-Character@Unarmed-Attack-R1.FBX`
    - Hit: `RPG-Character@Unarmed-GetHit-F1.FBX`
    - Die: `RPG-Character@Unarmed-Knockdown1.FBX`
- 位移备用动作可用 Kevin Iglesias
  - `Assets/ThirdParty/Characters/KevinIglesias_HumanAnimations/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx`
  - `Assets/ThirdParty/Characters/KevinIglesias_HumanAnimations/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Forward.fbx`
  - `Assets/ThirdParty/Characters/KevinIglesias_HumanAnimations/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx`

## 建议接入策略
1. 先走“稳定可用”路线：`RPG-Character.FBX（纯模型） + RPG Unarmed 动作`，快速闭环验证。
2. 再做“风格替换”路线：把模型替换为 Medieval Lite / Mannequin，动作继续复用 RPG 动作（Humanoid 重定向）。
3. 最后微调 `UnitVisualProfile` 的模型偏移、缩放和参数映射。

> 注意：不要把 `RPG-Character.prefab` 直接作为运行时视觉模型挂到 `UnitVisualProfile`。
> 该 Prefab 带有导航/控制脚本（NavMeshAgent、RPGCharacterController、SuperCharacterController），会与项目现有控制系统冲突。
> 应优先使用“纯模型资产”（如 `RPG-Character.FBX`）或无逻辑脚本的美术 Prefab。

## 当前结论
- 这 4 个包已经满足你当前需求：
  - 骨骼网格体：有
  - 基础移动动作：有
  - 战斗动作（攻击/受击/倒地）：有
- 下一步无需再找包，直接进入“控制器绑定与参数对齐”。

## 已落地的自动化
已新增 Editor 工具：
- `Assets/_Game/Scripts/Editor/UnitVisualAnimationBootstrapper.cs`

菜单入口：
- `Combat/Visual/Build Default Unit Animator And Bind Profiles`

执行后会自动：
1. 生成默认控制器  
`Assets/_Game/Art/Characters/Controllers/Unit_Combat_Default.controller`
2. 绑定所有 `UnitVisualProfile.animatorController`
3. 自动填充默认模型（仅在 `modelPrefab` 为空时）：
   - Player -> RPG Character
   - Enemy -> Medieval Lite
   - 其他 -> Mannequin
