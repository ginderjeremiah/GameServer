import type { IChallenge, IItem, IItemMod, ISkill, IZone } from '$lib/api';
import { itemCategoryName, modTypeLabel } from './item-display';
import { rarityColor, rarityLabel } from './rarity';

/* What completing a challenge grants. A challenge can unlock zones (those gated on it via
   `unlockChallengeId`) and award a single reward (an item, a mod, or a skill). These pure helpers
   resolve that "what does this unlock" view from the reference data so it can be surfaced wherever
   a challenge appears — the locked-zone tooltip, the challenges screen, etc. — without each call
   site re-deriving the relationship. */

export type UnlockRewardKind = 'item' | 'mod' | 'skill';

export interface UnlockReward {
	kind: UnlockRewardKind;
	/** The reward's real name. Callers mask it themselves (e.g. `???`) while the challenge is sealed. */
	name: string;
	/** Themeable accent: the rarity hue for items/mods, a neutral skill accent for skills. */
	accent: string;
	/** Teaser sub-label, e.g. `Rare · Helm`, `Epic · Prefix`, or `Skill`. */
	sub: string;
}

/** The zero-based-id reference pools the reward resolver reads (any may be undefined before load). */
export interface RewardRefs {
	items?: (IItem | undefined)[];
	itemMods?: (IItemMod | undefined)[];
	skills?: (ISkill | undefined)[];
}

/**
 * Zones whose unlock gate is this challenge — i.e. completing it unlocks them. Returned in authored
 * order, with retired zones excluded (a retired zone is out of circulation, so it is not advertised
 * as a reward). The gate is the reverse of the zone→challenge relationship (`zone.unlockChallengeId`).
 */
export function zonesUnlockedBy(challengeId: number, zones: (IZone | undefined)[]): IZone[] {
	return zones
		.filter((z): z is IZone => z != null && z.unlockChallengeId === challengeId && z.retiredAt == null)
		.sort((a, b) => a.order - b.order);
}

/**
 * Resolve a challenge's single reward (item, mod, or skill) from the reference pools. Item > mod >
 * skill precedence mirrors the challenges-screen reward resolution. Returns null when the challenge
 * grants no reward (or the referenced record is missing/unloaded).
 */
export function resolveUnlockReward(challenge: IChallenge, refs: RewardRefs): UnlockReward | null {
	if (challenge.rewardItemId != null) {
		const item = refs.items?.[challenge.rewardItemId];
		if (item) {
			return {
				kind: 'item',
				name: item.name,
				accent: rarityColor(item.rarityId),
				sub: `${rarityLabel(item.rarityId)} · ${itemCategoryName(item.itemCategoryId)}`
			};
		}
	}
	if (challenge.rewardItemModId != null) {
		const mod = refs.itemMods?.[challenge.rewardItemModId];
		if (mod) {
			return {
				kind: 'mod',
				name: mod.name,
				accent: rarityColor(mod.rarityId),
				sub: `${rarityLabel(mod.rarityId)} · ${modTypeLabel(mod.itemModTypeId)}`
			};
		}
	}
	if (challenge.rewardSkillId != null) {
		const skill = refs.skills?.[challenge.rewardSkillId];
		if (skill) {
			return {
				kind: 'skill',
				name: skill.name,
				accent: 'var(--accent-light)',
				sub: 'Skill'
			};
		}
	}
	return null;
}
