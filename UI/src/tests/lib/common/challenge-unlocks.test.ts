import { describe, it, expect } from 'vitest';
import {
	EItemCategory,
	EItemModType,
	ERarity,
	type IChallenge,
	type IItem,
	type IItemMod,
	type ISkill,
	type IZone
} from '$lib/api';
import { challengeCompletedMessage, resolveUnlockReward, zonesUnlockedBy, type UnlockReward } from '$lib/common';

const zone = (id: number, order: number, name: string, unlockChallengeId?: number, retiredAt?: string): IZone =>
	({
		id,
		name,
		description: '',
		order,
		levelMin: 1,
		levelMax: 10,
		bossLevel: 1,
		unlockChallengeId,
		retiredAt
	}) as IZone;

const challenge = (over: Partial<IChallenge> = {}): IChallenge =>
	({ id: 1, name: 'C', description: '', challengeTypeId: 0, entityType: 0, progressGoal: 10, ...over }) as IChallenge;

describe('zonesUnlockedBy', () => {
	it('returns the zones gated on the challenge, in authored order', () => {
		const zones = [zone(30, 3, 'Gamma', 7), zone(10, 1, 'Alpha', 7), zone(20, 2, 'Beta')];
		expect(zonesUnlockedBy(7, zones).map((z) => z.name)).toEqual(['Alpha', 'Gamma']);
	});

	it('returns an empty list when no zone is gated on the challenge', () => {
		const zones = [zone(10, 1, 'Alpha', 7), zone(20, 2, 'Beta')];
		expect(zonesUnlockedBy(99, zones)).toEqual([]);
	});

	it('excludes retired zones (out of circulation, not advertised as a reward)', () => {
		const zones = [zone(10, 1, 'Alpha', 7), zone(20, 2, 'Beta', 7, '2026-01-01T00:00:00Z')];
		expect(zonesUnlockedBy(7, zones).map((z) => z.name)).toEqual(['Alpha']);
	});

	it('tolerates holes in the sparse (id-indexed) zone array', () => {
		const zones: (IZone | undefined)[] = [];
		zones[10] = zone(10, 1, 'Alpha', 7);
		expect(zonesUnlockedBy(7, zones).map((z) => z.name)).toEqual(['Alpha']);
	});
});

describe('resolveUnlockReward', () => {
	const items: (IItem | undefined)[] = [];
	items[3] = { id: 3, name: 'Iron Helm', itemCategoryId: EItemCategory.Helm, rarityId: ERarity.Rare } as IItem;
	const itemMods: (IItemMod | undefined)[] = [];
	itemMods[4] = { id: 4, name: 'of Fury', itemModTypeId: EItemModType.Suffix, rarityId: ERarity.Epic } as IItemMod;
	const skills: (ISkill | undefined)[] = [];
	skills[5] = { id: 5, name: 'Cleave' } as ISkill;
	const refs = { items, itemMods, skills };

	it('resolves an item reward with rarity tier/accent, "rarity · category" sub-label and the source record', () => {
		const reward = resolveUnlockReward(challenge({ rewardItemId: 3 }), refs);
		expect(reward).toMatchObject({ kind: 'item', name: 'Iron Helm', sub: 'Rare · Helm', rarity: ERarity.Rare });
		expect(reward?.accent).toContain('--rarity-');
		// The matched reference record is carried so richer surfaces build their preview without re-looking-up.
		expect(reward?.kind === 'item' && reward.item).toBe(items[3]);
	});

	it('resolves a mod reward with rarity tier/accent, a "rarity · modtype" sub-label and the source record', () => {
		const reward = resolveUnlockReward(challenge({ rewardItemModId: 4 }), refs);
		expect(reward).toMatchObject({ kind: 'mod', name: 'of Fury', sub: 'Epic · Suffix', rarity: ERarity.Epic });
		expect(reward?.kind === 'mod' && reward.mod).toBe(itemMods[4]);
	});

	it('resolves a skill reward (Common tier) with a neutral accent, "Skill" sub-label and the source record', () => {
		const reward = resolveUnlockReward(challenge({ rewardSkillId: 5 }), refs);
		expect(reward).toMatchObject({ kind: 'skill', name: 'Cleave', sub: 'Skill', rarity: ERarity.Common });
		expect(reward?.kind === 'skill' && reward.skill).toBe(skills[5]);
	});

	it('returns null when the challenge grants no reward', () => {
		expect(resolveUnlockReward(challenge(), refs)).toBeNull();
	});

	it('returns null when the referenced reward record is missing/unloaded', () => {
		expect(resolveUnlockReward(challenge({ rewardItemId: 999 }), refs)).toBeNull();
	});

	it('prefers an item over a mod/skill when more than one reward field is set', () => {
		const reward = resolveUnlockReward(challenge({ rewardItemId: 3, rewardItemModId: 4, rewardSkillId: 5 }), refs);
		expect(reward?.kind).toBe('item');
	});
});

describe('challengeCompletedMessage', () => {
	it('names the challenge and the reward it unlocked', () => {
		const reward = { name: 'Iron Helm' } as UnlockReward;
		expect(challengeCompletedMessage('First Blood', reward)).toBe(
			'Challenge complete: First Blood — unlocked Iron Helm'
		);
	});

	it('announces the completion alone when the challenge grants no reward', () => {
		expect(challengeCompletedMessage('First Blood', null)).toBe('Challenge complete: First Blood');
	});
});
