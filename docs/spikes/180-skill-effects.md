# Spike #180 — Plan out skill effects

- **Spike issue:** [#180](https://github.com/ginderjeremiah/GameServer/issues/180)
- **Status:** Draft — design reviewed with the project owner (core decisions settled); implementation sub-issues not yet created.

## Goal

Let skills do more than deal damage: when a skill fires it can apply **effects** — timed buffs/debuffs that increase or decrease attributes on the caster or the opponent — including damage-over-time and heal-over-time. The UI indicates which skills carry effects and which effects are currently active in battle.

## Current state (what already exists)

- **`Skill` is reference data only** (`Id`, `Name`, `BaseDamage`, `CooldownMs`, `DamageMultipliers`): in battle, `BattleSkill` charges each 40ms tick and fires `BaseDamage + Σ(attribute × multiplier)` against the defender's flat `Defense`. No effects, no in-simulation RNG. Players and enemies share the same skill catalogue, so anything authored on a skill applies to both sides automatically.
- **The attribute system was explicitly designed to be the effect substrate** (`game-design.md` → Attributes): DoTs are envisioned as timed bonuses to attributes the simulator reads per tick. The backend `AttributeCollection` already supports `AddModifier` with cascading cache invalidation through derived attributes — but has **no `RemoveModifier`**. The frontend is asymmetric: `BattleAttributes` flattens everything into a plain `number[]` at battle start (via `computeAttributes`, which mirrors the backend's composition rules including derived resolution and the circular guard), so it cannot currently change a value mid-battle at all.
- **Seeded RNG scaffolding exists but is reserved**: Mulberry32 is ported on both sides and `PlayerState.BattleSeed` is persisted and sent to the client, earmarked for the crit/dodge/block work (#178). It has no live consumer yet.
- **Battle parity discipline**: battle logic is hand-ported FE/BE and guarded by mirrored scenario matrices (`BattleSimulatorParityTests.cs` ↔ `battle-simulation-parity.test.ts`). Every effect rule below must be pinned by mirrored vectors.
- New `EAttribute` values are cheap: the `Attributes` table is seeded from the enum (`GameContext` `HasData` over `Attribute.GetAllAttributes()`), so an addition is an enum value + description + migration.

## Decisions

Settled with the project owner during the spike:

1. **Effects ride the real attribute system (full modifier graph).** Applying an effect adds a genuine `AttributeModifier` to the target's live collection; expiry removes it. Derived attributes cascade naturally — a Strength buff also raises MaxHealth, exactly like permanent Strength would. This requires backend `AttributeCollection.RemoveModifier` and a frontend `BattleAttributes` rework (retain the modifier list, recompute on change via the existing `computeAttributes`), which also unifies the currently-asymmetric FE/BE attribute machinery. Rejected alternative: a flat "effect overlay" on top of frozen base values (less rework, but no derived cascade and a second composition path to keep in parity).
2. **V1 includes DoT/HoT via per-tick attributes.** Two new attributes — `DamageTakenPerSecond` and `HealthRegenPerSecond` — plus an end-of-tick simulator phase that applies them. A poison is then pure data: an effect that adds `+12 DamageTakenPerSecond` to the opponent for 3000ms. This realizes the mechanism `game-design.md` already describes and finally feeds the defined-but-unused `DamageHealed` statistic.
3. **Timed durations only in V1.** Durations are authored in ms and consumed in 40ms ticks. The issue's "after x hits" idea (hit-count durations, every-Nth-hit triggers) is deferred; the authored model keeps an obvious seam for it (see Deferred extensions).
4. **Refresh-only stacking.** Re-applying an already-active effect resets its remaining duration to full; magnitudes never stack and no second modifier is added. Per-effect stacking policies are a possible later extension.
5. **V1 is deterministic — no proc chance.** Effects always apply when the skill fires. Chance-based application would consume the seeded battle RNG, whose draw-ordering design belongs to #178; an optional `ChanceToApply` is a designed extension to land with/after it. This keeps the spike independent of #178.

## Authored model

New child reference data on `Skill`, shaped like `SkillDamageMultiplier`:

**`SkillEffect`** — `SkillId`, `Target` (new enum `ESkillEffectTarget { Self = 1, Opponent = 2 }`), `Attribute` (`EAttribute`), `ModifierType` (`EModifierType`: additive/multiplicative), `Amount`, `DurationMs`.

- A skill may carry any number of effects; each is applied independently when the skill fires.
- A new `EAttributeModifierSource.SkillEffect` value marks the modifiers effects create (identification for removal + breakdown/inspection display). It flows to the client through the existing #282 domain-enum codegen.
- The two new attributes are authored in **per-second units** (friendlier than per-tick); the simulator applies `value × MsPerTick / 1000` per tick.

## Runtime semantics (parity-critical rules)

These rules define the simulation behaviour both sides must implement identically; the mirrored parity matrices pin each one.

- **Fire order:** when a skill fires, its damage is computed and dealt **first** (from pre-effect attribute state), **then** its effects are applied — a self damage-buff never boosts the hit that carries it. Skills resolve sequentially in loadout order, so an effect applied by an earlier slot does affect a later slot firing on the same tick.
- **Active-effect state:** the `Battler` tracks `ActiveEffect { sourceEffect, remainingMs }`. Apply = add the modifier to the target's collection; re-apply of the same authored effect = refresh `remainingMs` to full (no new modifier).
- **Expiry:** at the **start of each tick**, before any skill fires, every active effect's `remainingMs` decrements by the tick size; effects reaching ≤ 0 are removed (modifier removed, caches invalidated). Consequently `DurationMs / 40` is the number of ticks an effect influences, counting the tick it was applied on.
- **DoT/HoT phase:** at the **end of each tick**, after both battlers' skills have resolved (reached only if both still live, matching the existing early-return-on-death loop). The enemy resolves first — if enemy DoT kills it, victory is awarded before the player's DoT applies (ties favour the player, consistent with the skill-exchange order). Per battler: `DamageTakenPerSecond` applies first (**bypasses Defense**), death is checked, then `HealthRegenPerSecond` heals (capped at MaxHealth).
- **MaxHealth changes mid-battle:** `CurrentHealth` is unchanged when MaxHealth rises (no free healing), and is clamped down to the new maximum when MaxHealth drops below it (e.g. an Endurance buff expiring). Clamping happens at the two effect bookkeeping points (apply, expire).
- **Live attribute reads:** Defense is already read per-hit on both sides. CooldownRecovery is read per-tick on the backend but **cached at battle start on the frontend** (`Battler.cdMultiplier`) — the frontend must switch to a live read so CDR buffs work.
- **Statistics attribution:** DoT on the enemy (necessarily sourced from player debuffs) counts toward `PlayerDamageDealt` but not `HighestPlayerAttack` and not any per-skill `SkillStats` (per-skill DoT attribution deferred); DoT on the player counts toward `PlayerDamageTaken`. Actual healing received by the player (post-cap) accumulates into a new `BattleStats.PlayerDamageHealed`, wired through battle-completed resolve into the existing `EStatisticType.DamageHealed`.
- **Performance:** effect bookkeeping is on the replay hot path — recomputes happen only when an effect is applied/expires (reads stay cached between changes), and per-tick bookkeeping must avoid allocations (same discipline as #286).

## Changes by area

### A. Attribute foundation (removable modifiers)
- Backend: `AttributeCollection.RemoveModifier(modifier)` with the same cascading derived-node cache invalidation as `AddModifier`.
- Frontend: rework `BattleAttributes` to retain its modifier list and support add/remove with recompute via `computeAttributes` (memoised between changes), replacing the flattened `number[]`. `Battler.reset` feeds modifiers instead of pre-flattened values; `cdMultiplier` becomes a live read.

### B. `SkillEffect` reference data + authoring
- Entity `SkillEffect` + `GameContext` config + **EF migration**; `Skill` entity nav.
- Read contract `Game.Abstractions.Contracts.SkillEffect` on the `Skill` contract; repo projection (`Skills.All()`); TS codegen (`ISkill.effects`, `ESkillEffectTarget`).
- Domain `Game.Core.Skills.SkillEffect` on `Skill`.
- Admin: `AdminTools/SetSkillEffects` endpoint (mirroring `SetSkillMultipliers`) + a Workbench skill-editor **Effects** table section (target, attribute, modifier type, amount, duration).

### C. Battle runtime — timed buffs/debuffs
- New attributes enum values + migration (`DamageTakenPerSecond`, `HealthRegenPerSecond` land here so the schema/codegen settles once), `EAttributeModifierSource.SkillEffect`.
- `BattleSkill` applies effects on fire via `BattleContext` (Self → active battler, Opponent → target); `Battler` gains active-effect tracking with the apply/refresh/expire/clamp rules above. Mirrored in `battler.ts`/`skill.ts`/`battle-step.ts`.
- Mirrored parity vectors: buff affects subsequent hits only; derived cascade (STR → MaxHealth); expiry tick math; refresh-not-stack; MaxHealth clamp; CDR buff changing fire ticks.

### D. Battle runtime — DoT/HoT phase
- End-of-tick phase in both simulators with the ordering/death rules above; defense bypass; regen cap.
- `BattleStats.PlayerDamageHealed` + resolve wiring to `DamageHealed`; DoT stat attribution per above.
- Mirrored parity vectors: poison kill timing (incl. enemy-first tie), regen survival, DoT+regen same tick, dot-expiry mid-fight.

### E. UI — effect indicators
- **Skill badge** on battle skill buttons + loadout/selection surfaces for skills with effects (the indicator the issue calls out), + effect lines in `SkillTooltip` (e.g. "On hit: +15 Strength (self), 5s" / "Poisons: 12 dmg/s, 3s").
- **Active-effect chips** on the battler cards: buff/debuff direction, remaining duration (render-interpolated countdown), refresh on re-apply.
- **Logs** (frontend-only per the logs policy): application (and possibly expiry) messages; DoT ticks are **not** logged per tick (25/s) — aggregate (per-second or on expiry/death summary), exact rule decided in implementation.

## Battle-parity note

Everything in "Runtime semantics" executes on both sides. The frontend attribute rework (A) is the riskiest parity surface: after it, both sides must compose attributes through their one shared path (backend `AttributeCollection`, frontend `computeAttributes`) including mid-battle recomputation — any divergence in derived resolution or modifier ordering breaks replay validation. The existing parity matrices grow scenarios per area (C, D) with identical inputs and expected outcomes on both sides, per the established convention.

## Implementation issues

To be created after owner review of this document, as sub-issues of #180 (planned breakdown):

1. **Attribute foundation: removable modifiers + frontend modifier-graph battler attributes** *(foundation)* — area **A**.
2. **`SkillEffect` reference data + admin authoring end-to-end** *(independent of 1)* — area **B**. No battle behaviour.
3. **Battle runtime: timed attribute buffs/debuffs** *(depends on 1 + 2)* — area **C**.
4. **Battle runtime: DoT/HoT per-tick attributes + simulator phase** *(depends on 3)* — area **D**.
5. **UI: skill-effect indicators, active-effect chips, logs** *(depends on 2 + 3; DoT display polish with 4)* — area **E**.

## Documentation to update on landing

- `docs/game-design.md` (**Skills** + **Attributes** sections) — skills can carry timed effects; DoT/HoT as per-tick attributes (replacing the speculative wording with the shipped mechanism). (With **C**/**D**.)
- `docs/backend.md` — battle-setup section: effect runtime rules summary + the removable-modifier/attribute-recompute parity surface. (With **A**/**C**.)

## Deferred extensions (designed seams, not in V1)

- **Proc chance (`ChanceToApply`)** — lands with/after the seeded-RNG work (#178); requires a defined global RNG draw order across crit/dodge/effect rolls.
- **Hit-count durations & every-Nth-hit triggers** — the "after x hits" idea: add a duration-type discriminator (e.g. `EEffectDurationType { Time, HitsTaken }`) and/or trigger fields to `SkillEffect` when wanted; needs hit-event bookkeeping on the battler.
- **Stacking policies** — authored per effect (refresh / stack with cap) if refresh-only proves too limiting.
- **Per-skill DoT attribution** in `SkillStats` (requires tracking effect provenance through the DoT phase).
- **Dispel/cleanse and instant-heal effect kinds** — new effect kinds beyond attribute modifiers.
- **Content authoring** — once shipped, author effect-bearing skills (and revisit enemy loadouts) so the mechanic is actually encountered; content task, not code.
- **Card minigame synergy** — #259 expects skills to map onto cards; effect-bearing skills are the interesting ones there.
