# Spike #1390 — Content authoring pipeline: source-controlled content + progression-graph lint

- **Spike issue:** [#1390](https://github.com/ginderjeremiah/GameServer/issues/1390)
- **Status:** Design decided (ideation complete); implementation split into sub-issues (see [Implementation issues](#implementation-issues)). Nothing implemented yet.
- **Related:** builds on the reference-data architecture ([docs/backend.md → Reference Data](../backend.md#reference-data)); subsumes the hand-maintained `e2e-seed.sql`; the macro-strategy home it asked for — [`docs/content-design.md`](../content-design.md) — already exists.

## Goal

Content authoring has started in earnest, and the admin **Workbench** already covers catalogue *values* well (add/edit/retire, per-entity validation, cross-ref pickers). This spike scopes the **tooling and process** around authoring — not the content itself. Three gaps:

1. **No source-controlled snapshot.** Static content lives **only in Postgres**, authored at runtime. A content change can't be diffed, reviewed, reproduced in a fresh dev/test/CI DB, or rolled back, and a freshly-migrated database boots with **zero** content.
2. **No whole-graph integrity check.** Content is a heavily cross-referenced graph; save-time invariants are enforced **per entity**, but nothing checks the **whole graph** for reachability — the dangling end of a broken reference lives on the *other* record.
3. **Nowhere for per-piece design intent.** The Workbench holds values, not rationale.

## Current state (validated against the code)

- **Intrinsic vs. static seeding.** `GameContext.OnModelCreating` seeds the **intrinsic**, enum-backed sets via `HasData` (Attributes, Rarities, Roles, LogTypes, ChallengeTypes, StatisticTypes, ItemModTypes, ItemCategories, EquipmentSlots, TagCategories). The **static** content graph — `Skill`, `Item`, `ItemMod`, `Enemy`, `Zone`, `Challenge`, `Class`, `Path`/`Proficiency`, `SkillRecipe` — is authored at runtime and is **not** encoded in the app or its migrations, so a fresh DB has none of it. Confirmed.
- **The read contract is already the right export shape.** Reference reads return `Game.Abstractions.Contracts` — one published language shared by the client loading screen and the Workbench — and the top-level contracts are **fully nested**: `Contracts.Skill` embeds `DamageMultipliers`, `Effects`, and `DamagePortions`; `Zone` embeds its enemies; etc. The export is therefore a near-trivial serialization of an **existing, already-materialized, published projection**.
- **The write path is split.** Admin saves take a top-level `IReadOnlyList<Change<Contracts.X>>` **plus separate child endpoints** (e.g. `SetSkillPortions`, `SetSkillMultipliers`, `SetSkillEffects`), each keyed by parent id and reconciled by `ChangeSetProcessor` / `KeyedChangeSetProcessor` with per-entity anti-tamper invariants and `ReferenceFieldValidation`. **Read is nested; write is split** — this asymmetry is the crux of the import design.
- **The id contract.** Static sets are cached as immutable lists **indexed by a 0-based, contiguous id**; each snapshot **asserts `Id == index`** as it builds. Records are **retired, not deleted** (nullable `RetiredAt`), so slots are permanent and the cache stays gap-free. Tags are the exception (own identity, hard delete). Any export/import must preserve explicit ids and retire timestamps exactly.
- **Cache + cross-instance.** Caches are eager build-then-swap snapshots; `AdminCacheReloadFilter` does an awaited local reload after a write and broadcasts a Redis invalidation (`IReferenceDataChangeNotifier`) that other instances debounce into a background reload.
- **The only existing source-controlled seed is `e2e-seed.sql`** — a 187-line, hand-maintained, idempotent SQL script seeding a minimal slice (the punch fallback + class-kit skills 0–3, one zone with enemies, a boss) so the Playwright stack can reach the game. It is applied after migration and deliberately kept separate from the app. It is the precedent — and the thing a general pipeline should **subsume**.

## Decisions

### 1. Authoring model: **(A) DB-primary + one-way export.** Confirmed.

The Workbench's pickers, per-entity anti-tamper save invariants, and the 0-based-contiguity + retire-don't-delete contract **all live in the DB write path**. Files-primary (B) would have to re-implement that entire safety net against text files — cross-ref integrity, contiguity, the `Id == index` invariant, retire timestamps, the acyclic recipe/prerequisite checks. Because the read contract is already a clean nested published projection, (A) buys ~90% of the git-native benefit (diff / review / reproduce / rollback / CI-lint) for ~10% of the cost. The spike confirms nothing about the read projection blocks a deterministic export, so (B) is unnecessary.

### 2. Serialization shape: **per-set files, id-ordered, nested children, canonically formatted.**

- **One file per static reference set** (`skills.json`, `items.json`, `item-mods.json`, `enemies.json`, `zones.json`, `challenges.json`, `classes.json`, `paths.json`, `skill-recipes.json`), **not** per-zone "content packs." The id-as-index contiguity makes an id-ordered array the natural, stable shape; content packs would fragment a heavily cross-referenced graph, complicate the contiguity guarantee, and scatter one logical edit across many files. Per-set files keep each diff local to the set that changed.
- **Determinism rules (the make-or-break for review-friendly diffs):** top-level array sorted by id (== index); child collections in a declared stable order (e.g. damage portions by `EDamageType`, effects by a stable key); canonical number formatting via invariant culture with fixed rendering for `decimal`/`double` so `1` vs `1.0` vs `1.00` never churn; `RetiredAt` normalized to a stable UTC ISO-8601 (or null); **retired rows are included** — they hold permanent slots. Intrinsic/enum sets are **not** exported (they live in `HasData`/enums); only the 9 static sets.
- **Source:** serialize the existing nested **read contracts** per set through a single canonical `JsonSerializerOptions` plus the ordering rules above.

### 3. Import / seed path: **dedicated bulk seeder, not change-set replay.**

The read(nested)/write(split) asymmetry forces a choice:

- **(i) Replay through the admin repos** in dependency order reuses every save invariant and reconciler, but drags in the split child endpoints, the awaited-reload filter, and whole-graph cross-ref validation that can't satisfy forward references mid-import (chicken-and-egg).
- **(ii) A dedicated bulk seeder** writes entities directly with **explicit ids** (identity insert, preserving 0-based contiguity + the `Id == index` assertion), preserves `RetiredAt`, inserts in FK/dependency order (referenced sets before referencing sets, parents before children), coexists with the intrinsic `HasData` seeds already present post-migration, and warms/reloads the caches once at the end.

**Recommend (ii).** A fresh-DB seed is a trusted, whole-graph operation; it needs correctness, speed, and exact id/retire fidelity — not the incremental per-edit anti-tamper path. This seeder gives dev / CI / recovery a real content baseline, **subsumes `e2e-seed.sql`** (delete it once the seeder covers that slice), and hands the offline-rewards (#879) and battle test fixtures real content instead of bespoke rows.

### 4. Round-trip / drift policy: one-way, **CI-enforced.**

JSON is *generated, never hand-edited.* Enforce with a **CI drift guard**: a test that re-derives the export from a freshly-migrated-and-seeded DB and asserts byte-equality with the committed files — a hand-edit or a stale snapshot fails the build. Authoring stays in the Workbench; a content change is landed by running the exporter and committing the diff (document the workflow; an optional admin "Export" affordance that writes the repo files is a nice-to-have, not required).

### 5. Progression-graph lint: a shared reachability checker, surfaced as **a CI test (must-have) and an admin "Content Health" view (nice-to-have).**

Per-entity saves validate one side of a reference; the lint checks the **whole graph**, where the dangling end lives on the other record. Enumerated checks:

- Zone gated by a challenge that can never be completed (challenge retired, or its completion target unreachable).
- Challenge rewarding a retired / nonexistent item or skill.
- **Orphan skills:** an `Item`-flagged skill with no granting item; a `Synthesis`-flagged skill with no producing recipe; a `Player`-flagged skill no proficiency milestone ever grants (acquisition *intent* vs. graph *reality*, across records).
- Milestone / level-reward → dangling skill id; proficiency prerequisite → retired/frozen path.
- Item requiring a proficiency level that's unreachable (path frozen/retired, or level above the path's max).
- Weapon `WeaponType` / `GrantedSkillId` stranding, and the single-non-retired-Home-zone rule — both per-save guarded today; the lint is the **whole-graph backstop** that catches cross-record breakage a per-entity save can't.
- Recipe inputs that can never be owned; dead-end zone-unlock chains; a zone with no unlock path from the start zone.

Home: one backend reachability **service** powers both (a) a CI test over the exported JSON (gates merges, cheap, the must-have) and (b) a read-only admin **Content Health** view surfacing the same warnings live (nice-to-have). They share one core checker.

### 6. Per-piece design intent: an authoring-only `DesignerNotes` field.

Add `string DesignerNotes` to the 9 static reference entities, following the established authoring-metadata pattern (same shape as `IconPath` / word-of-power): it lives on the **entity + contract only**, is surfaced in the Workbench, is **never** put on the lean `Game.Core` battle model and **never** sent to the game client, and **is** exported (per-piece rationale is worth version-controlling). Backfills to `""`; bumps each set's version hash once. Alternative homes (a sidecar notes file, the content-design doc) are rejected — per-piece rationale belongs next to the piece, and the Workbench is where authoring happens. (The macro arc lives in `docs/content-design.md`; see below.)

### 7. Content design doc: **already satisfied.**

The explore item asked to "confirm scope/structure of a `docs/content-design.md`." That file **already exists** — design pillars, the progression spine, power/XP & pacing curves, proficiency-path rollout, the class roster, and per-zone templates with a `[STRAWMAN | DRAFT | LOCKED]` status convention — and is already referenced by `CLAUDE.md`. So the macro-strategy home exists; no child issue. The division of labor is settled: **macro arc → `content-design.md`; per-piece rationale → `DesignerNotes`.**

### 8. Visualizer: a stretch, deferred.

Feasible (the Progression admin tool already has a map/graph view to borrow): render the content graph with references as edges and overlay the lint warnings. Low priority, its own issue, depends on the reachability model from decision 5.

## Implementation issues

Ordering: the **export (1)** defines the on-disk format the others consume, so it lands first; **seed (2)** and **lint (3)** build on it; **DesignerNotes (4)** is independent; the **visualizer (5)** is a stretch that follows the lint.

- [#1418](https://github.com/ginderjeremiah/GameServer/issues/1418) — Content export → canonical source-controlled JSON + CI drift guard (decisions 2, 4).
- [#1419](https://github.com/ginderjeremiah/GameServer/issues/1419) — Seed a fresh DB (dev/test/CI/recovery) from the content export; retire `e2e-seed.sql` (decision 3). Depends on #1418.
- [#1420](https://github.com/ginderjeremiah/GameServer/issues/1420) — Progression-graph lint: cross-entity reachability checks as a CI test + optional admin Content Health view (decision 5).
- [#1421](https://github.com/ginderjeremiah/GameServer/issues/1421) — Add authoring-only `DesignerNotes` to the 9 static reference entities (decision 6).
- [#1422](https://github.com/ginderjeremiah/GameServer/issues/1422) — (stretch) Content-graph visualizer in the admin Workbench (decision 8). Depends on #1420.
