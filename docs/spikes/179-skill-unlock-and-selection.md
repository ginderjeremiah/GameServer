# Spike #179 — Make skills unlockable + add a skill-selection page

- **Spike issue:** [#179](https://github.com/ginderjeremiah/GameServer/issues/179)
- **Status:** Design decided; split into implementation sub-issues (see [Implementation issues](#implementation-issues)). Not yet implemented.
- **Design input:** `docs/designs/game/skills/` — the **Skills Inspector — Game Theme** mock is the target UI for the (follow-up) page. The mock's wireframe history is in `docs/designs/game/skills/chat.md`.

## Goal

Two related pieces, framed by the issue:

1. **Make skills unlockable** — today every player simply starts with the same starter skills and there is no way to earn more. Skills should be unlockable the same way items and item mods are: as a **challenge reward**.
2. **Support a skill-selection page** — give the player a screen to see every skill they have unlocked and choose which (and in what order) are equipped for battle. This spike covers the **backend support** for that page; the **page UI itself is an explicit follow-up** (per the issue).

## Current state (what already exists)

The data model is already half-built for this — the persistence shape for "a player owns a set of skills, some of which are equipped" exists, it just isn't earnable or editable yet.

- **`Skill`** is static reference data (`Game.Infrastructure.Entities.Skill` → cached, id-as-index): `Id`, `Name`, `BaseDamage`, `Description`, `CooldownMs`, `IconPath`, + `SkillDamageMultipliers`. Served to the client as the `Skill` read contract via the `GetSkills` socket command (so the client already has the full catalog).
- **`PlayerSkill`** join entity already exists: composite key `(PlayerId, SkillId)` + a `Selected` bool. **There is no ordering column.**
- **`Player` aggregate** already splits `Skills` (all unlocked) from `SelectedSkills` (equipped) — but has **no** unlock / select / reorder methods (unlike items/mods, which have `UnlockItem`/`UnlockMod`/`TryEquipItem`/…).
- **New players** get `NewPlayerFactory.StarterSkillCount` (= 3) starter skills (ids 0–2), all `Selected = true`.
- **Battle** consumes `player.SelectedSkills` via `BattleSnapshot.FromPlayer` → `SkillIds`, reconstructed at validation via `ToBattler`. Order today is whatever order the mapper produced (non-deterministic — see parity note below).
- **`PlayerData`** (the player payload sent to the client) carries only `SelectedSkills` (the equipped ids). It does **not** send the full unlocked set.

### The two patterns we mirror

- **Challenge-reward unlock (items/mods):** `Challenge` carries `RewardItemId` / `RewardItemModId`. On battle completion, `BattleStatisticsEventHandler` evaluates challenges; for each newly-completed one it calls `player.UnlockItem(item)` / `player.UnlockMod(modId)`. Those raise `ItemUnlockedEvent` / `ModUnlockedEvent`, which `PlayerPersistencePublisher` pushes onto the Redis write-behind queue and `DataProviderSynchronizer` applies as an idempotent insert.
- **Player mutation over the socket (favorite / log prefs):** `SetItemFavorite` / `SaveLogPreferences` socket commands → `PlayerService` mutates the cached domain player + `SavePlayer` → the domain event flows through the same write-behind pipeline.

## Decisions

Settled with the project owner during the spike:

1. **Skills unlock as a challenge reward**, exactly mirroring items/mods. Add a third optional reward `RewardSkillId` to `Challenge` (additive — a challenge may grant any combination of item/mod/skill, as item+mod already can).
2. **Loadout cap = 4 (fixed).** A single `Game.Core` constant, matching the enemy loadout cap (`Enemy.SelectBattleSkills` ≤ 4) for player/enemy symmetry. Enforced on the backend as anti-cheat. (3 starter skills ≤ 4, so new players are valid. Scaling the cap with progression is a possible later enhancement.)
3. **Loadout order is persisted and meaningful.** Order drives in-game display order and is the current (incidental, not-yet-designed) tie-break when two skills come off cooldown on the same tick. Add an `Order` column to `PlayerSkill`; map `SelectedSkills` deterministically ordered.
4. **Mutations go over the WebSocket**, aligned with the documented "move all gameplay comms to WebSockets" direction (`backend.md`) — not a new HTTP endpoint, even though the sibling equip-item is still HTTP.
5. **One atomic "set loadout" command.** A single `SetSelectedSkills(orderedSkillIds)` command replaces the whole equipped set in order — handling select, deselect, and reorder through one path. Chosen over discrete select/deselect/reorder commands because it is atomic, race-free on ordering, and matches the page's "save loadout" interaction.
6. **The selection page needs the full unlocked set**, so `PlayerData` must additionally carry the player's unlocked skills with their selected state + order (mirroring how `InventoryData.UnlockedItems` carries an `Equipped` flag per item). The "unlocked by which challenge" line the mock shows is **derivable on the client** from the challenge catalogue it already loads (`RewardSkillId`) — no extra backend read.
7. **The page UI is a follow-up** (per the issue): this spike delivers only the backend/contract support. The design mock (`Skills Inspector — Game Theme.html`) is the target.

### Loadout column shape (sub-decision)

`PlayerSkill` keeps `Selected` and gains `Order int` (additive, no destructive backfill). The single `SetSelectedSkills` writer always rewrites both columns together, so the redundancy between "is selected" and "has a position" cannot drift in practice.

- The **alternative considered** was replacing `Selected` with a nullable `Order` (null = not equipped), which is more normalized (one source of truth) but requires a data backfill of existing selected rows and touches every existing `Selected` read. Rejected for this pass as higher-risk for little gain given the single-writer guarantee; revisit if a second writer ever appears.
- **Mapping must produce a deterministic total order** — order `SelectedSkills` by `(Order, SkillId)` — so existing rows (which all default to `Order = 0` until the player first saves a loadout) still resolve to a stable order. Because the equipped order is captured into `BattleSnapshot.SkillIds` and sent to the client ready-made, the frontend and backend simulate from the identical order by construction (battle-parity is preserved as long as the backend's order is deterministic).

## Backend changes by area

### A. Skill unlock via challenges
- `Challenge`: add `RewardSkillId int?` to the **entity** (`Game.Infrastructure.Entities.Challenge`, + optional `RewardSkill` nav), the **read/write contract** (`Game.Abstractions.Contracts.Challenge`), and the **domain model** (`Game.Core.Progress.Challenge`).
- Propagate it through `Challenges.All()` (repo projection), `GetChallenges.ToContract`, and `AdminChallenges` (add/edit insert/update). The admin Workbench challenge editor gains a **skill-reward picker** (frontend admin field).
- `CompletedChallenge` + `PlayerProgress.EvaluateChallenges`: carry `RewardSkillId`.
- `BattleStatisticsEventHandler`: when a completed challenge has a `RewardSkillId`, resolve it via `ISkills` (new injected dependency) and call `player.UnlockSkill(skill)`.
- `Player.UnlockSkill(Skill)` domain method → raise a new `SkillUnlockedEvent`. Adds the skill to `Skills`, **unselected** (earning a skill does not auto-equip it).
- `PlayerPersistencePublisher`: register the new event. `DataProviderSynchronizer`: `HandleSkillUnlocked` → idempotent insert of a `PlayerSkill` row (`Selected = false`, `Order = 0`).
- **EF migration:** `Challenge.RewardSkillId` (+ FK).

### B. Loadout persistence + rules foundation
- `PlayerSkill`: add `Order int` (entity + `GameContext` config). **EF migration.**
- `PlayerMapper.ToCore`: order `SelectedSkills` by `(Order, SkillId)`. `PlayerMapper.ToEntity` + `NewPlayerFactory`: assign `Order` (0..n-1) to the starter skills.
- `NewPlayerSkill`: add `Order`.
- Define the loadout-cap constant (`= 4`) in `Game.Core`.

### C. Expose unlocked skills to the client
- `PlayerData`: add the unlocked-skill set with `selected` + `order` per skill (shape parallel to `InventoryData.UnlockedItems`). Keep or fold in the existing `selectedSkills` (the client can derive the ordered equipped list from the richer set; decide during implementation whether to keep the convenience field). Also expose the loadout cap (e.g. `maxSelectedSkills`) so the client renders "X / 4" without hardcoding.
- Regenerate the TS client (`IPlayerData`) and update `player-manager.ts` to read the new field.

### D. `SetSelectedSkills` command + orchestration
- `Player.TrySetSelectedSkills(orderedSkillIds)`: validate every id is **unlocked**, **no duplicates**, **count ≤ cap**; on success replace the equipped set + order and raise a single `SelectedSkillsChangedEvent { PlayerId, OrderedSkillIds }`.
- `PlayerService.SetSelectedSkills`: mutate cached player + `SavePlayer`.
- `SetSelectedSkills` socket command (mirrors `SaveLogPreferences`): takes `List<int>`, returns success/error.
- `PlayerPersistencePublisher` + `DataProviderSynchronizer`: `HandleSelectedSkillsChanged` → for the player, clear all `Selected`/`Order`, then set `Selected = true`, `Order = index` for each id in the list (delete-then-rebuild style, idempotent — same shape as the mod-applied handler).

## Battle-parity note

`SelectedSkills` ordering is now meaningful, and **battle logic runs on both the frontend and backend**, so the ordered equipped set must be identical on both. It already is by construction (the order is captured into `BattleSnapshot.SkillIds` at battle start and sent to the client), provided the backend's `SelectedSkills` order is deterministic — hence the `(Order, SkillId)` ordering in the mapper. No new parity test vector is required for the *unlock/selection* logic itself (it is not battle simulation), but any change to how equipped order is derived must keep the two sides in lockstep.

## Implementation issues

Tracked as sub-issues of #179:

1. **[#261](https://github.com/ginderjeremiah/GameServer/issues/261) — Skills unlockable via challenge rewards (`RewardSkillId`)** *(independent — ✅ landed)*: area **A** above, end to end (entity/contract/domain, repo + admin authoring + Workbench picker, `Player.UnlockSkill` + `SkillUnlockedEvent` + write-behind handler, migration, tests).
2. **[#260](https://github.com/ginderjeremiah/GameServer/issues/260) — Persist skill loadout order + loadout-cap foundation** *(foundation)*: area **B** (`PlayerSkill.Order` + migration, ordered mapping, starter-skill order, cap constant).
3. **[#262](https://github.com/ginderjeremiah/GameServer/issues/262) — Expose unlocked skills to the client via `PlayerData`** *(depends on #260)*: area **C** (contract + codegen + `player-manager`).
4. **[#263](https://github.com/ginderjeremiah/GameServer/issues/263) — `SetSelectedSkills` socket command + loadout orchestration** *(depends on #260)*: area **D** (domain mutation + validation/cap, `PlayerService`, socket command, write-behind handler, tests).
5. **[#264](https://github.com/ginderjeremiah/GameServer/issues/264) — Skills selection page UI** *(follow-up; depends on #262 + #263)*: the player-facing page, porting the `Skills Inspector — Game Theme` mock; documents the screen in `docs/frontend-screens.md`.

**#260** is the foundation; **#262** and **#263** depend on it and can proceed in parallel; **#264** is the follow-up page and depends on **#262 + #263**. **#261** is independent of the rest and can land any time.

## Documentation to update on landing

- `docs/game-design.md` (**Skills** section) — skills are unlockable via challenges and equippable up to a cap of 4, with a meaningful loadout order. (Update when **A**/**B**/**D** land.)
- `docs/backend.md` — a short "skill loadout" design note alongside the battle-setup / reference-data sections (the `PlayerData` unlocked-skill projection, the `SetSelectedSkills` write path, cap-as-anti-cheat). (Update when **C**/**D** land.)
- `docs/frontend-screens.md` — the new selection screen. (Update when **E** lands.)

## Notes / follow-ups surfaced

- **Seed data:** seeded challenges have no skill reward; authoring a few `RewardSkillId` challenges (so the unlock path is exercisable) is a small content follow-up, not part of issue **A**.
- **`Skill` domain model lacks `IconPath`** (it's on the entity + contract). The selection page gets the icon from the `Skill` contract via `GetSkills`, so this is not blocking — noted only in case a future gameplay path needs it on the domain model.
- **Cap scaling** (grow the loadout cap with level / via a challenge) is intentionally out of scope; the fixed-4 constant is the seam where it would later be made dynamic.
