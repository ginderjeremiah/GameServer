import { describe, it, expect } from 'vitest';
import { ERarity, type IChallenge, type IChallengeCompletedModel, type IItem, type ISkill } from '$lib/api';
import { formatAwayDuration, resolveCompletedChallenges } from '$routes/game/welcome-back/offline-summary';

describe('formatAwayDuration', () => {
	it('shows whole minutes under an hour', () => {
		expect(formatAwayDuration(5 * 60_000)).toBe('5m');
		expect(formatAwayDuration(45 * 60_000)).toBe('45m');
	});

	it('shows hours and minutes between one hour and a day', () => {
		expect(formatAwayDuration(60 * 60_000)).toBe('1h 0m');
		expect(formatAwayDuration((3 * 60 + 12) * 60_000)).toBe('3h 12m');
		expect(formatAwayDuration(10 * 60 * 60_000)).toBe('10h 0m');
	});

	it('shows days and hours past a day', () => {
		expect(formatAwayDuration(24 * 60 * 60_000)).toBe('1d 0h');
		expect(formatAwayDuration((2 * 24 * 60 + 4 * 60) * 60_000)).toBe('2d 4h');
	});

	it('floors sub-minute and negative windows to 0m', () => {
		expect(formatAwayDuration(30_000)).toBe('0m');
		expect(formatAwayDuration(-1000)).toBe('0m');
	});
});

describe('resolveCompletedChallenges', () => {
	const item: IItem = { id: 2, name: 'Aegis', rarityId: ERarity.Rare, itemCategoryId: 0 } as unknown as IItem;
	const skill: ISkill = { id: 4, name: 'Firebolt' } as ISkill;
	const challenges: (IChallenge | undefined)[] = [
		{ id: 0, name: 'First Blood', rewardItemId: 2 } as IChallenge,
		{ id: 1, name: 'Untouchable', rewardSkillId: 4 } as IChallenge,
		{ id: 2, name: 'Persistence' } as IChallenge // no direct reward
	];
	const refs = {
		challenges,
		items: [undefined, undefined, item],
		itemMods: [],
		skills: [undefined, undefined, undefined, undefined, skill]
	};

	it('resolves each completed challenge to its name and unlocked reward', () => {
		const completed: IChallengeCompletedModel[] = [
			{ challengeId: 0, rewardItemId: 2 },
			{ challengeId: 1, rewardSkillId: 4 }
		];

		const result = resolveCompletedChallenges(completed, refs);

		expect(result[0].name).toBe('First Blood');
		expect(result[0].reward?.kind).toBe('item');
		expect(result[1].name).toBe('Untouchable');
		expect(result[1].reward?.kind).toBe('skill');
		expect(result[1].reward?.name).toBe('Firebolt');
	});

	it('resolves a no-reward challenge to a null reward', () => {
		const result = resolveCompletedChallenges([{ challengeId: 2 }], refs);
		expect(result[0].name).toBe('Persistence');
		expect(result[0].reward).toBeNull();
	});

	it('falls back to a safe name when the challenge is missing from the catalogue', () => {
		const result = resolveCompletedChallenges([{ challengeId: 99 }], refs);
		expect(result[0].name).toBe('Unknown challenge');
		expect(result[0].reward).toBeNull();
	});
});
