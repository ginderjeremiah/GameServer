/* Shared before/after-diff derivation for "did this just become true" (content-events, spike #1392 area
   A). Consolidates the pattern already duplicated across challenge-unlocks/synthesis-feedback/
   proficiency-feedback call sites (a snapshot taken before a change, compared against the same
   derivation after) into reusable primitives, so the tutorial mechanic-anchored triggers (#1587) and any
   future consumer (e.g. #1395's badges) have one place to detect "newly true" instead of a fourth
   bespoke diff. Pure, no UI, no persistence — callers own snapshotting and any one-shot/ever-seen state. */

import type { SkillActivation } from '$lib/battle/battle-step';

/** The base flip primitive every mechanic-anchored trigger reduces to: false→true, and only that
 *  direction (already-true stays uninteresting, true→false is not "newly" anything). */
export function newlyTrue(wasTrueBefore: boolean, isTrueNow: boolean): boolean {
	return isTrueNow && !wasTrueBefore;
}

/** Ids present in `after` but absent from `before` — the shared set-diff behind "what newly unlocked"
 *  (skills, items, creatable recipes, or any other zero-based/reference id set). */
export function newlyInSet<T>(before: ReadonlySet<T>, after: ReadonlySet<T>): T[] {
	const added: T[] = [];
	for (const id of after) {
		if (!before.has(id)) {
			added.push(id);
		}
	}
	return added;
}

/** Whether a numeric progress value (e.g. a proficiency level) crossed `threshold` going from `before`
 *  to `after` — the shared "crossed a level/milestone" check. */
export function crossedThreshold(before: number, after: number, threshold: number): boolean {
	return after >= threshold && before < threshold;
}

/** Whether this tick's activations include the player's first-ever landed critical hit (enemies never
 *  crit in this engine, so only player-side activations are relevant). */
export function isFirstCrit(
	hasCritBefore: boolean,
	activations: readonly Pick<SkillActivation, 'byPlayer' | 'crit'>[]
): boolean {
	return newlyTrue(
		hasCritBefore,
		activations.some((a) => a.byPlayer && a.crit)
	);
}

/** Whether this tick's activations include the player's first-ever dodged incoming hit (only enemy-fired
 *  activations carry a dodge roll). */
export function isFirstDodge(
	hasDodgedBefore: boolean,
	activations: readonly Pick<SkillActivation, 'dodged'>[]
): boolean {
	return newlyTrue(
		hasDodgedBefore,
		activations.some((a) => a.dodged)
	);
}

/** Whether this tick's activations include the first time one of the player's own skills has fully
 *  recharged and fired (a skill's cooldown resets to 0 the same tick it reaches its cap and fires, so a
 *  player-fired, non-counter activation IS that recharge). The riposte counter fires off a parry, not a
 *  cooldown, so it's excluded. */
export function isFirstCooldownRecharge(
	hasFiredBefore: boolean,
	activations: readonly Pick<SkillActivation, 'byPlayer' | 'counter'>[]
): boolean {
	return newlyTrue(
		hasFiredBefore,
		activations.some((a) => a.byPlayer && !a.counter)
	);
}
