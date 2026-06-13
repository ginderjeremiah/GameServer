import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { EChallengeType, type IChallenge, type IZone } from '$lib/api';

// ZoneNav reads the zone list from the reference-data store, the active zone from the player
// manager, and challenge completion from the shared store; all are mocked so the test controls
// the navigation + lock state directly. The real `$lib/common` zone-progression helpers are used.
// The lock tooltip is registered through the store; the registration is stubbed and the child
// ChallengeTooltip resolves the gating challenge from the same mocked reference data.
const { mockPlayerManager, staticData, playerChallenges, registerTooltipComponent } = vi.hoisted(() => ({
	mockPlayerManager: { currentZone: 0 },
	staticData: {
		zones: [] as IZone[],
		challenges: [] as IChallenge[],
		challengeTypes: [] as unknown[],
		items: [] as unknown[],
		itemMods: [] as unknown[],
		skills: [] as unknown[]
	},
	playerChallenges: { isChallengeCompleted: vi.fn(() => false) },
	registerTooltipComponent: vi.fn(() => ({
		setTooltipPosition: vi.fn(),
		showTooltip: vi.fn(),
		hideTooltip: vi.fn()
	}))
}));

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager }));
vi.mock('$stores', () => ({ staticData, playerChallenges, registerTooltipComponent }));

import ZoneNav from '$routes/game/screens/fight/ZoneNav.svelte';

const challenge = (id: number, name: string, description: string): IChallenge =>
	({ id, name, description, challengeTypeId: EChallengeType.EnemiesKilled, progressGoal: 10 }) as IChallenge;

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
	staticData.challenges = [];
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

	it('shows the actual gating challenge (not a boss assumption) when hovering a locked arrow', async () => {
		// Beta (order 2) is gated behind a non-boss challenge; hovering the locked arrow surfaces it.
		staticData.zones = [zone(10, 1, 'Alpha'), zone(20, 2, 'Beta', 5)];
		// Challenges are id-indexed (zero-based-id catalogue), so the gate (id 5) sits at index 5.
		staticData.challenges = [];
		staticData.challenges[5] = challenge(5, 'Cull the Swarm', 'Defeat 10 enemies');
		mockPlayerManager.currentZone = 10;
		playerChallenges.isChallengeCompleted.mockReturnValue(false);

		const { container } = render(ZoneNav);
		const wraps = container.querySelectorAll('.zone-btn-wrap');
		await fireEvent.mouseEnter(wraps[1], { clientX: 5, clientY: 5 });

		// The gating challenge's name + requirement appear, and what it unlocks (Beta).
		expect(screen.getByText('Cull the Swarm')).toBeTruthy();
		expect(screen.getByText('Defeat 10 enemies')).toBeTruthy();
		expect(screen.getByText('Beta')).toBeTruthy();
		// The old, inaccurate boss-only hint is gone.
		expect(container.textContent).not.toContain("Clear this zone's boss");

		// Leaving clears the tooltip's challenge so it no longer renders the gate.
		await fireEvent.mouseLeave(wraps[1]);
		expect(screen.queryByText('Cull the Swarm')).toBeNull();
	});

	it('does not surface a tooltip when hovering an unlocked (navigable) arrow', async () => {
		staticData.zones = [zone(10, 1, 'Alpha'), zone(20, 2, 'Beta')];
		staticData.challenges = [];
		staticData.challenges[5] = challenge(5, 'Cull the Swarm', 'Defeat 10 enemies');
		mockPlayerManager.currentZone = 10;

		const { container } = render(ZoneNav);
		const wraps = container.querySelectorAll('.zone-btn-wrap');
		await fireEvent.mouseEnter(wraps[1], { clientX: 5, clientY: 5 });

		// The right arrow is open (no gate), so hovering it shows nothing.
		expect(screen.queryByText('Cull the Swarm')).toBeNull();
	});
});
