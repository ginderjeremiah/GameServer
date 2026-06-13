# Spike #528 — Attribute enhancements

- **Spike issue:** [#528](https://github.com/ginderjeremiah/GameServer/issues/528)
- **Status:** Design decided; split into implementation sub-issues (see [Implementation issues](#implementation-issues)). Not yet implemented.

## Goal

Give Attributes a richer presence in the UI: an **icon** for each attribute (rendered wherever attributes appear), a proper **tooltip** for the attribute behind a skill effect during combat, and a set of **display/classification fields** on the attribute model so the UI stops hardcoding that knowledge. The new fields flow from the backend to the client and are mostly used there.

## Current state (what already exists)

- **Attributes are intrinsic, enum-keyed reference data — not DB-authored content.** `EAttribute` (`Game.Core/Enums.cs`) has 17 values: 6 core (STR/END/INT/AGI/DEX/LUK), 3 implemented derived (`MaxHealth`/`Defense`/`CooldownRecovery`), 5 unimplemented (crit/dodge/block), the two per-second DoT/HoT channels (`DamageTakenPerSecond`/`HealthRegenPerSecond`), and obsolete `DropBonus`.
- **The domain object is thin.** `Game.Core.Attributes.Attribute` carries only `Id`/`Name`/`Description` (Name and Description derived from the enum) and an `IsCore`/`CoreAttributes` concept. It flows to the client via the `GetAttributes` socket command → `Game.Api.Models.Attributes.Attribute` → codegen'd `IAttribute` (`id`/`name`/`description`). The DB `Attributes` table is seeded from the enum (`HasData`) purely for integrity and is never queried.
- **The metadata this spike wants already exists — hardcoded on the frontend:**
  - `ATTRIBUTE_GROUPS` (`Core`/`Derived`) + a `BREAKDOWN_ATTRS` allow-list + per-attribute decimal precision live in `attribute-breakdown-view.svelte.ts`.
  - `HARMFUL_WHEN_RAISED` = `{DamageTakenPerSecond}` lives in `skill-effect-display.ts`.
  - `attributeColor` / `attributeCode` (the `STR/END/…` codes + the themeable `--attr-*` hues) live in `attribute-display.ts`.
- **Two distinct notions of "core."** `Attribute.IsCore` / `CoreAttributes` is a **domain invariant** consumed by the exp-power calc (`DefeatRewards` sums the additive *core* attribute amounts). It is unrelated to the *display* categorization this spike adds, even though the two coincide on the 6 allocatable attributes.
- **The combat effect tooltip is a bare `title=`.** The active-effect chips (`ActiveEffectChips.svelte`) explain themselves with a native `title` attribute, not the shared tooltip chrome (`components/tooltip`).

So most of this spike is **promoting scattered frontend constants into the Attribute domain object** as a single source of truth, plus adding icons and a real tooltip.

## Decisions

Settled with the project owner during the spike:

1. **`AttributeType` = `Primary` / `Secondary` / `Status`** (new domain enum `EAttributeType`, codegen'd to the client):
   - **Primary** — the 6 allocatable attributes.
   - **Secondary** — aggregate stats with a base/derived formula (`MaxHealth`, `Defense`, `CooldownRecovery`, and the future crit/dodge/block set).
   - **Status** — transient per-second combat channels fed only by effects/gear (`DamageTakenPerSecond`, `HealthRegenPerSecond`).

   This replaces `Core`/`Derived`, whose "derived" overclaimed: `MaxHealth` has a base independent of the core attributes, and the per-second channels come from gear/buffs, not from core at all. The taxonomy is **display-only** and stays distinct from the `IsCore`/`CoreAttributes` power-calc invariant (`Primary` is expected to equal `CoreAttributes`, but the two are not collapsed).
2. **The icon path is a frontend-owned enum map**, not a contract field. Attributes are intrinsic enum-keyed data whose other visual facets (`attributeColor`/`attributeCode`) are already frontend maps, the assets live in `UI/static/img`, and there is no authoring — so a backend `iconPath` would only hardcode a UI path it otherwise never references. `attributeIcon(id)` joins `attribute-display.ts`.
3. **Icon art covers the 11 currently-visible attributes** — the 6 core + `MaxHealth`/`Defense`/`CooldownRecovery` + the two per-second channels (which surface as DoT/HoT effect chips in combat). The unimplemented crit/dodge/block and obsolete `DropBonus` get no art until they are real.
4. **The breakdown page is self-selecting.** Instead of a hardcoded allow-list, the breakdown shows any attribute with **≥1 non-combat modifier** (a contributor whose source is not `SkillEffect`). This drops obsolete/unimplemented attributes, keeps the per-second Status channels off the persistent inspector (they only ever get combat modifiers), and auto-includes crit/dodge/regen-gear once they gain real contributors.
5. **No DB migration for the new fields.** They are derived, never-queried display metadata; the `Attributes` table stays `Id`/`Name`/`Description`. Adding them to the contract changes the attribute reference-data version hash (a one-time client re-download), which is expected.
6. **Accent hues for Secondary/Status attributes are deferred** — only the 6 core have `--attr-*` hues today; the rest stay neutral for now.

## Enriched attribute model

New fields on `Game.Core.Attributes.Attribute` (mapped per-attribute, same switch style as `GetDescription`), surfaced on `Game.Api.Models.Attributes.Attribute` and codegen'd onto `IAttribute`:

| Field | Type | Purpose | Replaces (frontend) |
|---|---|---|---|
| `AttributeType` | `EAttributeType` | Primary / Secondary / Status display taxonomy | `ATTRIBUTE_GROUPS` |
| `IsPercentage` | `bool` | Value reads as a percentage (crit/dodge/block, `CooldownRecovery`) | — (new; impactful once crit/dodge land) |
| `IsHarmful` | `bool` | Raising it is detrimental to the bearer (`DamageTakenPerSecond` only) | `HARMFUL_WHEN_RAISED` |
| `Code` | `string` | Short code (`STR`/`END`/…) | `ATTRIBUTE_CODE` |
| `DisplayOrder` | `int` | Canonical UI ordering | implicit enum/list order |
| `Decimals` | `int` | Display precision (`CooldownRecovery` = 2, most = 0) | `BREAKDOWN_ATTRS.dec` |

`EAttributeType` is a domain enum (not a DTO), hand-maintained and codegen'd to the frontend via the existing domain-enum codegen (#282), like `ESkillEffectTarget`. `IsHarmful` is display-only (buff/debuff tinting) and never used in battle math, so moving it server-side is **parity-safe**.

## Changes by area

### A. Backend — attribute metadata foundation
`EAttributeType` + the six fields on the domain object and API model; per-attribute mapping; codegen of the enum and `IAttribute`. No migration. Unit-test the mapping (every value resolves; `Primary` == `CoreAttributes`).

### B. Frontend — consume the metadata, delete the constants
Read `attributeType`/`isPercentage`/`isHarmful`/`code`/`decimals` off `staticData.attributes`; remove `HARMFUL_WHEN_RAISED` (thread `isHarmful` into `effectDirection`/`describeEffect`/`effectLogMessage` like `attributeName`), `ATTRIBUTE_GROUPS`, `ATTRIBUTE_CODE`, and the `BREAKDOWN_ATTRS` allow-list/precision (group by `attributeType`, select by the "has a non-combat modifier" rule). `attributeColor` stays frontend (theme).

### C. Icons — art + render
Generate the 11 icons via the locked `/image` → `strip-bg.py` pipeline (`docs/icon-art.md`), catalog each prompt, add `attributeIcon(id)` to `attribute-display.ts`, and render on the effect chips, breakdown rail, point-buy steppers, and skill scaling chips.

### D. Attribute tooltip
A reusable `AttributeTooltip` on the shared tooltip chrome (icon + name + type + description; + effect direction/magnitude/duration in the chip context), replacing the native `title=` on `ActiveEffectChips` and reused on the other attribute surfaces.

## Implementation issues

Tracked as sub-issues of #528:

1. **[#529](https://github.com/ginderjeremiah/GameServer/issues/529) — Enrich the Attribute domain object + contract with display metadata** *(foundation)* — area **A**.
2. **[#530](https://github.com/ginderjeremiah/GameServer/issues/530) — Frontend: consume attribute metadata, remove hardcoded constants** *(depends on #529)* — area **B**.
3. **[#531](https://github.com/ginderjeremiah/GameServer/issues/531) — Attribute icons: generate art + render where appropriate** *(parallel with #530)* — area **C**.
4. **[#532](https://github.com/ginderjeremiah/GameServer/issues/532) — Attribute tooltip component (combat effect chips + reuse)** *(depends on #529 + #531; degrades gracefully)* — area **D**.

**#529** is the foundation; **#530** follows it, **#531** can run in parallel, and **#532** lands once the data (#529) and icon (#531) exist.

## Documentation to update on landing

- `docs/frontend-screens.md` — the Attributes and Attribute Breakdown screen notes (Primary/Secondary/Status grouping; the self-selecting "has a non-combat modifier" breakdown membership).
- `docs/frontend.md` — the attribute-display helper note, if the `attributeCode`/icon source-of-truth split is worth a cross-cutting mention.
- `docs/icon-art.md` — the per-attribute icon prompt catalog entries (with **C**).

## Out of scope / deferred

- **Accent hues for Secondary/Status attributes** (decision 6) — keep them neutral for now.
- **The `IsPercentage` storage convention** (5 vs 0.05) — settled when crit/dodge are implemented; this spike adds only the display flag.
- **Icons for unimplemented crit/dodge/block and obsolete `DropBonus`** — generated when those mechanics become real.
