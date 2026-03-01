# Skill VFX Asset Ledger (Free-Only)

Updated: 2026-03-01

## Scope
- Pipeline: `URP 14`
- Goal: Player `basicAttack + startingSkills` have visible cast/result feedback.
- Source policy: Unity Asset Store official pages + CC0 sources only.

## Approved Sources
1. Cartoon FX Remaster Free  
   URL: https://marketplace.unity.com/packages/vfx/particles/cartoon-fx-remaster-free-109565  
   License: Unity Asset Store EULA (standard)
2. Magic Effects FREE  
   URL: https://assetstore.unity.com/packages/vfx/particles/spells/magic-effects-free-247933  
   License: Unity Asset Store EULA (standard)
3. Trails VFX - URP  
   URL: https://marketplace.unity.com/packages/vfx/trails-vfx-urp-242574  
   License: Unity Asset Store EULA (standard)
4. Free Fire VFX - URP  
   URL: https://marketplace.unity.com/packages/vfx/particles/fire-explosions/free-fire-vfx-urp-266226  
   License: Unity Asset Store EULA (standard)
5. Kenney Impact Sounds  
   URL: https://kenney.nl/assets/impact-sounds  
   License: CC0
6. Kenney UI Audio  
   URL: https://kenney.nl/assets/ui-audio  
   License: CC0
7. OpenGameArt - Magic Spell SFX  
   URL: https://lpc.opengameart.org/content/magic-spell-sfx  
   License: CC0

## Local Mapping Folders
- Third-party VFX import staging: `Assets/_Game/Art/VFX/ThirdPartyFree`
- Third-party SFX import staging: `Assets/_Game/Art/SFX/ThirdPartyFree`
- Runtime fallback variants (auto-generated): `Assets/_Game/Art/VFX/RuntimeVariants`

## Current Project Status
- Runtime fallback VFX generation: implemented via `SkillCueBatchBinder`.
- Batch cue mapping for player skills: implemented via `SkillCueBatchBinder`.
- Scene mounting (`SampleScene` + `Town`): implemented via `SkillCueBatchBinder`.
- Note: Asset Store package binaries require Unity Editor import with a logged-in account.

## Usage
1. Unity menu: `Combat/Tools/Skill Presentation/Apply Free VFX Plan (Player Skills)`
2. Verify cues in `Combat/Tools/Skill Authoring Window`.
3. Play `SampleScene` or `Town` and validate player skills visually.
