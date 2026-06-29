/* Proficiencies screen — "The Lexicon": the word-detail inspector's pure logic core.

   Derives the inspector pane's content from a selected tier's view-model (proficiencies-lexicon.ts) and
   the path it belongs to — the state pill, the XP progress text, the per-level breakdown ladder, and the
   "Trained by" chips. Framework-free and store-free (reference-data lookups are passed in as a name
   resolver + the attributes set, mirroring the param-based `$lib/common` helpers) so the whole derivation
   is unit-testable without rendering; `WordDetail.svelte` only wires these to the live stores. */

import { EActivityKey, EModifierType, type IAttribute, type IProficiencyLevelModifier } from '$lib/api';
import { attributeName, formatAttributeDelta, formatNum } from '$lib/common';
import type { TierState, TierView } from './proficiencies-lexicon';

/** A skill-id → display-name resolver (the inspector passes one reading `staticData.skills`). Returns
 *  `undefined` for an id with no live skill, so unresolved entries can be dropped rather than shown blank. */
export type SkillNameResolver = (skillId: number) => string | undefined;

/** The state pill shown beside the inspector header: a label plus a theme key (`mastered`/`training`/
 *  `learning`) the component maps to a `--gold`/`--accent`/neutral accent. */
export interface StatePill {
	label: string;
	key: 'mastered' | 'training' | 'learning';
}

/** One row of the per-level breakdown ladder (levels 1..maxLevel). */
export interface LadderRow {
	level: number;
	/** Whether the player has reached this level (drives the reached/dim styling). */
	reached: boolean;
	/** A milestone level — one that grants a reward skill (matches the spine pip-track diamonds). */
	isMilestone: boolean;
	/** The formatted per-level attribute payout(s) authored at this level, joined; empty when none. */
	bonus: string;
	/** The resolved name of the skill this milestone grants, when the level is a reward milestone. */
	skillName?: string;
}

/** The state pill for a tier's state. `maxed` → MASTERED, `training` → TRAINING, otherwise LEARNING (an
 *  unlocked tier not currently fed by the loadout). */
export function statePill(state: TierState): StatePill {
	switch (state) {
		case 'maxed':
			return { label: 'MASTERED', key: 'mastered' };
		case 'training':
			return { label: 'TRAINING', key: 'training' };
		default:
			return { label: 'LEARNING', key: 'learning' };
	}
}

/** The XP progress summary line. A maxed tier banks no XP, so it reads as fully translated; otherwise it
 *  shows the residual XP toward the next level's threshold. */
export function xpProgressText(tier: TierView): string {
	if (tier.level >= tier.maxLevel) {
		return 'maxed out — fully translated';
	}
	return `${formatNum(tier.xp)} / ${formatNum(tier.xpForNext)} XP → level ${tier.level + 1}`;
}

/** The decipher reveal line for a tier, gated by its stage: the translation once translated, the quoted
 *  pronunciation once it is learned, otherwise the undeciphered placeholder. Shared by the spine card and
 *  the inspector so the two surfaces always agree on the reveal string. */
export function decipherReveal(tier: TierView): string {
	if (tier.decipher === 'translated') {
		return tier.translation;
	}
	if (tier.decipher === 'pronunciation') {
		return `“${tier.pronunciation}”`;
	}
	return '⟨ undeciphered ⟩';
}

/** Formats a single proficiency level modifier as a magnitude + attribute name, e.g. `+2% Fire Damage`
 *  or `×1.5 Critical Damage`. Reuses the attribute-tooltip formatters: a multiplicative factor renders
 *  as `×factor`; an additive amount goes through `formatAttributeDelta`, which is signed and honours the
 *  attribute's percentage scale. */
export function formatModifier(modifier: IProficiencyLevelModifier, attributes?: IAttribute[]): string {
	const magnitude =
		modifier.modifierTypeId === EModifierType.Multiplicative
			? `×${formatNum(modifier.amount)}`
			: formatAttributeDelta(modifier.amount, modifier.attributeId, attributes);
	return `${magnitude} ${attributeName(modifier.attributeId, attributes)}`;
}

/**
 * Builds the per-level breakdown ladder (rows for levels 1..maxLevel) for a tier. Each row carries the
 * per-level attribute payout authored at that exact level (the increment, joined when more than one is
 * authored) and — for a reward milestone — the resolved skill name it grants. `isMilestone` matches the
 * spine's pip-track diamonds (a level with an authored reward skill). Levels with no authored payout are
 * still rendered (an empty `bonus`) so the ladder is the full 1..maxLevel track.
 */
export function buildLadder(
	tier: TierView,
	attributes: IAttribute[] | undefined,
	resolveSkill: SkillNameResolver
): LadderRow[] {
	const modifiersByLevel = new Map<number, IProficiencyLevelModifier[]>();
	for (const modifier of tier.levelModifiers) {
		const existing = modifiersByLevel.get(modifier.level);
		if (existing) {
			existing.push(modifier);
		} else {
			modifiersByLevel.set(modifier.level, [modifier]);
		}
	}

	const rewardByLevel = new Map<number, number>();
	for (const reward of tier.levelRewards) {
		rewardByLevel.set(reward.level, reward.rewardSkillId);
	}

	const rows: LadderRow[] = [];
	for (let level = 1; level <= tier.maxLevel; level++) {
		const modifiers = modifiersByLevel.get(level) ?? [];
		const rewardSkillId = rewardByLevel.get(level);
		rows.push({
			level,
			reached: tier.level >= level,
			isMilestone: rewardSkillId !== undefined,
			bonus: modifiers.map((modifier) => formatModifier(modifier, attributes)).join(' · '),
			skillName: rewardSkillId !== undefined ? resolveSkill(rewardSkillId) : undefined
		});
	}
	return rows;
}

/** The activity a path trains on, as the inspector's single "Trained by" chip label — a path earns from
 *  any effect whose key resolves to it (spike #1318). */
export function trainedBy(activityKey: EActivityKey): string[] {
	// TODO(#1341): surface a friendly label (e.g. Dot → "Damage over time") rather than the raw enum name,
	// as part of the authoring/labels pass.
	return [EActivityKey[activityKey]];
}
