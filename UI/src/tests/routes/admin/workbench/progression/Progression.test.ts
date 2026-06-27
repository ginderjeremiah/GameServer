import { describe, it, expect, afterEach, beforeAll, beforeEach, vi } from 'vitest';
import { render, cleanup, fireEvent, screen, waitFor } from '@testing-library/svelte';

// jsdom has no ResizeObserver; the Map surface observes its container on mount.
class ResizeObserverStub {
	observe() {}
	unobserve() {}
	disconnect() {}
}

const { fetchMock } = vi.hoisted(() => ({ fetchMock: vi.fn() }));

vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, ApiRequest: { get: vi.fn(), post: vi.fn() }, fetchSocketData: fetchMock };
});
vi.mock('$stores', () => ({ staticData: {}, toastError: vi.fn() }));
// The detail panes read select options off the reference store; empty arrays let them render inertly.
vi.mock('$routes/admin/workbench/reference.svelte', () => ({
	reference: {
		attributeOptions: () => [],
		modifierTypeOptions: () => [],
		playerSkillOptions: () => [],
		skillOptions: () => [],
		proficiencyOptions: () => [],
		proficiencyName: () => undefined
	}
}));

import Progression from '$routes/admin/workbench/progression/Progression.svelte';

const onePath = { id: 0, name: 'Fire', description: '', falloffBase: 0.6, contributions: [] };
const oneTier = {
	id: 0,
	name: 'Fire Magic',
	description: '',
	iconPath: '',
	word: '',
	pronunciation: '',
	translation: '',
	pathId: 0,
	pathOrdinal: 0,
	maxLevel: 10,
	baseXp: 100,
	xpGrowth: 1.4,
	seedSkillId: undefined,
	levelModifiers: [],
	levelRewards: [],
	prerequisiteIds: []
};

const seed = (paths: unknown[], profs: unknown[]) => {
	fetchMock.mockImplementation((command: string) => Promise.resolve(command === 'GetPaths' ? paths : profs));
};

beforeAll(() => {
	(globalThis as unknown as { ResizeObserver: typeof ResizeObserverStub }).ResizeObserver = ResizeObserverStub;
});

beforeEach(() => {
	fetchMock.mockReset();
	seed([], []);
});

afterEach(cleanup);

describe('Progression view toggle', () => {
	it('defaults to the List surface once loaded', async () => {
		render(Progression);
		await waitFor(() => expect(screen.getByTestId('progression-list')).toBeTruthy());
		expect(screen.queryByTestId('progression-map')).toBeNull();
		expect(screen.getByRole('button', { name: 'List' }).getAttribute('aria-pressed')).toBe('true');
		expect(screen.getByRole('button', { name: 'Map' }).getAttribute('aria-pressed')).toBe('false');
	});

	it('swaps to the Map surface when the Map toggle is clicked, and back to List', async () => {
		render(Progression);
		await waitFor(() => expect(screen.getByTestId('progression-list')).toBeTruthy());

		await fireEvent.click(screen.getByTestId('progression-map-toggle'));
		expect(screen.getByTestId('progression-map')).toBeTruthy();
		expect(screen.queryByTestId('progression-list')).toBeNull();
		expect(screen.getByRole('button', { name: 'Map' }).getAttribute('aria-pressed')).toBe('true');

		await fireEvent.click(screen.getByRole('button', { name: 'List' }));
		expect(screen.getByTestId('progression-list')).toBeTruthy();
		expect(screen.queryByTestId('progression-map')).toBeNull();
	});

	it('flips back to the List surface when a Map node navigates into the editor', async () => {
		seed([onePath], [oneTier]);
		render(Progression);
		await waitFor(() => expect(screen.getByTestId('progression-list')).toBeTruthy());

		await fireEvent.click(screen.getByTestId('progression-map-toggle'));
		const node = screen.getByTestId('progression-map').querySelector('[data-node="t0"]') as HTMLElement;
		await fireEvent.click(node);

		// onNavigate flipped the surface back to List, drilled into the clicked tier.
		expect(screen.queryByTestId('progression-map')).toBeNull();
		expect(screen.getByTestId('progression-list')).toBeTruthy();
	});
});
