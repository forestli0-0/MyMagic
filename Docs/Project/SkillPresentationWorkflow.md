# Skill Presentation Workflow (MVP)

## Goal
- Use one pipeline for mechanics and presentation.
- Keep old `SkillStep.animationTrigger/vfxPrefab/sfx` fields backward compatible.
- Let designers configure presentation without code changes.

## Runtime Pipeline
1. `SkillUserComponent.ExecuteStep` dispatches `SkillStepExecutedEvent`.
2. `EffectExecutor` dispatches `SkillEffectExecutedEvent` before/after apply.
3. `ProjectileController` dispatches `ProjectileLifecycleEvent` on spawn/hit/return/split.
4. `SkillPresentationSystem` listens to these events and plays cues.

## Data Model
- `SkillRuntimeContext` includes:
  - `CastId`
  - `StepIndex`
- `SkillStep` includes:
  - `presentationCues` (`List<SkillPresentationCue>`)
  - legacy fields kept for fallback
- `SkillPresentationCue` supports:
  - event type (`Step/EffectBefore/EffectAfter/Projectile*`)
  - anchor (`Caster/Target/Explicit/AimPoint/Projectile/World`)
  - animation trigger
  - VFX prefab + lifetime/follow
  - SFX clip + basic audio params

## Authoring Flow
1. Open `Combat/Tools/Skill Authoring Window`.
2. Create or select a skill.
3. Configure step mechanics (`effects`).
4. Configure `presentationCues` per step.
5. Run validation in window.
6. Sync skill/effect/buff/projectile references into `GameDatabase`.
7. Mount selected skill to player `startingSkills` for scene testing.

## Free VFX Bootstrap
1. Run `Combat/Tools/Skill Presentation/Apply Free VFX Plan (Player Skills)`.
2. Tool output:
   - Ensures free-asset staging folders exist.
   - Generates runtime fallback VFX prefabs under `Assets/_Game/Art/VFX/RuntimeVariants`.
   - Batch-binds `Auto_*` cues for player skills.
   - Mounts `SkillPresentationSystem` in `SampleScene` and `Town`.
3. Re-run command safely after adding external free packs; auto cues are idempotent (`Auto_*` cues are regenerated).

## Legacy Compatibility
- If a step has no `presentationCues`, `SkillPresentationSystem` falls back to:
  - `animationTrigger`
  - `vfxPrefab`
  - `sfx`
- Editor window provides:
  - `Migrate Legacy (Selected)`
  - `Migrate Legacy (All)`

## Rollback
- Disable/remove `SkillPresentationSystem` to stop presentation playback.
- Mechanics chain remains intact (damage/buff/projectile logic still works).
