# Spike #980 — Allow items to grant skills

- **Spike issue:** [#980](https://github.com/ginderjeremiah/GameServer/issues/980)
- **Status:** Design decided; split into implementation sub-issues (see [Implementation issues](#implementation-issues)). Not yet implemented.

## Goal

Two related pieces, framed by the issue:

1. **Items grant skills** — an equipped item can carry a skill that becomes active while it's equipped, in addition to skills earned the existing way (challenge rewards).
2. **Categorize skills by acquisition** — `Player` / `Item` / `Enemy-Only`, with overlap supported (an item skill an enemy is also allowed to have). The issue explicitly asks for "a way to appropriately label the data here."

### Purpose (the framing that drove the design)

The point of item-granted skills is **thematic coherence, not raw power**: a melee weapon brings *Cleave*; a staff brings *Fireball*. The naive alternative — letting the player freely select any skill but gating selection by equipment category (a skill→category compatibility matrix) — is awkward: the skill sits in the loadout and greys out when you swap weapons, and you must author and explain the rule. **Equipment bringing its own skills inverts that into something self-consistent** — Cleave exists *because* the axe is equipped; swap to a staff and it's simply gone, with no rule to author and no greyed-out state.

The two halves reinforce each other: the **`Item`-only acquisition flag is what enforces the "no Cleave on a staff-user" property.** An `Item`-only skill can never be a challenge reward, so it never enters the selectable pool — the only way to have it is to equip something that grants it. The mechanic (innate grant) and the label (Item-only) are not two features that happen to ship together; the flag is what *guarantees* the property the mechanic exists for.

## Current state (what already exists)

The seams this rides on are already in place.

- **`Skill`** is id-indexed immutable reference data (`Game.Core.Skills.Skill`: `Id`/`Name`/`BaseDamage`/`Description`/`CooldownMs`/`DamageMultipliers`/`Effects`). It carries **no categorization** today — identified by raw id. Served to the client via `GetSkills`.
- **Player skill tracking** is the `PlayerSkill` join (`Selected`/`Order`); the loadout is capped at `GameConstants.MaxSelectedSkills` (= 4, matching the enemy cap). Unlock via `Player.UnlockSkill`, edited atomically via `SetSelectedSkills` (see spike [#179](./179-skill-unlock-and-selection.md)).
- **The "Player" axis already exists** — `Challenge.RewardSkillId` → `Player.UnlockSkill`. **The "Enemy" axis already exists structurally** — enemies hold `AvailableSkills` via the `EnemySkill` join and pick a loadout from them (`Enemy.SelectBattleSkills`). So **"Item" is the only genuinely new acquisition channel**; the labeling declares *intent* on top of these references.
- **Items** carry attribute lists + mod slots and contribute attribute modifiers when equipped. Crucially, `BattleSnapshot` **already captures `EquippedItems`** (item id + applied mod ids) and resolves their contributions at `ToBattler` via `resolveItem` — the same seam an item→skill grant slots into, for free.
- **Battle** consumes `player.SelectedSkills` → `BattleSnapshot.SkillIds`, reconstructed at validation via `ToBattler(resolveItem, resolveMod, resolveSkill)`. `Battler` already accepts an arbitrary number of skills (bosses field more than 4 via `SelectAllBattleSkills`), so item skills sitting *outside* the 4-cap need no change to the battler.
- **The Skills screen** currently lists every non-retired skill as an aspirational "locked" catalogue with a client-derived "unlock by" line. With acquisition variety this becomes misleading (an enemy-only skill would read as something the player can aspire to unlock).

## Decisions

Settled with the project owner during the spike:

1. **Item-granted skills are innate / always-active.** While the item is equipped, its skill is appended to the battler — **additive to, and exempt from, the player-selected 4-cap.** The cap governs only the *selected* set. The opportunity cost lives at the **gear slot** (you forgo a competing item's bundle), not at the loadout layer.
   - **Rejected — expands the selectable pool** (granted skill joins the available set; player chooses to slot it): more agency, but needs dynamic availability, an unequip→deselect cascade, and transient-unlock persistence. Layerable later if wanted.
   - **Rejected — counts toward the 4-cap:** gear competing with chosen skills is punishing.
2. **One granted skill per item** — a nullable `GrantedSkillId` on the item (mirroring `Challenge.RewardSkillId`), not a list. A weapon bringing one signature skill is the natural unit. Multi-grant is a clean later migration if ever needed; set bonuses granting skills are a *separate* entity (below), not a reason to make this a list.
3. **No snapshot shape change.** Granted skills are *derived* from the already-captured `EquippedItems` at `ToBattler` time, exactly like item attributes — not denormalized into `SkillIds`.
4. **Acquisition is a declared `[Flags]` enum** `ESkillAcquisition { None = 0, Player = 1, Item = 2, Enemy = 4 }` on the skill. Overlap (e.g. `Item|Enemy`) is natural; "Enemy-Only" = `Enemy` set with the others clear. It is **intent**; the references (challenge/item/enemy) are **reality**; backend authoring validation bridges them.
   - The flag lives on the **entity + contract** (admin tooling + codex), **not** on the lean `Game.Core` battle `Skill` — the battle never reads provenance, so keeping it off the battle model keeps that model lean and parity-safe.
5. **Remove the locked/aspirational catalogue from the Skills loadout screen.** The screen shows only the player's unlocked skills + the equipped band. The "how do I get this skill?" responsibility moves entirely to the **Codex** — which must gain that information **first** (so there is never a release window where the unlock info lives nowhere).

### Battle-assembly rules (parity-critical)

Battle logic runs on both sides and must agree tick-for-tick, so the granted-skill assembly is specified exactly and mirrored:

- **Order:** player-selected skills first (in their chosen order), then granted skills in `EEquipmentSlot` order (Helm→Accessory). Deterministic, so the cooldown tie-break matches on both sides.
- **Dedupe by id, first wins:** a granted skill that duplicates a selected skill (or another item's grant) appears once. (Stacking would be a deliberate future feature, not a default.) The Skills-screen UI surfaces the dropped duplicate ("already in your loadout") so it isn't invisibly wasted.
- Each appended skill is wrapped in a normal `BattleSkill` — charge, effects, scaling, and cooldown all work unchanged.

### Deliberate asymmetry

Players can field more than 4 skills (selected + granted); enemies cannot (they can't equip items). This is an intentional player-favoring break in the documented player/enemy skill symmetry — the same category as player-only crit/dodge/block — and should be noted as such in the design doc.

### Flags declare *allowed*, not *exists*

A skill can be `Item`-flagged with no item granting it. So the **codex provenance must display actual references** ("Granted by *Staff of Embers*", "Rewarded by *challenge X*"), using the flag only to decide the "enemy-only / not obtainable" case — otherwise it would claim "obtainable via item" when nothing grants it.

## Changes by area

### A. Skill acquisition flags + authoring validation

- `ESkillAcquisition` `[Flags]` enum (domain enum, codegen'd to TS).
- Bitmask column on the `Skill` entity (+ migration; existing rows default to `Player`) and the contract + mapper. Skills version hash changes (one-time client re-download).
- Backend validation on admin save (anti-tamper): challenge `RewardSkillId` → `Player`-flagged; enemy `AvailableSkills` → `Enemy`-flagged. (The `Item` axis lands in **B**.)
- Admin Workbench: Skill editor flags field; filter the challenge-reward and enemy-skill pickers by flag.

### B. Items grant an innate skill

- Nullable `GrantedSkillId` on the `Item` entity (+ FK, migration), domain model (id only — keeps the item cache independent of the skill cache), contract, mapper.
- `BattleSnapshot.ToBattler`: gather granted skills from the equipped items → dedupe → order (per the rules above) → append to the battler. Structure the gather as "from all active grant sources" so set bonuses (below) are an additive source, not a rewrite.
- Frontend: mirror the assembly in the battle build (`BattleEngine`/`BattleSimulator`); add parity-matrix rows (grants a skill; two items same skill; grant duplicates a selected skill; grant with effects). Item tooltip/card "Grants:" line; innate skill rendered read-only in the loadout band.
- Admin Workbench: Item editor "Granted Skill" picker (filtered to `Item`-flagged, retired excluded) + backend validation.

### C. Codex skill provenance

- Skills dossier shows how each skill is obtained, derived from actual references (challenge `RewardSkillId`, item `GrantedSkillId`), with the flag deciding the "enemy-only / not obtainable" wording. Client-side from cached reference data; no new backend read.

### D. Skills screen cleanup

- Skills loadout screen shows only unlocked skills + equipped band; drop the locked catalogue, the filter toggle, and the client-side "unlock-by" derivation. Update `docs/frontend-screens.md`.

## Forward-compatibility — equipment sets / set bonuses

Out of scope here, but the V1 design is chosen so set bonuses can grant skills later as a purely additive change:

- A set bonus granting a skill is just **another source** of granted skills. Because **B** writes the assembly as "gather from all active grant sources → dedupe → order" (not "iterate equipped items"), adding sets is additive — no rewrite of the battler skill assembly.
- The **parity/snapshot story extends for free**: a set bonus is conditional on equipped-set-count, which is fully derivable from the `EquippedItems` already in the snapshot. So set-granted skills reconstruct from the same snapshot with no new field — the same guarantee item skills get.
- The genuinely hard part of sets is the **conditional attribute modifiers** (bonuses keyed to equipped count); skill-granting is a small rider on whatever set-bonus machinery is built. Nothing in V1 needs to anticipate sets beyond the one composition seam above.

## Implementation issues

Tracked as sub-issues of #980:

1. **[#1100](https://github.com/ginderjeremiah/GameServer/issues/1100) — Skill acquisition flags (Player/Item/Enemy) + authoring validation** *(foundation; no dependencies)* — area **A**.
2. **[#1101](https://github.com/ginderjeremiah/GameServer/issues/1101) — Items grant an innate skill when equipped** *(depends on #1100)* — area **B**.
3. **[#1102](https://github.com/ginderjeremiah/GameServer/issues/1102) — Codex: surface skill acquisition/provenance** *(depends on #1100, #1101)* — area **C**.
4. **[#1103](https://github.com/ginderjeremiah/GameServer/issues/1103) — Skills screen: remove locked/aspirational skills** *(depends on #1102)* — area **D**.

**#1100** is the foundation. **#1101** builds on it (the mechanic). **#1102** (codex provenance) must precede **#1103** (skills-screen cleanup) so the "how do I get this skill?" info never lives nowhere.

## Documentation to update on landing

- `docs/game-design.md` (**Skills** / **Items** sections) — items can grant an innate skill active while equipped, additive to and exempt from the loadout cap; skills carry an acquisition classification (Player/Item/Enemy-Only); the deliberate player/enemy skill-count asymmetry. (Update with **A**/**B**.)
- `docs/backend.md` — a short note on the acquisition flag (authoring intent vs. reference reality, validation bridges them) and the item-grant battle-assembly rule (derived from the snapshot's equipped items; order + dedupe for parity). (Update with **A**/**B**.)
- `docs/frontend-screens.md` — the Skills screen losing its locked catalogue, and the Codex Skills dossier gaining provenance. (Update with **C**/**D**.)

## Notes / follow-ups surfaced

- **Balance (tuning, not code):** innate item skills have no loadout-layer cost — the cost is the gear slot. Item-skill items become auto-equips if competing gear isn't compelling. Watch during the balancing pass; treat the weapon's *bundle* (attributes + its skill) as the unit you balance.
- **Content pass:** existing skills default to `Player` in the migration; enemy/item skills need re-flagging, and starter skills must stay `Player`. Not part of any code issue.
- **The `Enemy` flag is the most optional of the three** — `EnemySkill` membership already *is* the enemy categorization, so the flag is pure guard + picker filter there. Kept for symmetry and so "Enemy-Only" is a positive declaration rather than a derived double-negative.
- **Stacking duplicate grants** (a granted skill firing twice) is intentionally rejected for V1 in favour of dedupe; it's the seam where a future "stacking" build feature would live.
