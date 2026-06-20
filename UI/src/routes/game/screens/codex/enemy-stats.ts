/* Enemy attribute values at a given level, for the Codex enemy dossier. Generalises the
   Skills screen's `enemyDefense` (skills-view.svelte.ts): each attribute distribution entry
   contributes `baseAmount + amountPerLevel * level` additively, then the derived secondary
   stats (Max Health, Defense) are resolved through the same `BattleAttributes` composition the
   battle uses — so the displayed numbers match combat by construction.

   An enemy's `attributeDistribution` carries only the six core (primary) attributes; Max Health /
   Defense are never stored, they are derived here. The per-level slope of a derived stat is the
   linear delta `f(level + 1) − f(level)` (the additive→derived composition is linear in level).
   Memoised per enemy+level so dragging the dossier's level slider doesn't rebuild a full
   `BattleAttributes` per read. */

import { EAttribute, type IEnemy } from '$lib/api';
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

/** Memoised result by enemy identity → level (mirrors `enemyDefenseCache`): a record replaced on a
 *  reference-data reload is recomputed (and the stale entry GC'd) rather than served a wrong value. */
const cache = new WeakMap<IEnemy, Map<number, EnemyAttributes>>();

/** Build the enemy's `BattleAttributes` at a level (additive distribution + the derived pass). */
function buildAt(enemy: IEnemy, level: number): BattleAttributes {
	const modifiers = enemy.attributeDistribution.map((dist) => ({
		attributeId: dist.attributeId,
		amount: dist.baseAmount + dist.amountPerLevel * level
	}));
	return new BattleAttributes(modifiers, true);
}

/** The enemy's primary + derived-secondary attribute values at a level (memoised per enemy+level). */
export function enemyAttributesAtLevel(enemy: IEnemy, level: number): EnemyAttributes {
	let byLevel = cache.get(enemy);
	if (byLevel === undefined) {
		byLevel = new Map<number, EnemyAttributes>();
		cache.set(enemy, byLevel);
	}
	const cached = byLevel.get(level);
	if (cached !== undefined) {
		return cached;
	}
	const atLevel = buildAt(enemy, level);
	const atNext = buildAt(enemy, level + 1);
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
