/* Card-Boss Duel — pure mechanics.

   Everything here is deterministic and framework-free: the tunables, the
   attribute→stat formulas, the half-open coverage rule, the no-overlap queue
   placement, and the strike-damage calculation. The stateful engine
   (`loom-game.ts`) and the Svelte UI build on top of these, and the unit tests
   exercise them directly. Keep this file free of DOM, Svelte, and randomness
   so the rules stay verifiable in isolation. */

import type { CardKind } from './cards';

/* ── tunables ─────────────────────────────────────────────────────────────
   Gathered here as the demo's balance knobs; these become the
   admin-configurable values once the minigame is backed by real data. */

/** Pixels of board width per logical tick (the render scale). */
export const PX_PER_TICK = 24;
/** NOW line position as a fraction of board width from the left. */
export const NOW_FRACTION = 0.3;
/** Ticks advanced per second at full speed (continuous tempo). */
export const BASE_RATE = 4.2;
/** Fraction of speed while Reflex slow-time is held. */
export const REFLEX_SCALE = 0.34;
/** A guarded strike deals this fraction of its damage. */
export const GUARD_DAMAGE_MULT = 0.4;
/** A strike resolving on a crit tick is multiplied by this. */
export const CRIT_MULT = 2;
/** Ticks past the NOW line after which an entity is culled (off-screen left). */
export const CULL_TICKS = 24;
/** How far ahead of NOW (in ticks) entities are scheduled into the future. */
export const SCHEDULE_HORIZON = 34;

/* ── attribute → stat formulas ──────────────────────────────────────────────
   The loom's "feel" lives in the tempo attributes everyone has, so a build
   that specialised in STR or INT (but not both) stays fully playable. */

/** Hand size cap grows slowly with Dexterity: 4 + ⌊log₁₀(DEX)⌋. */
export function handCap(dex: number): number {
	return 4 + Math.floor(Math.log10(Math.max(1, dex)));
}

/** Ticks between crit marks, tightening with Luck: max(6, 26 − ⌊LUCK⌋). */
export function critGap(luck: number): number {
	return Math.max(6, 26 - Math.floor(luck));
}

/** Seconds between draws, faster with Agility: max(1.0, 2.0 − 0.005·AGI). */
export function drawInterval(agi: number): number {
	return Math.max(1.0, 2.0 - 0.005 * agi);
}

/* ── coverage & placement ───────────────────────────────────────────────── */

export interface Span {
	start: number;
	end: number;
}

/** Half-open coverage: a span [start, end) covers `tick` iff start ≤ tick < end.
 *  This makes an impact land *centered* in a cell (never straddling an edge) and
 *  lets adjacent queued spans tile seamlessly with no double-covered seam. */
export function coversTick(span: Span, tick: number): boolean {
	return tick >= span.start && tick < span.end;
}

/** True if any span in the lane covers `tick`. */
export function spanActiveAt(lane: readonly Span[], tick: number): boolean {
	return lane.some((s) => coversTick(s, tick));
}

/** Earliest start ≥ `desired` (and never in the past) at which a span of length
 *  `dur` fits without overlapping any existing span in the lane. Same-type cards
 *  therefore queue tail-to-tail rather than stacking. */
export function freeStart(lane: readonly Span[], desired: number, dur: number, playTick: number): number {
	let start = Math.max(desired, Math.ceil(playTick));
	const sorted = lane.slice().sort((a, b) => a.start - b.start);
	let changed = true;
	let guard = 0;
	while (changed && guard++ < 200) {
		changed = false;
		for (const s of sorted) {
			if (start < s.end && s.start < start + dur) {
				start = s.end;
				changed = true;
			}
		}
	}
	return start;
}

/* ── strike resolution ──────────────────────────────────────────────────── */

export interface StrikeOutcome {
	dmg: number;
	crit: boolean;
	guarded: boolean;
}

/** Final damage of a player strike resolving at `resolveTick`, applying a crit
 *  (×2) when an unused crit mark sits on that tick and the boss's guard
 *  reduction (×0.4) when a guard span covers it. Crit applies before guard. */
export function resolveStrike(
	baseDmg: number,
	resolveTick: number,
	crits: readonly { tick: number; used: boolean }[],
	enemyGuards: readonly Span[]
): StrikeOutcome {
	let dmg = baseDmg;
	const crit = crits.some((c) => !c.used && Math.round(c.tick) === Math.round(resolveTick));
	if (crit) {
		dmg *= CRIT_MULT;
	}
	const guarded = spanActiveAt(enemyGuards, resolveTick);
	if (guarded) {
		dmg = Math.floor(dmg * GUARD_DAMAGE_MULT);
	}
	return { dmg, crit, guarded };
}

/* ── misc ───────────────────────────────────────────────────────────────── */

/** Fisher–Yates shuffle in place, using an injected RNG for determinism. */
export function shuffle<T>(arr: T[], rng: () => number): T[] {
	for (let i = arr.length - 1; i > 0; i--) {
		const j = Math.floor(rng() * (i + 1));
		[arr[i], arr[j]] = [arr[j], arr[i]];
	}
	return arr;
}

/** Whether a card kind shares the strike/attack lane (vs. the block lane). */
export function isAttackKind(kind: CardKind): boolean {
	return kind === 'attack' || kind === 'channel';
}
