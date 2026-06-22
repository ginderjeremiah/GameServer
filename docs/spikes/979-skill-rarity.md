# Spike #979 — Add skill rarity

- **Spike issue:** [#979](https://github.com/ginderjeremiah/GameServer/issues/979)
- **Status:** Design decided; one implementation sub-issue (see [Implementation issue](#implementation-issue)). Not yet implemented.
- **Related:** the `ERarity` tier is reused verbatim from items/item-mods; the field follows the just-merged `ESkillAcquisition` authoring-field pattern ([#1100](https://github.com/ginderjeremiah/GameServer/issues/1100)); rarity is the **tier weight** the proficiency XP accrual ([#982](https://github.com/ginderjeremiah/GameServer/issues/982) decision 4 / [#1116](https://github.com/ginderjeremiah/GameServer/issues/1116)) consumes via the deferred [#1123](https://github.com/ginderjeremiah/GameServer/issues/1123).

## Goal

Give skills a rarity tier (`ERarity`, Common→Mythic) **matching items and item mods**, so a skill can be authored as Common/Rare/Legendary/… and that tier is surfaced wherever skills are displayed (challenge-reward previews, codex, the Skills loadout screen, the battle tooltip).

The issue is one line ("Add skill rarity matching item and item mods rarities"). The spike's job is to pin **what rarity means for a skill**, **where the field lives**, and **the one forward seam that changes the placement** — the proficiency XP tier-weight.

## Current state (what already exists)

The primitive and every display helper already exist — this is reuse, not new machinery.

- **`ERarity`** (`Common = 1 … Mythic = 6`) is a shared domain enum with a seeded `Rarities` reference table. **Items and item mods** carry `RarityId` on their **entity + contract** (and `Game.Core.Items.Item` carries `ERarity Rarity` on the lean domain model too). EF infers the `Item.RarityId → Rarity` FK **by convention** from the `Rarity` navigation — there is no explicit `HasOne`/`HasForeignKey` config to copy.
- **Rarity is display/authoring only today.** Outside DB seeding, `.Rarity` is **never read** by any battle or domain logic — it drives tooltip/card accents, the challenge-reward sub-label, and the rarity sort/glow on richer surfaces. (`Game.Core.Items.Item.Rarity` is, today, carried but unread — effectively vestigial.)
- **The frontend rarity helpers are centralized** in `$lib/common/rarity.ts`: `rarityColor` / `rarityLabel` / `rarityGlow` / `rarityTint` over `--rarity-*` theme variables, plus admin `reference.rarityName` / `rarityColor` / `rarityOptions`.
- **The skill display surfaces already stub "no rarity"** and fall back to a neutral accent — these are the exact plug-in points:
  - `challenge-unlocks.ts` resolves a skill reward to `ERarity.Common` + `var(--accent-light)` with the comment *"Skills carry no rarity tier."*
  - `SkillRewardTooltip.svelte` hardcodes `ACCENT = var(--accent-light)` *"since skills have no rarity tier."*
  - The codex skill table/dossier are **intellect-themed** (`--attr-intellect`), not rarity-themed.
- **`ESkillAcquisition` (#1100) is the exact authoring-field template**, just merged: a new field added to the `Skill` **entity + contract + mapper + admin editor + a versioning test**, deliberately kept **off** the lean `Game.Core.Skills.Skill` because *the battle never reads provenance*. Skill rarity follows this shape — with **one deliberate divergence** (below): it *does* go on the lean core model, because a reward calc will read it.

## Decisions

Settled with the project owner during the spike:

1. **Rarity is authoring + display metadata in V1, identical in spirit to item/mod rarity** — the battle simulation never reads it. There is **no gameplay effect shipped in this issue**.

2. **But it is a _reserved logic hook_, not purely cosmetic.** Rarity is the **tier weight** the proficiency XP accrual will consume (#982 decision 4: a proficiency's XP slice ∝ `Σ(skillTierWeight × contributionWeight)`; flat `1` until #979 lands, then weighted by #1123). So the field must be positioned where that calc reads it.

3. **Placement: `Skill` entity + contract + the lean `Game.Core.Skills.Skill` model** (mapped in **both** `ToContract` and `ToCore`). This is a **deliberate divergence from the `ESkillAcquisition` precedent**, and the reason is decision 2:
   - Proficiency XP is computed **backend-authoritative at battle completion** (#982 decision 9: `BattleStatisticsEventHandler` live, the consolidation pass offline), consuming `BattleStats.SkillStats` keyed by **skill id**. The reward/gameplay read for a skill by id is `ISkills.GetSkill(id)`, which returns the **lean core `Skill`** — so `GetSkill(id).Rarity` is the natural tier-weight lookup. Acquisition stayed off the core model because nothing would ever read it; rarity *will* be read there, so it belongs on it.
   - This also brings skills into parity with `Game.Core.Items.Item.Rarity` (and turns that model's "carried but unread" rarity from vestigial into a deliberate, now-shared convention).
   - **Still zero battle-parity surface:** rarity is not read in the seeded 40 ms tick simulation, so the **frontend battle/skill model needs no rarity field** and no parity vectors change. The client gets rarity for *display* from the `ISkill` reference contract, and proficiency XP is pushed from the server (not computed in the client sim), so the client battle model never needs it either.

4. **Existing skills backfill to `Common`** in the migration (mirroring acquisition's backfill to `Player`). The core/contract model carries **no default**, so EF always writes the explicit authored value on insert; the migration default only backfills existing rows.

5. **One vertical-slice implementation issue.** The field is useless without its display, and the change is small and cohesive (no cross-entity validation, no battle assembly, no parity matrix — unlike #980). Backend field + admin editor + every skill display surface ship together.

## Changes by area

### Backend

- **Entity** `Game.Infrastructure/Entities/Skill.cs`: add `int RarityId` + a `virtual Rarity Rarity` navigation (FK inferred by convention, exactly like `Item` — no `GameContext` relationship config needed; the `Rarities` seed already supplies the FK targets).
- **Migration** `AddSkillRarity`: add the `RarityId` column, `NOT NULL`, `defaultValue: 1` (Common) to backfill existing rows. (Pattern: `20260621221535_AddSkillAcquisition`.)
- **Contract** `Game.Abstractions/Contracts/Skill.cs`: add `ERarity RarityId`.
- **Core model** `Game.Core/Skills/Skill.cs`: add `required ERarity Rarity` (the reserved hook — see decision 3).
- **Mapper** `SkillMapper`: map `RarityId` in `ToContract` **and** `Rarity` in `ToCore`.
- **Admin save** `AdminSkills.SaveSkills`: set `RarityId = (int)item.RarityId` on both the add and edit entity builds.
- **Versioning:** adding rarity to the contract changes the skills set's content hash → clients re-download skills once. Add `SkillRarityVersioningTests` (mirror `SkillAcquisitionVersioningTests`).
- **Tests:** extend `SkillMapperTests` (rarity round-trips to contract **and** core) and the admin skills integration coverage (save/edit persists rarity). Update any `Skill` core/contract test fixtures for the new `required` member.

### Frontend

- **`ISkill` regenerates** from the contract via codegen (gains `rarityId: ERarity`) — no hand-edit.
- **Admin Workbench** `entities/skill.ts`: add `rarityId: ERarity.Common` to `newItem`, a `Rarity` `select` field (`reference.rarityOptions`) in the Identity section, and `listBadge`/`badgeColor` via `reference.rarityName` / `reference.rarityColor` — copy `item.ts`.
- **Adopt the rarity hue/label** on the surfaces that currently stub a neutral accent:
  - `challenge-unlocks.ts` — skill reward uses `rarityColor(skill.rarityId)` / `rarityLabel(skill.rarityId)`, `sub: \`${rarityLabel} · Skill\``, and `rarity: skill.rarityId` (drops the `Common`/neutral fallback, so skill rewards sort/glow by real tier alongside items/mods).
  - `SkillRewardTooltip.svelte` — accent from `rarityColor(skill.rarityId)` instead of the hardcoded neutral.
  - Codex skill table/dossier — layer rarity into the accent consistently with items. *(The codex skills are intellect-themed today; the exact blend of intellect-vs-rarity accent is a small styling call at implementation time.)*
  - Skills loadout screen (`SkillRow` / `SkillRail` / `SkillInspector`) and the battle `SkillTooltip` — surface the rarity label/hue.
- **Tests:** update the touched component tests + `entities/skill.test.ts` (rarity field + badge).

### Docs to update on landing

- `docs/game-design.md` (**Skills**): skills carry a rarity tier matching items/mods — authoring + display in V1, and the **reserved tier-weight** for proficiency XP (#982/#1123).
- `docs/backend.md`: a one-liner that skill rarity lives on the entity + contract + lean core model (the divergence from the acquisition flag, and why — the proficiency XP reward read).

## Forward-compatibility — proficiency XP tier weight (#1123)

This issue ships **no** XP behavior; it just lands the data where #1123 reads it. #982 decision 4 already specifies the consumer:

- Proficiency XP per won battle is a fixed, difficulty-scaled pie split across represented proficiencies, each slice ∝ `Σ(skillTierWeight × contributionWeight)`. **`skillTierWeight` is a pure function of the skill's `ERarity`**, looked up by id at battle completion (`ISkills.GetSkill(skillId).Rarity` — the read decision 3 enables).
- #1123's only constraint on the weighting curve: **low tier must still earn nonzero XP** (slow, never walled). Defining the actual `ERarity → weight` curve is #1123's job, not this issue's — this issue just guarantees the rarity is authored and readable.
- Until #1123, #1116 uses a flat weight of `1`, so landing #979 is non-breaking for the proficiency work.

## Implementation issue

Tracked as a sub-issue of #979:

- **[#1128](https://github.com/ginderjeremiah/GameServer/issues/1128) — Add skill rarity (field + admin authoring + display)** — the full vertical slice above. Scope: medium. No dependencies (the proficiency consumer #1123 depends on *this*, not the reverse).

## Notes / follow-ups surfaced

- **Content pass (not code):** existing skills backfill to `Common`; authors re-tier skills as intended afterward. Starter skills can stay Common or be tiered to taste.
- **Balance (tuning, not code):** the `ERarity → proficiency-XP weight` curve is owned by #1123 with the "nonzero floor" guard; nothing to tune in this issue.
- **`Game.Core.Items.Item.Rarity` was unread before this** (vestigial). Putting skill rarity on the core model deliberately, with a real future reader, makes "rarity on the lean core model" a shared, justified convention rather than an accident — no separate cleanup needed.
