import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, fireEvent, cleanup, screen, waitFor } from '@testing-library/svelte';

// Doubles for the select page's wiring: the handoff supplies the character list, ApiRequest.post is
// keyed by route, and the navigation / takeover / world-entry side effects are mocked so the page can
// be driven without real I/O.
const {
	postMock,
	getMock,
	setTokensMock,
	getRefreshTokenMock,
	gotoMock,
	takeMock,
	initializeMock,
	confirmTakeoverMock
} = vi.hoisted(() => ({
	postMock: vi.fn(),
	getMock: vi.fn(),
	setTokensMock: vi.fn(),
	getRefreshTokenMock: vi.fn(),
	gotoMock: vi.fn(),
	takeMock: vi.fn(),
	initializeMock: vi.fn(),
	confirmTakeoverMock: vi.fn()
}));

// One creatable class so the picker has an option to default to (id 0 → the create call sends it).
const CREATABLE_CLASS = {
	id: 0,
	name: 'Warrior',
	description: 'A frontline fighter.',
	word: 'kor',
	passiveAttributeId: 1,
	passiveAmount: 5,
	passiveScalingAmount: 0,
	passiveModifierType: 1,
	attributeDistributions: [],
	starterSkills: [],
	starterEquipment: []
};

vi.mock('$app/environment', () => ({ browser: true }));
vi.mock('$app/navigation', () => ({ goto: gotoMock }));
vi.mock('$app/paths', () => ({ resolve: (p: string) => p }));
// Spread the real module so the enums/values the class picker's display helpers read at import stay
// intact; only the I/O entry points are replaced with spies.
vi.mock('$lib/api', async (importOriginal) => ({
	...((await importOriginal()) as Record<string, unknown>),
	ApiRequest: class {
		constructor(private route: string) {}
		post = (body: unknown) => postMock(this.route, body);
		get = () => getMock(this.route);
	},
	setTokens: setTokensMock,
	getRefreshToken: getRefreshTokenMock,
	reportDeviceInfo: vi.fn(),
	logout: vi.fn()
}));
vi.mock('$lib/engine', () => ({ playerManager: { initialize: initializeMock } }));
vi.mock('$routes/login/session-takeover', () => ({ confirmSessionTakeover: confirmTakeoverMock }));
vi.mock('$routes/select/player-select-handoff', () => ({ playerSelectHandoff: { take: takeMock, set: vi.fn() } }));

import SelectPage from '../../../routes/select/+page.svelte';

const SUMMARIES = [
	{ id: 1, name: 'Hero', level: 3, currentZoneId: 0, lastActivity: '2026-06-20T00:00:00Z' },
	{ id: 2, name: 'Rogue', level: 7, currentZoneId: 1, lastActivity: '2026-06-19T00:00:00Z' }
];

beforeEach(() => {
	postMock.mockReset();
	getMock.mockReset();
	// The create form fetches its class options over HTTP (Login/CharacterCreationData).
	getMock.mockResolvedValue({ status: 200, data: [CREATABLE_CLASS] });
	setTokensMock.mockClear();
	getRefreshTokenMock.mockReturnValue('r');
	gotoMock.mockClear();
	takeMock.mockReset();
	initializeMock.mockClear();
	confirmTakeoverMock.mockReset();
	confirmTakeoverMock.mockResolvedValue(true);
});

afterEach(cleanup);

describe('Select page', () => {
	it('redirects to login when reached without a handoff', async () => {
		takeMock.mockReturnValue(null);
		render(SelectPage);

		await waitFor(() => expect(gotoMock).toHaveBeenCalledWith('/'));
		expect(screen.queryByTestId('character-list')).toBeNull();
	});

	it('renders a card per handed-off character', async () => {
		takeMock.mockReturnValue(SUMMARIES);
		render(SelectPage);

		await waitFor(() => expect(screen.getAllByTestId('player-card')).toHaveLength(2));
		expect(screen.getByText('Hero')).toBeTruthy();
		expect(screen.getByText('Rogue')).toBeTruthy();
	});

	it('selects a character: binds, confirms takeover, seeds the player and enters loading', async () => {
		takeMock.mockReturnValue(SUMMARIES);
		postMock.mockResolvedValue({
			status: 200,
			data: { tokens: { accessToken: 'a2', refreshToken: 'r2' }, player: { id: 2, name: 'Rogue' } }
		});
		render(SelectPage);

		await waitFor(() => expect(screen.getAllByTestId('player-card')).toHaveLength(2));
		await fireEvent.click(screen.getAllByTestId('player-card')[1]);

		await waitFor(() =>
			expect(postMock).toHaveBeenCalledWith('Login/SelectPlayer', { playerId: 2, refreshToken: 'r' })
		);
		expect(setTokensMock).toHaveBeenCalledWith({ accessToken: 'a2', refreshToken: 'r2' });
		await waitFor(() => expect(initializeMock).toHaveBeenCalledWith({ id: 2, name: 'Rogue' }));
		expect(gotoMock).toHaveBeenCalledWith('/loading');
	});

	it('creates a character and appends it to the list', async () => {
		takeMock.mockReturnValue([SUMMARIES[0]]);
		postMock.mockResolvedValue({
			status: 200,
			data: { id: 9, name: 'Mage', level: 1, currentZoneId: 0, lastActivity: '2026-06-21T00:00:00Z' }
		});
		render(SelectPage);

		await waitFor(() => expect(screen.getAllByTestId('player-card')).toHaveLength(1));
		await fireEvent.click(screen.getByTestId('show-create'));
		await fireEvent.input(screen.getByTestId('new-name-input'), { target: { value: 'Mage' } });
		await fireEvent.submit(screen.getByTestId('create-form'));

		await waitFor(() => expect(postMock).toHaveBeenCalledWith('Login/CreatePlayer', { name: 'Mage', classId: 0 }));
		await waitFor(() => expect(screen.getAllByTestId('player-card')).toHaveLength(2));
		expect(screen.getByText('Mage')).toBeTruthy();
	});
});
