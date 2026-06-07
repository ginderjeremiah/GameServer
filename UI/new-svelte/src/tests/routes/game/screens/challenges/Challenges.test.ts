import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import {
	EChallengeGoalComparison,
	EChallengeType,
	EEntityType,
	type IChallenge,
	type IPlayerChallenge
} from '$lib/api';

// Challenges fetches the player's progress via ApiRequest and resolves the
// challenge catalogue + reward pools from staticData.
const { mockGet, mockToastError, staticData } = vi.hoisted(() => ({
	mockGet: vi.fn(),
	mockToastError: vi.fn(),
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));

vi.mock('$lib/api', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$lib/api')>();
	return { ...actual, ApiRequest: { get: mockGet } };
});
// Override staticData + toastError; keep the other real stores (the screen also
// uses registerTooltipComponent, and $components pulls in the engine/log store).
vi.mock('$stores', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$stores')>();
	return { ...actual, staticData, toastError: mockToastError };
});

import Challenges from '$routes/game/screens/challenges/Challenges.svelte';

const challenge = (over: Partial<IChallenge> & Pick<IChallenge, 'id' | 'name' | 'challengeTypeId'>): IChallenge => ({
	description: `${over.name} description`,
	entityType: EEntityType.None,
	progressGoal: 10,
	...over
});

const PLAYER_CHALLENGES: IPlayerChallenge[] = [{ challengeId: 1, progress: 5, completed: false }];

beforeEach(() => {
	staticData.items = [];
	staticData.itemMods = [];
	staticData.enemies = [];
	staticData.zones = [];
	staticData.skills = [];
	staticData.challengeTypes = [
		{ id: EChallengeType.EnemiesKilled, goalComparison: EChallengeGoalComparison.AtLeast, name: 'Enemies Killed' }
	];
	staticData.challenges = [
		challenge({ id: 1, name: 'First Blood', challengeTypeId: EChallengeType.EnemiesKilled, progressGoal: 10 })
	];
	mockToastError.mockClear();
	mockGet.mockClear();
	mockGet.mockResolvedValue(PLAYER_CHALLENGES);
});

afterEach(() => cleanup());

describe('Challenges screen', () => {
	it('fetches the player challenges and renders the overview body', async () => {
		render(Challenges);
		expect(screen.getByTestId('challenges-screen')).toBeTruthy();
		// The type rail's "Overview" entry only renders in the normal body.
		expect(await screen.findByText('Overview')).toBeTruthy();
		expect(screen.queryByTestId('challenges-error')).toBeNull();
		expect(mockGet).toHaveBeenCalledWith('Challenges/Player');
		expect(mockToastError).not.toHaveBeenCalled();
	});

	it('renders normally (no error) for a player with no recorded progress', async () => {
		mockGet.mockResolvedValue([]);
		render(Challenges);
		// A genuine empty result is the normal "no progress yet" view, not an error.
		expect(await screen.findByText('Overview')).toBeTruthy();
		expect(screen.queryByTestId('challenges-error')).toBeNull();
		expect(mockToastError).not.toHaveBeenCalled();
	});

	it('surfaces an error (not the zero-progress view) when the fetch fails', async () => {
		mockGet.mockRejectedValue(new Error('network down'));
		render(Challenges);
		expect(await screen.findByTestId('challenges-error')).toBeTruthy();
		// A failed load must not masquerade as a player with zero progress.
		expect(screen.queryByText('Overview')).toBeNull();
		expect(mockToastError).toHaveBeenCalledTimes(1);
	});
});
