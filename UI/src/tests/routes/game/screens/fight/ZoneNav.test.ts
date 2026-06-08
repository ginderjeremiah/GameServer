import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import type { IZone } from '$lib/api';

// ZoneNav reads the zone list from the reference-data store, the active zone from the player
// manager, and challenge completion from the shared store; all are mocked so the test controls
// the navigation + lock state directly. The real `$lib/common` zone-progression helpers are used.
const { mockPlayerManager, staticData, playerChallenges } = vi.hoisted(() => ({
	mockPlayerManager: { currentZone: 0 },
	staticData: { zones: [] as IZone[] },
	playerChallenges: { isChallengeCompleted: vi.fn(() => false) }
}));

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager }));
vi.mock('$stores', () => ({ staticData, playerChallenges }));

import ZoneNav from '$routes/game/screens/fight/ZoneNav.svelte';

const zone = (id: number, order: number, name: string, unlockChallengeId?: number): IZone => ({
	id,
	name,
	description: '',
	order,
	levelMin: 1,
	levelMax: 10,
	bossLevel: 1,
	unlockChallengeId
});

beforeEach(() => {
	playerChallenges.isChallengeCompleted.mockReset();
	playerChallenges.isChallengeCompleted.mockReturnValue(false);
	// Deliberately out of array order to prove the component orders by `order`, not index.
	staticData.zones = [zone(30, 3, 'Frozen Summit'), zone(10, 1, 'Verdant Hollow'), zone(20, 2, 'Ashen Wastes')];
	mockPlayerManager.currentZone = 20;
});

afterEach(cleanup);

describe('ZoneNav', () => {
	it('shows the current zone name and a 1-based, zero-padded number ordered by zone order', () => {
		render(ZoneNav);
		const nav = screen.getByTestId('zone-nav');
		expect(nav.textContent).toContain('Zone · 02');
		expect(nav.textContent).toContain('Ashen Wastes');
	});

	it('disables the left button on the first zone', () => {
		mockPlayerManager.currentZone = 10;
		render(ZoneNav);
		const [left, right] = screen.getAllByRole('button') as HTMLButtonElement[];
		expect(left.disabled).toBe(true);
		expect(right.disabled).toBe(false);
	});

	it('disables the right button on the last zone', () => {
		mockPlayerManager.currentZone = 30;
		render(ZoneNav);
		const [left, right] = screen.getAllByRole('button') as HTMLButtonElement[];
		expect(left.disabled).toBe(false);
		expect(right.disabled).toBe(true);
	});

	it('advances to the next zone (by order) when the right button is clicked', async () => {
		render(ZoneNav);
		const [, right] = screen.getAllByRole('button') as HTMLButtonElement[];
		await fireEvent.click(right);
		// From order 2 (id 20) forward to order 3 (id 30).
		expect(mockPlayerManager.currentZone).toBe(30);
	});

	it('moves to the previous zone (by order) when the left button is clicked', async () => {
		render(ZoneNav);
		const [left] = screen.getAllByRole('button') as HTMLButtonElement[];
		await fireEvent.click(left);
		// From order 2 (id 20) back to order 1 (id 10).
		expect(mockPlayerManager.currentZone).toBe(10);
	});

	it('falls back to the first ordered zone when the current zone is unknown', () => {
		mockPlayerManager.currentZone = 999;
		render(ZoneNav);
		const nav = screen.getByTestId('zone-nav');
		expect(nav.textContent).toContain('Zone · 01');
		expect(nav.textContent).toContain('Verdant Hollow');
	});

	it('locks the right arrow when the next zone is gated by an uncompleted challenge', async () => {
		// Zone B (order 2) is gated behind challenge 5, which the player has not completed.
		staticData.zones = [zone(10, 1, 'Alpha'), zone(20, 2, 'Beta', 5)];
		mockPlayerManager.currentZone = 10;
		playerChallenges.isChallengeCompleted.mockReturnValue(false);

		render(ZoneNav);
		const [, right] = screen.getAllByRole('button') as HTMLButtonElement[];

		expect(right.disabled).toBe(true);
		expect(right.getAttribute('aria-label')).toBe('Next zone locked');
		// A locked arrow shows the padlock glyph rather than the chevron.
		expect(right.querySelector('svg')).not.toBeNull();

		// Clicking a locked arrow does not navigate.
		await fireEvent.click(right);
		expect(mockPlayerManager.currentZone).toBe(10);
	});

	it('unlocks the right arrow once the gating challenge is completed', async () => {
		staticData.zones = [zone(10, 1, 'Alpha'), zone(20, 2, 'Beta', 5)];
		mockPlayerManager.currentZone = 10;
		// The only gated neighbour here is Beta; its gate is satisfied.
		playerChallenges.isChallengeCompleted.mockReturnValue(true);

		render(ZoneNav);
		const [, right] = screen.getAllByRole('button') as HTMLButtonElement[];

		expect(right.disabled).toBe(false);
		expect(right.querySelector('svg')).toBeNull();

		await fireEvent.click(right);
		expect(mockPlayerManager.currentZone).toBe(20);
	});

	it('locks the left arrow when the previous zone is gated and uncompleted', () => {
		// A non-linear gate: the lower-order zone is itself gated and uncompleted.
		staticData.zones = [zone(10, 1, 'Alpha', 5), zone(20, 2, 'Beta')];
		mockPlayerManager.currentZone = 20;
		playerChallenges.isChallengeCompleted.mockReturnValue(false);

		render(ZoneNav);
		const [left] = screen.getAllByRole('button') as HTMLButtonElement[];

		expect(left.disabled).toBe(true);
		expect(left.getAttribute('aria-label')).toBe('Previous zone locked');
		expect(left.querySelector('svg')).not.toBeNull();
	});
});
