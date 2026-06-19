import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import {
	EChallengeGoalComparison,
	EChallengeType,
	EEntityType,
	type IChallenge,
	type IPlayerChallenge
} from '$lib/api';

// Challenges fetches the player's progress over the socket (via the playerChallenges store) and
// resolves the challenge catalogue + reward pools from staticData.
const { mockFetchSocket, mockToastError, staticData } = vi.hoisted(() => ({
	mockFetchSocket: vi.fn(),
	mockToastError: vi.fn(),
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));

vi.mock('$lib/api', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$lib/api')>();
	return { ...actual, fetchSocketData: mockFetchSocket };
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
	mockFetchSocket.mockClear();
	mockFetchSocket.mockResolvedValue(PLAYER_CHALLENGES);
});

afterEach(() => cleanup());

describe('Challenges screen', () => {
	it('fetches the player challenges and renders the overview body', async () => {
		render(Challenges);
		expect(screen.getByTestId('challenges-screen')).toBeTruthy();
		// The type rail's "Overview" entry only renders in the normal body.
		expect(await screen.findByText('Overview')).toBeTruthy();
		expect(screen.queryByTestId('challenges-error')).toBeNull();
		expect(mockFetchSocket).toHaveBeenCalledWith('GetPlayerChallenges');
		expect(mockToastError).not.toHaveBeenCalled();
	});

	it('renders normally (no error) for a player with no recorded progress', async () => {
		mockFetchSocket.mockResolvedValue([]);
		render(Challenges);
		// A genuine empty result is the normal "no progress yet" view, not an error.
		expect(await screen.findByText('Overview')).toBeTruthy();
		expect(screen.queryByTestId('challenges-error')).toBeNull();
		expect(mockToastError).not.toHaveBeenCalled();
	});

	it('surfaces an error (not the zero-progress view) when the fetch fails', async () => {
		mockFetchSocket.mockRejectedValue(new Error('network down'));
		render(Challenges);
		expect(await screen.findByTestId('challenges-error')).toBeTruthy();
		// A failed load must not masquerade as a player with zero progress.
		expect(screen.queryByText('Overview')).toBeNull();
		expect(mockToastError).toHaveBeenCalledTimes(1);
	});

	describe('rail/detail interaction', () => {
		it('pivots from the overview into a type detail when a type card is picked', async () => {
			const { container } = render(Challenges);
			await screen.findByText('Overview');

			// Activate the overview's "Enemies Killed" type card (its full-bleed overlay button) →
			// view.select(type).
			const typeCard = container.querySelector('.type-card .overlay-button') as HTMLElement;
			await fireEvent.click(typeCard);

			// The detail grid now shows the type's challenge cards + the sort control.
			expect(await screen.findByText('First Blood')).toBeTruthy();
			expect(screen.getByText('Sort')).toBeTruthy();
			expect(screen.queryByText('Overview')).not.toBeNull(); // rail still present
		});

		it('selects a type from the rail', async () => {
			const { container } = render(Challenges);
			await screen.findByText('Overview');

			const railButtons = container.querySelectorAll('.rail button');
			// [0] is the Overview entry; [1] is the first type's rail button.
			await fireEvent.click(railButtons[1]);

			expect(await screen.findByText('First Blood')).toBeTruthy();
		});

		it('switches the active sort via the SortControl', async () => {
			const { container } = render(Challenges);
			await screen.findByText('Overview');
			await fireEvent.click(container.querySelector('.type-card .overlay-button') as HTMLElement);
			await screen.findByText('First Blood');

			const nameSort = screen.getByText('Name');
			await fireEvent.click(nameSort);

			expect(nameSort.closest('button')?.classList.contains('active')).toBe(true);
		});
	});
});
