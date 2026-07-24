/* Attribute build screen — a point-buy on the six core attributes with a live
   preview of the derived stats they feed.

   Two in-page modes share one allocation model, so the toggle reads as a single
   page changing density rather than two screens:
     · Guided     — the hexagon radar is the hero; attribute steppers carry
                    plain-language "feeds" tags and no derived-stats panel, so it
                    cannot get cluttered as more derived stats are added.
     · Theorycraft — an allocation table with per-point marginal yields, a
                    compact radar, and a scrollable grouped derived-stats panel.

   IMPORTANT: the derived stats and their formulas are NOT redefined here — they
   are read from the real combat model (`BattleAttributes`), the same one the
   battle simulation uses, so the page always reflects what the game actually
   computes. Only the derived stats the game produces today are surfaced; the
   deprecated DropBonus, the authored-only DamageReflection (no core-attribute
   yield, so it has nothing to preview here), and the crit/dodge set are
   intentionally omitted and appear automatically once listed here with a real
   per-point formula.

   The backend (`PlayerStatPoints.TryUpdateAttributes`) allows points to be moved
   freely — any attribute can be decremented back to 0, refunding its points to
   the pool — so there is no respec cost or locked baseline here. */

import {
	apiSocket,
	EAttribute,
	ESkillEffectTarget,
	type IAttributeUpdate,
	type IBattlerAttribute,
	type ISkill
} from '$lib/api';
import { BattleAttributes, isSkillDormant } from '$lib/battle';
import { attributeName, CORE_ATTRIBUTES, SaveFlash } from '$lib/common';
import { safeLocalStorage } from '$lib/common/local-storage';
import { inventoryManager, playerManager } from '$lib/engine';
import { staticData, toastError } from '$stores';

export type AttributeMode = 'guided' | 'theory';

/** Per-player prefix (see `readStoredMode`/`storeMode`) — each character keeps its own density mode. */
const MODE_STORAGE_KEY_PREFIX = 'gameserver.attrMode.';

const sum = (arr: number[]): number => arr.reduce((a, b) => a + b, 0);
/** Rounds a per-point yield to clean floating-point noise while preserving the small increments a
 *  base-1 multiplier attribute contributes (e.g. an Agility point's +0.002 to a cadence/evasion multiplier),
 *  which a coarser 2-decimal round would collapse to zero and drop from the surfaced yields. */
const roundYield = (n: number): number => Math.round(n * 1e6) / 1e6;

/** Re-exported for this screen's own consumers (`Attributes.svelte`, `TheoryRow.svelte`, etc.), which
 *  import it from here rather than reaching into `$lib/common` directly — the allocation domain
 *  (EAttribute 0..5, in display order) now lives in `$lib/common/attribute-display` since the admin
 *  Workbench needs it too (a class's stat distribution is core-attribute-only), but this screen is
 *  still where its "allocation domain" meaning is documented. */
export { CORE_ATTRIBUTES };

/** Display grouping for the derived-stats panel. Rendered in this order; groups
 *  with no surfaced stats are skipped. */
export type DerivedGroup = 'Survivability' | 'Offense' | 'Utility';
export const DERIVED_GROUPS: DerivedGroup[] = ['Survivability', 'Offense', 'Utility'];

export interface DerivedStatDef {
	id: EAttribute;
	group: DerivedGroup;
}

/** Derived stats the game actually computes today (see `BattleAttributes`). Kept
 *  deliberately minimal — adding a real formula there plus an entry here is all
 *  it takes to surface a new stat, and the panel scales by scrolling. Value formatting
 *  (including the `%` for percentage attributes) comes from the shared `formatAttributeValue`. */
export const DERIVED_STATS: DerivedStatDef[] = [
	{ id: EAttribute.MaxHealth, group: 'Survivability' },
	{ id: EAttribute.Toughness, group: 'Survivability' },
	{ id: EAttribute.CooldownRecovery, group: 'Utility' }
];

/** Marginal yield of a single point in a core attribute on one derived stat. */
export interface PerPointYield {
	id: EAttribute;
	delta: number;
}

/* ── derived-stat computation (delegated to the real combat model) ────────── */

/** Computes every surfaced derived stat for a core-allocation vector using the
 *  same `BattleAttributes` the battle simulation uses. */
export function deriveStats(coreValues: number[]): Record<number, number> {
	const attrs: IBattlerAttribute[] = CORE_ATTRIBUTES.map((id, i) => ({ attributeId: id, amount: coreValues[i] ?? 0 }));
	const battle = new BattleAttributes(attrs, true);
	const out: Record<number, number> = {};
	for (const def of DERIVED_STATS) {
		out[def.id] = battle.getValue(def.id);
	}
	return out;
}

/** The marginal change to each surfaced derived stat from +1 in the core
 *  attribute at `coreIndex`, computed by diffing the real formulas against a
 *  neutral baseline. The surfaced derived stats are linear in the core
 *  attributes (purely additive modifiers — see `STATIC_ATTRIBUTE_MODIFIERS`), so
 *  this marginal delta is constant per index and independent of the current
 *  allocation; it is auto-derived so it stays correct if those formulas change. */
function computePerPointYields(coreIndex: number): PerPointYield[] {
	const baseline = CORE_ATTRIBUTES.map(() => 0);
	const bumped = [...baseline];
	bumped[coreIndex] = 1;
	const before = deriveStats(baseline);
	const after = deriveStats(bumped);
	const yields: PerPointYield[] = [];
	for (const def of DERIVED_STATS) {
		const delta = roundYield(after[def.id] - before[def.id]);
		if (delta !== 0) {
			yields.push({ id: def.id, delta });
		}
	}
	return yields;
}

/** Per-point yields memoised by core index, computed once from the (constant)
 *  marginal formulas rather than re-derived per allocation change. */
const PER_POINT_YIELDS: PerPointYield[][] = CORE_ATTRIBUTES.map((_, i) => computePerPointYields(i));

/** The constant marginal yields for the core attribute at `coreIndex`. */
export function perPointYields(coreIndex: number): PerPointYield[] {
	return PER_POINT_YIELDS[coreIndex] ?? [];
}

export function feedsFor(coreIndex: number): EAttribute[] {
	return perPointYields(coreIndex).map((y) => y.id);
}

/** Inverse of {@link feedsFor}: the core attributes that feed each derived stat,
 *  precomputed once from the constant per-point yields so the breakdown's "fed by"
 *  line is an O(1) lookup rather than a per-render scan over every core attribute. */
const CONTRIBUTORS_BY_DERIVED: Partial<Record<EAttribute, EAttribute[]>> = (() => {
	const map: Partial<Record<EAttribute, EAttribute[]>> = {};
	CORE_ATTRIBUTES.forEach((core, i) => {
		for (const derivedId of feedsFor(i)) {
			(map[derivedId] ??= []).push(core);
		}
	});
	return map;
})();

/** The core attributes that feed a derived stat (inverse of {@link feedsFor}). */
export function contributorsFor(derivedId: EAttribute): EAttribute[] {
	return CONTRIBUTORS_BY_DERIVED[derivedId] ?? [];
}

/* ── amplifier inert-signal (spike #1426 Decision 5) ──────────────────────
   AGI and LUK are pure amplifiers: their `*Multiplier` targets scale a `0`-based enabler
   (`DodgeChance`/`CooldownBonus`/`ParryChance`/a skill's own authored `criticalChance`), so a build with no
   matching enabler fielded gets nothing from the points invested there — dormant, not dead. This section
   answers whether that's the case for the *current* loadout, purely as a read-only signal (no allocation
   gating; free-pool points stay a legitimate pre-commitment). */

/** Hint copy for an inert amplifier, keyed by the attribute it describes. Absent for every attribute
 *  that always contributes (see {@link isAttributeInert}). */
const INERT_HINT: Partial<Record<EAttribute, string>> = {
	[EAttribute.Agility]: 'Dormant — no dodge or cadence enabler fielded',
	[EAttribute.Luck]: 'Dormant — no crit or parry enabler fielded'
};

/** The hint text for an inert core attribute, or `undefined` if it isn't a candidate for the signal at all. */
export function inertHint(id: EAttribute): string | undefined {
	return INERT_HINT[id];
}

/** Whether any fielded source — an equipped item/mod's static stat line, or a fielded skill's own
 *  self-targeted effect — authors a nonzero base amount of `attributeId`. Only the *authored* amount is
 *  checked (an effect's own base `amount`, not its live in-combat scaled value), matching the "committed
 *  enabler" framing the crit/dodge/parry/cadence template already uses. This is exactly the source set
 *  `StaticAttributeModifiers`' "granted by items/item-mods/skill-effects" convention documents for
 *  DodgeChance/CooldownBonus/ParryChance — a future source authoring one of them outside that convention
 *  (e.g. a class/proficiency payout) would need a matching scan added here. */
function hasFieldedEnabler(
	attributeId: EAttribute,
	equipmentStats: readonly IBattlerAttribute[],
	fieldedSkills: readonly ISkill[]
): boolean {
	if (equipmentStats.some((a) => a.attributeId === attributeId && a.amount > 0)) {
		return true;
	}
	return fieldedSkills.some((skill) =>
		skill.effects.some((e) => e.target === ESkillEffectTarget.Self && e.attributeId === attributeId && e.amount > 0)
	);
}

/** Whether the core attribute at `id` is inert for the current loadout: AGI only amplifies a fielded
 *  `DodgeChance`/`CooldownBonus` enabler, LUK only a fielded skill's own authored `criticalChance` or a
 *  fielded `ParryChance` enabler (spike #1426 Decision 5). Every other core attribute always contributes,
 *  so this is unconditionally `false` for them. */
export function isAttributeInert(
	id: EAttribute,
	equipmentStats: readonly IBattlerAttribute[],
	fieldedSkills: readonly ISkill[]
): boolean {
	if (id === EAttribute.Luck) {
		return (
			!fieldedSkills.some((s) => s.criticalChance > 0) &&
			!hasFieldedEnabler(EAttribute.ParryChance, equipmentStats, fieldedSkills)
		);
	}
	if (id === EAttribute.Agility) {
		return (
			!hasFieldedEnabler(EAttribute.DodgeChance, equipmentStats, fieldedSkills) &&
			!hasFieldedEnabler(EAttribute.CooldownBonus, equipmentStats, fieldedSkills)
		);
	}
	return false;
}

/** The player's live fielded skill set — the selected loadout plus equipped-gear grants, de-duplicated
 *  and passed through the weapon-match gate ({@link isSkillDormant}) — mirroring `Battler.fillSkills`'s
 *  assembly (without allocating battle `Skill` instances) so the amplifier check never disagrees with
 *  what the battler actually fields. */
function fieldedPlayerSkills(): ISkill[] {
	const catalogue = staticData.skills ?? [];
	const weaponType = inventoryManager.equippedWeaponType;
	const ids = new Set<number>([...playerManager.selectedSkills, ...inventoryManager.grantedSkillIds]);
	const result: ISkill[] = [];
	for (const id of ids) {
		const skill = catalogue[id];
		if (skill && !isSkillDormant(skill, weaponType)) {
			result.push(skill);
		}
	}
	return result;
}

/** Maps a pointer position (in the radar's SVG user space) to the attribute
 *  value its axis vertex would represent if dragged there: projects the pointer
 *  onto the axis direction (so sideways drift is ignored and only the radial
 *  component counts) and scales by the radar geometry. The result is unclamped —
 *  {@link AttributesView.setValue} applies the budget/zero limits. */
export function radarValueAtPointer(
	px: number,
	py: number,
	centre: number,
	axisAngleRad: number,
	axisLength: number,
	hexMax: number
): number {
	if (axisLength <= 0) {
		return 0;
	}
	const projection = (px - centre) * Math.cos(axisAngleRad) + (py - centre) * Math.sin(axisAngleRad);
	return (projection / axisLength) * hexMax;
}

/** Compact labels for the dense theorycraft "per point" line. Falls back to the
 *  full name so a newly surfaced derived stat still reads correctly. */
const DERIVED_SHORT: Partial<Record<EAttribute, string>> = {
	[EAttribute.MaxHealth]: 'HP',
	[EAttribute.Toughness]: 'Tgh',
	[EAttribute.CooldownRecovery]: 'CDR'
};

export function derivedShortLabel(id: EAttribute): string {
	return DERIVED_SHORT[id] ?? attributeName(id, staticData.attributes);
}

/* ── reactive view-model ──────────────────────────────────────────────────
   Owns the persisted baseline (`committed`) and the working allocation
   (`draft`), derives the dirty set and the live/preview derived stats, and
   persists only the per-attribute deltas (mirroring the backend's update
   contract).

   `committed` is read live off the player manager rather than snapshotted once
   at construction: a mid-session resync (`playerManager.initialize()`, #1809)
   reassigns `attributes` wholesale, and the baseline must track it or the
   screen computes `remaining`/deltas against a server state that no longer
   exists. `draft` layers unsaved per-attribute deltas (`#pendingDeltas`) over
   `committed`, but only while `#pendingDeltasAttrs` — the manager `attributes`
   reference those deltas were computed against — still matches the live one;
   {@link effectiveDeltas} treats them as zero the moment it doesn't. A
   not-yet-saved edit can't be trusted to still apply cleanly against a
   baseline it was never computed against, and an **external** resync (not this
   view's own `save`, which keeps the marker in lockstep) is by definition the
   fresher truth — so it silently drops the edit rather than layering it onto
   the new baseline. `save` instead consumes exactly the deltas it sent,
   preserving any edit made while that request was in flight (#1506). */
export class AttributesView {
	/** Active density mode; persisted to local storage per player. */
	mode = $state<AttributeMode>('guided');
	/** Unsaved per-attribute deltas (index matches CORE_ATTRIBUTES), valid only against
	 *  {@link #pendingDeltasAttrs} — see {@link effectiveDeltas}. */
	#pendingDeltas = $state<number[]>(CORE_ATTRIBUTES.map(() => 0));
	/** The `playerManager.attributes` reference {@link #pendingDeltas} was computed against; `null`
	 *  never matches, so deltas read as zero until the first edit stamps it. */
	#pendingDeltasAttrs = $state<IBattlerAttribute[] | null>(null);
	/** Brief "Attributes saved" confirmation flash. */
	#saveFlash = new SaveFlash();
	/** True while a save request is in flight (guards against double-submit). */
	saving = $state(false);
	/** While a radar drag is active the scale is pinned to the value captured at
	 *  gesture start (see {@link lockScale}); null when unpinned. */
	#lockedHexMax = $state<number | null>(null);

	constructor() {
		this.mode = readStoredMode();
	}

	/** Brief "Attributes saved" confirmation flash. */
	get saved(): boolean {
		return this.#saveFlash.active;
	}

	/** Unspent pool (statPointsGained − statPointsUsed), derived live from the reactive
	 *  player manager so points granted by a background level-up appear without a remount. */
	readonly savedAvailable = $derived(playerManager.statPointsGained - playerManager.statPointsUsed);

	/** Last-saved allocation per core attribute, read live off the player manager (see the class doc). */
	readonly committed = $derived(CORE_ATTRIBUTES.map((id) => readAllocation(id)));

	/** {@link #pendingDeltas} if they're still valid against the live manager state, otherwise all
	 *  zero — a pure read (no mutation), so it's safe to call from a `$derived` (see the class doc). */
	private effectiveDeltas(): number[] {
		return this.#pendingDeltasAttrs === playerManager.attributes ? this.#pendingDeltas : CORE_ATTRIBUTES.map(() => 0);
	}

	/** Working allocation per core attribute: {@link committed} plus the {@link effectiveDeltas}. */
	readonly draft = $derived.by(() => {
		const deltas = this.effectiveDeltas();
		return this.committed.map((v, i) => v + deltas[i]);
	});

	/** Current (pending) value per core attribute. */
	readonly values = $derived(this.draft);
	/** Saved value per core attribute. */
	readonly savedValues = $derived(this.committed);
	/** Net points spent versus the saved baseline (negative while refunding). */
	readonly pendingSpent = $derived(sum(this.draft) - sum(this.committed));
	/** Unspent points after the pending changes. */
	readonly remaining = $derived(this.savedAvailable - this.pendingSpent);
	/** Denominator for the budget meter — the pool available at the baseline. */
	readonly budget = $derived(this.savedAvailable);
	readonly dirty = $derived(this.draft.some((v, i) => v !== this.committed[i]));
	/** Number of attributes whose allocation differs from the baseline. */
	readonly changedCount = $derived(this.draft.filter((v, i) => v !== this.committed[i]).length);

	/** Live derived stats for the pending build and the saved build. */
	readonly derived = $derived(deriveStats(this.draft));
	readonly savedDerived = $derived(deriveStats(this.committed));

	/** A radar scale that keeps the build shape readable as values grow, pinned to
	 *  a fixed value for the duration of a drag so allocating points mid-gesture
	 *  can't rescale the radar (see {@link lockScale}). */
	readonly hexMax = $derived(this.#lockedHexMax ?? computeHexMax(this.draft, this.committed));

	/** The player's live fielded skill set (selected loadout + equipped-gear grants, weapon-gated),
	 *  recomputed when the loadout or equipped gear changes — see {@link isInert}. */
	readonly fieldedSkills = $derived.by<ISkill[]>(() => fieldedPlayerSkills());

	/** Whether the core attribute at `i` is inert under the current loadout — see {@link isAttributeInert}. */
	isInert(i: number): boolean {
		return isAttributeInert(CORE_ATTRIBUTES[i], inventoryManager.equipmentStats, this.fieldedSkills);
	}

	/** Whether any attribute can still be incremented (points remain). */
	canInc(): boolean {
		return this.remaining > 0;
	}

	/** Whether the attribute at `i` can be decremented (its value is above 0). */
	canDec(i: number): boolean {
		return this.draft[i] > 0;
	}

	inc(i: number, by = 1): void {
		const add = Math.min(by, this.remaining);
		if (add <= 0) {
			return;
		}
		const next = [...this.effectiveDeltas()];
		next[i] += add;
		this.#pendingDeltas = next;
		this.#pendingDeltasAttrs = playerManager.attributes;
		this.#saveFlash.reset();
	}

	dec(i: number, by = 1): void {
		const sub = Math.min(by, this.draft[i]);
		if (sub <= 0) {
			return;
		}
		const next = [...this.effectiveDeltas()];
		next[i] -= sub;
		this.#pendingDeltas = next;
		this.#pendingDeltasAttrs = playerManager.attributes;
		this.#saveFlash.reset();
	}

	/** Set the attribute at `i` directly to `target`, clamped to the available
	 *  budget (when increasing) and to 0 (when decreasing). Backs the radar drag:
	 *  the pointer position maps to an absolute target, which this reconciles into
	 *  the same bounded inc/dec the steppers use. */
	setValue(i: number, target: number): void {
		const clamped = Math.max(0, Math.round(target));
		const delta = clamped - this.draft[i];
		if (delta > 0) {
			this.inc(i, delta);
		} else if (delta < 0) {
			this.dec(i, -delta);
		}
	}

	/** Pin the radar scale to its current value for the duration of a drag, so
	 *  points allocated mid-gesture can't rescale the radar and shift the
	 *  pointer→value mapping out from under the pointer (#433). */
	lockScale(): void {
		this.#lockedHexMax = computeHexMax(this.draft, this.committed);
	}

	/** Release the pinned scale so it tracks the allocation again. */
	unlockScale(): void {
		this.#lockedHexMax = null;
	}

	setMode(mode: AttributeMode): void {
		this.mode = mode;
		storeMode(mode);
	}

	discard(): void {
		this.#pendingDeltas = CORE_ATTRIBUTES.map(() => 0);
		this.#pendingDeltasAttrs = playerManager.attributes;
		this.#saveFlash.reset();
	}

	/** The minimal set of per-attribute deltas to persist — only the changed
	 *  attributes. Computed from plain state so it is safe to read from
	 *  non-reactive call sites like {@link save}. */
	get changedUpdates(): IAttributeUpdate[] {
		const updates: IAttributeUpdate[] = [];
		const deltas = this.effectiveDeltas();
		for (let i = 0; i < CORE_ATTRIBUTES.length; i++) {
			if (deltas[i] !== 0) {
				updates.push({ attributeId: CORE_ATTRIBUTES[i], amount: deltas[i] });
			}
		}
		return updates;
	}

	async save(): Promise<void> {
		const updates = this.changedUpdates;
		if (updates.length === 0 || this.saving) {
			return;
		}

		// The exact deltas being sent, so they can be subtracted back out below regardless of
		// what happens to the rest of the pending set while the request is in flight.
		const sentDeltas = [...this.effectiveDeltas()];

		this.saving = true;
		const response = await apiSocket.sendSocketCommand('UpdatePlayerStats', updates);
		this.saving = false;

		// Snapshot the current effective deltas (any mid-flight edit included) before the
		// reassignment below moves the resync marker out from under them. Reference equality
		// with `#pendingDeltas` tells whether they were still valid at this exact moment —
		// `effectiveDeltas()` falls back to a *new* zero array (never `#pendingDeltas` itself)
		// the instant an external resync lands mid-flight and invalidates them; in that case
		// they're already gone and must not be "consumed" a second time below (which would
		// otherwise subtract `sentDeltas` from zero and manufacture a phantom negative delta).
		const currentDeltas = this.effectiveDeltas();
		const deltasStillValid = currentDeltas === this.#pendingDeltas;

		// Any returned payload is the server's authoritative post-command state (the
		// rejection path returns it unchanged), so adopt the allocations and the spent
		// total absolutely rather than re-deriving the spend locally (#1548). `committed`
		// picks this up live (see the class doc); this view's own reassignment isn't an
		// external resync, so re-pin the (still-unconsumed, on an error) deltas against it.
		if (response.data) {
			playerManager.attributes = response.data.attributes;
			playerManager.statPointsUsed = response.data.statPointsUsed;
			playerManager.playerRating = response.data.playerRating;
			this.#pendingDeltas = currentDeltas;
			this.#pendingDeltasAttrs = playerManager.attributes;
		}

		if (response.error || !response.data) {
			toastError('Your attribute changes could not be saved. Please try again.');
			return;
		}

		// Consume exactly the deltas that were sent, preserving any edit made while the save was
		// in flight instead of wiping it (#1506) — unless an external resync already discarded
		// the pending set mid-flight, in which case there's nothing left to consume.
		if (deltasStillValid) {
			this.#pendingDeltas = this.#pendingDeltas.map((d, i) => d - sentDeltas[i]);
		}
		this.#saveFlash.flash();
	}

	dispose(): void {
		this.#saveFlash.dispose();
	}
}

/* ── helpers ──────────────────────────────────────────────────────────────── */

/** Reads a core attribute's saved amount from the player manager (0 if absent). */
function readAllocation(id: EAttribute): number {
	return playerManager.attributes.find((a) => a.attributeId === id)?.amount ?? 0;
}

/** Rounds the radar scale up to a tidy multiple of 5 with ~20% headroom so the
 *  build shape never clips and never fills the whole hexagon at a glance. */
function computeHexMax(draft: number[], committed: number[]): number {
	const peak = Math.max(1, ...draft, ...committed);
	return Math.max(10, Math.ceil((peak * 1.2) / 5) * 5);
}

/** Keyed by the live player id so switching characters doesn't inherit — or silently overwrite —
 *  another character's density mode (docs/frontend-screens.md: "persisted per player"). */
function modeStorageKey(): string {
	return `${MODE_STORAGE_KEY_PREFIX}${playerManager.id}`;
}

function readStoredMode(): AttributeMode {
	// Storage may be null (private mode / SSR); fall back to the default.
	return safeLocalStorage()?.getItem(modeStorageKey()) === 'theory' ? 'theory' : 'guided';
}

function storeMode(mode: AttributeMode): void {
	try {
		safeLocalStorage()?.setItem(modeStorageKey(), mode);
	} catch {
		// Persisting the preference is best-effort; ignore storage failures.
	}
}
