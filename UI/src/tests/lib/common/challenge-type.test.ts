import { describe, it, expect } from 'vitest';
import { EChallengeType, type IChallengeType } from '$lib/api';
import { challengeTypeColor, challengeTypeName, clampChallengeProgress } from '$lib/common';

describe('challengeTypeColor', () => {
	it('maps each type to its themeable --challenge-* accent variable', () => {
		expect(challengeTypeColor(EChallengeType.EnemiesKilled)).toBe('var(--challenge-enemies-killed)');
		expect(challengeTypeColor(EChallengeType.TimeTrial)).toBe('var(--challenge-time-trial)');
	});
});

describe('challengeTypeName', () => {
	const types = [{ id: EChallengeType.EnemiesKilled, name: 'Slayer' }] as IChallengeType[];

	it('prefers the authored name from the reference set', () => {
		expect(challengeTypeName(EChallengeType.EnemiesKilled, types)).toBe('Slayer');
	});

	it('falls back to the normalized enum name when the type is absent or no set is given', () => {
		expect(challengeTypeName(EChallengeType.TimeTrial, types)).toBe('Time Trial');
		expect(challengeTypeName(EChallengeType.BossesDefeated)).toBe('Bosses Defeated');
	});
});

describe('clampChallengeProgress', () => {
	it('passes through progress at or under the goal', () => {
		expect(clampChallengeProgress(0, 10)).toBe(0);
		expect(clampChallengeProgress(7, 10)).toBe(7);
		expect(clampChallengeProgress(10, 10)).toBe(10);
	});

	it('clamps progress that overshoots the goal', () => {
		expect(clampChallengeProgress(12, 10)).toBe(10);
	});
});
