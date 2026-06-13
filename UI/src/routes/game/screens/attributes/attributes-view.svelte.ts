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
   deprecated DropBonus and the not-yet-implemented crit/dodge/block attributes
   are intentionally omitted and will appear automatically once they gain real
   formulas.

   The backend (`PlayerStatPoints.TryUpdateAttributes`) allows points to be moved
   freely — any attribute can be decremented back to 0, refunding its points to
   the pool — so there is no respec cost or locked baseline here. */

import { apiSocket, EAttribute, type IAttributeUpdate, type IBattlerAttribute } from '$lib/api';
import { BattleAttributes } from '$lib/battle';
import { attributeName } from '$lib/common';
import { playerManager } from '$lib/engine';
import { staticData, toastError } from '$stores';

export type AttributeMode = 'guided' | 'theory';

const MODE_STORAGE_KEY = 'ttf.attr.mode';

const sum = (arr: number[]): number => arr.reduce((a, b) => a + b, 0);
const round2 = (n: number): number => Math.round(n * 100) / 100;

/** The six core attributes that accept stat-point allocation (EAttribute 0..5),
 *  in display order. */
export const CORE_ATTRIBUTES: EAttribute[] = [
	EAttribute.Strength,
	EAttribute.Endurance,
	EAttribute.Intellect,
	EAttribute.Agility,
	EAttribute.Dexterity,
	EAttribute.Luck
];

/** Display grouping for the derived-stats panel. Rendered in this order; groups
 *  with no surfaced stats are skipped. */
export type DerivedGroup = 'Survivability' | 'Offense' | 'Utility';
export const DERIVED_GROUPS: DerivedGroup[] = ['Survivability', 'Offense', 'Utility'];

export interface DerivedStatDef {
	id: EAttribute;
	group: DerivedGroup;
	/** Optional unit suffix appended after the value (e.g. `%`). */
	unit: string;
}

/** Derived stats the game actually computes today (see `BattleAttributes`). Kept
 *  deliberately minimal — adding a real formula there plus an entry here is all
 *  it takes to surface a new stat, and the panel scales by scrolling. */
export const DERIVED_STATS: DerivedStatDef[] = [
	{ id: EAttribute.MaxHealth, group: 'Survivability', unit: '' },
	{ id: EAttribute.Defense, group: 'Survivability', unit: '' },
	{ id: EAttribute.CooldownRecovery, group: 'Utility', unit: '' }
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
 *  attribute at `coreIndex`. Auto-derived by diffing the real formulas, so it
 *  stays correct if those formulas change. */
export function perPointYields(coreIndex: number, coreValues: number[]): PerPointYield[] {
	const bumped = [...coreValues];
	bumped[coreIndex] = (bumped[coreIndex] ?? 0) + 1;
	const before = deriveStats(coreValues);
	const after = deriveStats(bumped);
	const yields: PerPointYield[] = [];
	for (const def of DERIVED_STATS) {
		const delta = round2(after[def.id] - before[def.id]);
		if (delta !== 0) {
			yields.push({ id: def.id, delta });
		}
	}
	return yields;
}

/** The derived stats a core attribute feeds — intrinsic to the formulas, so it
 *  is computed once from a neutral baseline. */
const CORE_FEEDS: EAttribute[][] = CORE_ATTRIBUTES.map((_, i) =>
	perPointYields(
		i,
		CORE_ATTRIBUTES.map(() => 1)
	).map((y) => y.id)
);

export function feedsFor(coreIndex: number): EAttribute[] {
	return CORE_FEEDS[coreIndex] ?? [];
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
	[EAttribute.Defense]: 'Def',
	[EAttribute.CooldownRecovery]: 'CDR'
};

export function derivedShortLabel(id: EAttribute): string {
	return DERIVED_SHORT[id] ?? attributeName(id, staticData.attributes);
}

/** The unit suffix configured for a surfaced derived stat (empty if none). */
export function derivedUnit(id: EAttribute): string {
	return DERIVED_STATS.find((d) => d.id === id)?.unit ?? '';
}

/* ── reactive view-model ──────────────────────────────────────────────────
   Owns the working allocation (`draft`) and the persisted baseline
   (`committed`), derives the dirty set and the live/preview derived stats, and
   persists only the per-attribute deltas (mirroring the backend's update
   contract). */
export class AttributesView {
	/** Active density mode; persisted to local storage per player. */
	mode = $state<AttributeMode>('guided');
	/** Working allocation per core attribute (index matches CORE_ATTRIBUTES). */
	draft = $state<number[]>([]);
	/** Last-saved allocation; the dirty set is `draft` minus this. */
	committed = $state<number[]>([]);
	/** Unspent pool (statPointsGained − statPointsUsed) at the saved baseline. */
	savedAvailable = $state(0);
	/** Brief "Attributes saved" confirmation flash. */
	saved = $state(false);
	/** True while a save request is in flight (guards against double-submit). */
	saving = $state(false);
	/** While a radar drag is active the scale is pinned to the value captured at
	 *  gesture start (see {@link lockScale}); null when unpinned. */
	#lockedHexMax = $state<number | null>(null);

	#flashTimer: ReturnType<typeof setTimeout> | undefined;

	constructor() {
		this.mode = readStoredMode();
		this.syncFromPlayer();
	}

	/** (Re)seed the baseline and working copy from the player manager. */
	syncFromPlayer(): void {
		const committed = CORE_ATTRIBUTES.map((id) => readAllocation(id));
		this.committed = committed;
		this.draft = [...committed];
		this.savedAvailable = playerManager.statPointsGained - playerManager.statPointsUsed;
	}

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

	isDirtyIndex(i: number): boolean {
		return this.draft[i] !== this.committed[i];
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
		const next = [...this.draft];
		next[i] += add;
		this.draft = next;
		this.saved = false;
	}

	dec(i: number, by = 1): void {
		const sub = Math.min(by, this.draft[i]);
		if (sub <= 0) {
			return;
		}
		const next = [...this.draft];
		next[i] -= sub;
		this.draft = next;
		this.saved = false;
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
		this.draft = [...this.committed];
		this.saved = false;
	}

	/** The minimal set of per-attribute deltas to persist — only the changed
	 *  attributes. Computed from plain state so it is safe to read from
	 *  non-reactive call sites like {@link save}. */
	get changedUpdates(): IAttributeUpdate[] {
		const updates: IAttributeUpdate[] = [];
		for (let i = 0; i < CORE_ATTRIBUTES.length; i++) {
			const delta = this.draft[i] - this.committed[i];
			if (delta !== 0) {
				updates.push({ attributeId: CORE_ATTRIBUTES[i], amount: delta });
			}
		}
		return updates;
	}

	async save(): Promise<void> {
		const updates = this.changedUpdates;
		if (updates.length === 0 || this.saving) {
			return;
		}

		const netSpent = sum(this.draft) - sum(this.committed);
		this.saving = true;
		let result: IBattlerAttribute[] | undefined;
		try {
			const response = await apiSocket.sendSocketCommand('UpdatePlayerStats', updates);
			if (!response.error) {
				result = response.data;
			}
		} catch {
			result = undefined;
		} finally {
			this.saving = false;
		}

		if (!result) {
			toastError('Your attribute changes could not be saved. Please try again.');
			return;
		}

		// Persist to the player manager so battles and other screens use the new
		// allocation, then re-seed the baseline to clear the dirty state.
		playerManager.attributes = result;
		playerManager.statPointsUsed += netSpent;
		this.syncFromPlayer();
		this.flashSaved();
	}

	private flashSaved(): void {
		this.saved = true;
		if (this.#flashTimer) {
			clearTimeout(this.#flashTimer);
		}
		this.#flashTimer = setTimeout(() => {
			this.saved = false;
		}, 1900);
	}

	dispose(): void {
		if (this.#flashTimer) {
			clearTimeout(this.#flashTimer);
		}
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

function readStoredMode(): AttributeMode {
	try {
		return localStorage.getItem(MODE_STORAGE_KEY) === 'theory' ? 'theory' : 'guided';
	} catch {
		// Storage can be unavailable (private mode / SSR); fall back to the default.
		return 'guided';
	}
}

function storeMode(mode: AttributeMode): void {
	try {
		localStorage.setItem(MODE_STORAGE_KEY, mode);
	} catch {
		// Persisting the preference is best-effort; ignore storage failures.
	}
}
