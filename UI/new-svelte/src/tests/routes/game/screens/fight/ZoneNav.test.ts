import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import type { IZone } from '$lib/api';

// ZoneNav reads the zone list from the reference-data store and the active zone from the
// player manager; both are mocked so the test controls the navigation state directly.
const { mockPlayerManager, staticData } = vi.hoisted(() => ({
	mockPlayerManager: { currentZone: 0 },
	staticData: { zones: [] as IZone[] }
}));

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager }));
vi.mock('$stores', () => ({ staticData }));

import ZoneNav from '$routes/game/screens/fight/ZoneNav.svelte';

const zone = (id: number, order: number, name: string): IZone => ({
	id,
	name,
	description: '',
	order,
	levelMin: 1,
	levelMax: 10
});

beforeEach(() => {
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
});
