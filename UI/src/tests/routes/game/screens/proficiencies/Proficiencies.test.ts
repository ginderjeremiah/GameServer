import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import type { IPath, IProficiency } from '$lib/api';
import { EActivityKey } from '$lib/api';

/* The screen drives the real playerProficiencies store (fetched over the socket) and reads the
   proficiency/path reference data from staticData, so the socket fetch and staticData are stubbed; the
   rest of the stores stay real (the $components barrel pulls in the engine, which reads them). The
   selected loadout / granted skills come from the real engine singletons (empty by default — the
   training state is exercised by the view-model suite). */
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
vi.mock('$stores', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$stores')>();
	return { ...actual, staticData, toastError: mockToastError };
});

import Proficiencies from '$routes/game/screens/proficiencies/Proficiencies.svelte';
import { playerProficiencies } from '$stores';

const prof = (o: Partial<IProficiency> & { id: number; pathId: number; pathOrdinal: number }): IProficiency => ({
	name: `Prof ${o.id}`,
	description: '',
	iconPath: '',
	word: `w${o.id}`,
	pronunciation: '',
	translation: '',
	maxLevel: 10,
	baseXp: 100,
	xpGrowth: 1,
	levelModifiers: [],
	levelRewards: [],
	prerequisiteIds: [],
	...o
});

const PROFICIENCIES: IProficiency[] = [
	prof({ id: 0, pathId: 0, pathOrdinal: 0, name: 'Fire' }),
	prof({ id: 1, pathId: 0, pathOrdinal: 1, name: 'Inferno' }),
	prof({ id: 2, pathId: 1, pathOrdinal: 0, name: 'Earth' })
];
const PATHS: IPath[] = [
	{ id: 0, name: 'Pyromancy', description: '', activityKey: EActivityKey.Fire },
	{ id: 1, name: 'Geomancy', description: '', activityKey: EActivityKey.Earth }
];

beforeEach(() => {
	playerProficiencies.reset();
	staticData.proficiencies = PROFICIENCIES;
	staticData.paths = PATHS;
	mockToastError.mockClear();
	// Fire maxed (reveals Inferno) on path 0, Earth opened on path 1 → two discovered paths.
	mockFetchSocket.mockResolvedValue([
		{ proficiencyId: 0, level: 10, xp: 0 },
		{ proficiencyId: 2, level: 1, xp: 5 }
	]);
});

afterEach(() => cleanup());

describe('Proficiencies screen', () => {
	it('fetches progress and renders the discovered spine', async () => {
		render(Proficiencies);
		expect(screen.getByTestId('proficiencies-screen')).toBeTruthy();
		// The first discovered path (Pyromancy) is selected by default; its spine shows Fire + Inferno.
		expect(await screen.findByTestId('tier-1')).toBeTruthy();
		expect(screen.getByTestId('tier-0')).toBeTruthy();
		expect(mockFetchSocket).toHaveBeenCalledWith('GetPlayerProficiencies');
		expect(screen.queryByTestId('proficiencies-empty')).toBeNull();
	});

	it('selecting a rail path switches the spine and marks it current', async () => {
		render(Proficiencies);
		await screen.findByTestId('rail-1');
		await fireEvent.click(screen.getByTestId('rail-1'));
		// Geomancy's only tier (Earth, id 2) now renders, and its rail row is aria-current.
		expect(screen.getByTestId('tier-2')).toBeTruthy();
		expect(screen.getByTestId('rail-1').getAttribute('aria-current')).toBe('true');
		expect(screen.queryByTestId('tier-1')).toBeNull();
	});

	it('selecting a spine tier marks it current', async () => {
		render(Proficiencies);
		const fire = await screen.findByTestId('tier-0');
		await fireEvent.click(fire);
		expect(screen.getByTestId('tier-0').getAttribute('aria-current')).toBe('true');
	});

	it('shows the empty state for a player who has discovered no path', async () => {
		mockFetchSocket.mockResolvedValue([]);
		render(Proficiencies);
		expect(await screen.findByTestId('proficiencies-empty')).toBeTruthy();
		expect(screen.queryByTestId('rail-0')).toBeNull();
		expect(mockToastError).not.toHaveBeenCalled();
	});

	it('surfaces an error (not the empty state) when the fetch fails', async () => {
		mockFetchSocket.mockRejectedValue(new Error('network down'));
		render(Proficiencies);
		expect(await screen.findByTestId('proficiencies-error')).toBeTruthy();
		expect(screen.queryByTestId('proficiencies-empty')).toBeNull();
		expect(mockToastError).toHaveBeenCalledTimes(1);
	});
});
