/* The single source for an enemy's battle build at a level. An enemy's `attributeDistribution`
   carries only the six core (primary) attributes; each entry contributes `baseAmount +
   amountPerLevel * level` additively, then the derived secondary stats (Max Health, Defense) are
   resolved through the same `BattleAttributes` composition the battle uses — so every display
   surface (Codex dossier, Skills Compare-vs) matches combat by construction. Mirrors the backend
   enemy build (`Enemy.GetAttributeModifiers`).

   The `BattleAttributes` build is memoised per enemy+level so a slider drag or a Compare-vs
   recompute doesn't rebuild a full set per read. Keying the outer cache on the enemy object means a
   record replaced when reference data reloads is recomputed (and the stale entry GC'd) rather than
   served a wrong value. */

import { EAttribute, type IEnemy } from '$lib/api';
// Imported from the specific module (not the `$lib/battle` barrel) to avoid a barrel import cycle:
// `battle-attributes` reads attribute names from `$lib/common`.
import { BattleAttributes } from '$lib/battle/battle-attributes';

export interface EnemyAttributeValue {
	attributeId: EAttribute;
	/** Base amount at level 0 (the distribution's `baseAmount`; 0 for a derived stat). */
	base: number;
	/** Increase per level (the distribution's `amountPerLevel`, or a derived stat's linear slope). */
	perLevel: number;
	/** Resolved (rounded) value at the requested level. */
	value: number;
}

export interface EnemyAttributes {
	/** The six core attributes straight from the distribution. */
	primary: EnemyAttributeValue[];
	/** Derived stats not stored on the enemy (Max Health, Defense), computed via the battle pass. */
	secondary: EnemyAttributeValue[];
}

/** The derived secondary stats surfaced in the dossier, in display order. */
const DERIVED_SECONDARY: EAttribute[] = [EAttribute.MaxHealth, EAttribute.Defense];

/** Memoised `BattleAttributes` by enemy identity → level — the single enemy battle build. */
const buildCache = new WeakMap<IEnemy, Map<number, BattleAttributes>>();

/** Memoised `EnemyAttributes` (primary + derived) by enemy identity → level. */
const attributesCache = new WeakMap<IEnemy, Map<number, EnemyAttributes>>();

/** The enemy's resolved `BattleAttributes` at a level (additive distribution + the derived pass),
 *  memoised per enemy+level. The one build every enemy-at-level display surface consumes. */
export function enemyBattleAttributes(enemy: IEnemy, level: number): BattleAttributes {
	let byLevel = buildCache.get(enemy);
	if (byLevel === undefined) {
		byLevel = new Map<number, BattleAttributes>();
		buildCache.set(enemy, byLevel);
	}
	const cached = byLevel.get(level);
	if (cached !== undefined) {
		return cached;
	}
	const modifiers = enemy.attributeDistribution.map((dist) => ({
		attributeId: dist.attributeId,
		amount: dist.baseAmount + dist.amountPerLevel * level
	}));
	const built = new BattleAttributes(modifiers, true);
	byLevel.set(level, built);
	return built;
}

/** An enemy's flat Defense at a level — the value the battle subtracts in `Battler.takeDamage`. */
export function enemyDefense(enemy: IEnemy, level: number): number {
	return enemyBattleAttributes(enemy, level).getValue(EAttribute.Defense);
}

/** The enemy's primary + derived-secondary attribute values at a level (memoised per enemy+level).
 *  The derived stats' per-level slope is the linear delta `f(level + 1) − f(level)` (the
 *  additive→derived composition is linear in level). */
export function enemyAttributesAtLevel(enemy: IEnemy, level: number): EnemyAttributes {
	let byLevel = attributesCache.get(enemy);
	if (byLevel === undefined) {
		byLevel = new Map<number, EnemyAttributes>();
		attributesCache.set(enemy, byLevel);
	}
	const cached = byLevel.get(level);
	if (cached !== undefined) {
		return cached;
	}
	const atLevel = enemyBattleAttributes(enemy, level);
	const atNext = enemyBattleAttributes(enemy, level + 1);
	const primary: EnemyAttributeValue[] = enemy.attributeDistribution.map((dist) => ({
		attributeId: dist.attributeId,
		base: dist.baseAmount,
		perLevel: dist.amountPerLevel,
		value: Math.round(dist.baseAmount + dist.amountPerLevel * level)
	}));
	const secondary: EnemyAttributeValue[] = DERIVED_SECONDARY.map((id) => {
		const value = atLevel.getValue(id);
		return { attributeId: id, base: 0, perLevel: atNext.getValue(id) - value, value: Math.round(value) };
	});
	const result: EnemyAttributes = { primary, secondary };
	byLevel.set(level, result);
	return result;
}
