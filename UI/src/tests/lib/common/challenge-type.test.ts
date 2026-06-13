import { describe, it, expect } from 'vitest';
import { EChallengeType, type IChallengeType } from '$lib/api';
import { challengeTypeColor, challengeTypeName } from '$lib/common';

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
