import { ERarity, type IChallenge, type IItem, type IItemMod, type ISkill, type IZone } from '$lib/api';
import { itemCategoryName, modTypeLabel } from './item-display';
import { rarityColor, rarityLabel } from './rarity';

/* What completing a challenge grants. A challenge can unlock zones (those gated on it via
   `unlockChallengeId`) and award a single reward (an item, a mod, or a skill). These pure helpers
   resolve that "what does this unlock" view from the reference data so it can be surfaced wherever
   a challenge appears — the locked-zone tooltip, the challenges screen, etc. — without each call
   site re-deriving the relationship. This is the single home for the item > mod > skill precedence
   and the rarity/accent/sub-label of a reward; richer surfaces (the challenges screen's
   `resolveReward`) build their preview on top of this resolution rather than re-deriving it. */

interface UnlockRewardBase {
	/** The reward's real name. Callers mask it themselves (e.g. `???`) while the challenge is sealed. */
	name: string;
	/** Themeable accent: the rarity hue for items/mods, a neutral skill accent for skills. */
	accent: string;
	/** Teaser sub-label, e.g. `Rare · Helm`, `Epic · Prefix`, or `Skill`. */
	sub: string;
	/** Rarity tier (drives the rarity sort/glow on richer surfaces). Skills carry none, so they resolve to `Common`. */
	rarity: ERarity;
}

export interface ItemUnlockReward extends UnlockRewardBase {
	kind: 'item';
	/** The rewarded item's reference record; the screen builds its preview item from this. */
	item: IItem;
}

export interface ModUnlockReward extends UnlockRewardBase {
	kind: 'mod';
	/** The rewarded mod's reference record, passed through to the mod tooltip. */
	mod: IItemMod;
}

export interface SkillUnlockReward extends UnlockRewardBase {
	kind: 'skill';
	/** The rewarded skill's reference record, passed through to the skill tooltip. */
	skill: ISkill;
}

export type UnlockReward = ItemUnlockReward | ModUnlockReward | SkillUnlockReward;

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
				item,
				name: item.name,
				accent: rarityColor(item.rarityId),
				sub: `${rarityLabel(item.rarityId)} · ${itemCategoryName(item.itemCategoryId)}`,
				rarity: item.rarityId
			};
		}
	}
	if (challenge.rewardItemModId != null) {
		const mod = refs.itemMods?.[challenge.rewardItemModId];
		if (mod) {
			return {
				kind: 'mod',
				mod,
				name: mod.name,
				accent: rarityColor(mod.rarityId),
				sub: `${rarityLabel(mod.rarityId)} · ${modTypeLabel(mod.itemModTypeId)}`,
				rarity: mod.rarityId
			};
		}
	}
	if (challenge.rewardSkillId != null) {
		const skill = refs.skills?.[challenge.rewardSkillId];
		if (skill) {
			return {
				kind: 'skill',
				skill,
				name: skill.name,
				accent: 'var(--accent-light)',
				sub: 'Skill',
				// Skills carry no rarity tier; resolve to the lowest so they sort with Common.
				rarity: ERarity.Common
			};
		}
	}
	return null;
}

/**
 * The player-facing message announcing a completed challenge and what it unlocked, used by the
 * completion success-toast. The reward (item / mod / skill) is named when present so the player sees
 * their gain at a glance; a challenge carrying no direct reward just announces the completion.
 */
export function challengeCompletedMessage(challengeName: string, reward: UnlockReward | null): string {
	if (reward) {
		return `Challenge complete: ${challengeName} — unlocked ${reward.name}`;
	}
	return `Challenge complete: ${challengeName}`;
}
