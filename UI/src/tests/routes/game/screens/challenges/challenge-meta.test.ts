import { describe, it, expect } from 'vitest';
import { EChallengeType } from '$lib/api';
import {
	CHALLENGE_TYPE_META,
	challengeTypeUnit,
	challengeTypeBlurb,
	formatCount
} from '$routes/game/screens/challenges/challenge-meta';

describe('CHALLENGE_TYPE_META', () => {
	it('declares a unit and blurb for every challenge type', () => {
		const typeIds = Object.values(EChallengeType).filter((v): v is EChallengeType => typeof v === 'number');
		for (const id of typeIds) {
			expect(CHALLENGE_TYPE_META[id]).toBeDefined();
			expect(CHALLENGE_TYPE_META[id].unit).toBeTruthy();
			expect(CHALLENGE_TYPE_META[id].blurb).toBeTruthy();
		}
	});
});

describe('challengeTypeUnit', () => {
	it("returns the type's progress noun", () => {
		expect(challengeTypeUnit(EChallengeType.EnemiesKilled)).toBe('kills');
		expect(challengeTypeUnit(EChallengeType.BossesDefeated)).toBe('bosses');
		expect(challengeTypeUnit(EChallengeType.SkillsUsed)).toBe('casts');
	});

	it('falls back to an empty string for an unknown type', () => {
		expect(challengeTypeUnit(999 as EChallengeType)).toBe('');
	});
});

describe('challengeTypeBlurb', () => {
	it("returns the type's hero-banner blurb", () => {
		expect(challengeTypeBlurb(EChallengeType.EnemiesKilled)).toBe('Cut down foes in the field.');
		expect(challengeTypeBlurb(EChallengeType.LevelReached)).toBe('Grow in power.');
	});

	it('falls back to an empty string for an unknown type', () => {
		expect(challengeTypeBlurb(999 as EChallengeType)).toBe('');
	});
});

describe('formatCount', () => {
	it('renders small counts plainly', () => {
		expect(formatCount(0)).toBe('0');
		expect(formatCount(999)).toBe('999');
	});

	it('thousands-separates large counts', () => {
		expect(formatCount(1000)).toBe('1,000');
		expect(formatCount(1234567)).toBe('1,234,567');
	});
});
