import { describe, it, expect } from 'vitest';
import { EChallengeGoalComparison, EChallengeType, type IChallengeType } from '$lib/api';
import {
	challengeTypeColor,
	challengeTypeName,
	clampChallengeProgress,
	comparisonFor,
	formatTime,
	progressInfo
} from '$lib/common';

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

describe('comparisonFor', () => {
	const types: IChallengeType[] = [
		{ id: EChallengeType.EnemiesKilled, name: 'Enemies Killed', goalComparison: EChallengeGoalComparison.AtLeast },
		{ id: EChallengeType.TimeTrial, name: 'Time Trial', goalComparison: EChallengeGoalComparison.AtMost }
	];

	it("reads the type's intrinsic goal-comparison direction from reference data", () => {
		expect(comparisonFor(EChallengeType.EnemiesKilled, types)).toBe(EChallengeGoalComparison.AtLeast);
		expect(comparisonFor(EChallengeType.TimeTrial, types)).toBe(EChallengeGoalComparison.AtMost);
	});

	it('defaults to at-least when the type is missing from reference data', () => {
		expect(comparisonFor(EChallengeType.TimeTrial, [])).toBe(EChallengeGoalComparison.AtLeast);
		expect(comparisonFor(EChallengeType.TimeTrial)).toBe(EChallengeGoalComparison.AtLeast);
	});
});

describe('progressInfo', () => {
	it('computes an accumulating (atLeast) fill clamped to 100%', () => {
		expect(progressInfo(50, EChallengeGoalComparison.AtLeast, 25)).toMatchObject({
			atMost: false,
			value: 25,
			goal: 50,
			percent: 50
		});
		expect(progressInfo(50, EChallengeGoalComparison.AtLeast, 80).percent).toBe(100);
	});

	it('clamps an over-goal value to the goal (transient race before the ChallengeCompleted push lands)', () => {
		expect(progressInfo(10, EChallengeGoalComparison.AtLeast, 12)).toMatchObject({ value: 10, goal: 10 });
	});

	it('treats an atMost goal as best-vs-target proximity, with 0 meaning "no data"', () => {
		// no qualifying value yet
		expect(progressInfo(60, EChallengeGoalComparison.AtMost, 0)).toMatchObject({
			atMost: true,
			hasData: false,
			percent: 0
		});
		// best 120s vs 60s target -> 50% of the way there
		expect(progressInfo(60, EChallengeGoalComparison.AtMost, 120)).toMatchObject({
			best: 120,
			target: 60,
			percent: 50,
			hasData: true
		});
		// already beating the target clamps to 100%
		expect(progressInfo(60, EChallengeGoalComparison.AtMost, 52).percent).toBe(100);
	});
});

describe('formatTime', () => {
	it('renders sub-minute durations in seconds', () => {
		expect(formatTime(45)).toBe('45s');
		expect(formatTime(9)).toBe('9s');
	});

	it('renders minute-spanning durations as m:ss with a zero-padded seconds field', () => {
		expect(formatTime(90)).toBe('1:30');
		expect(formatTime(125)).toBe('2:05');
		expect(formatTime(60)).toBe('1:00');
	});

	it('rounds fractional seconds', () => {
		expect(formatTime(45.4)).toBe('45s');
		expect(formatTime(89.6)).toBe('1:30');
	});

	it('renders a dash for zero, negative, or missing input', () => {
		expect(formatTime(0)).toBe('—');
		expect(formatTime(-5)).toBe('—');
		expect(formatTime(NaN)).toBe('—');
	});
});
