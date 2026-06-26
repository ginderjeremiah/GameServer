import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, fireEvent, cleanup, screen, waitFor } from '@testing-library/svelte';

// Doubles for the switcher's wiring: ApiRequest is keyed by route (get for the list, post for the
// switch/create), and the socket teardown, engine stop, token storage, and takeover are mocked so the
// overlay can be driven without real I/O. window.location is replaced so the commit/recovery reloads are
// observable instead of throwing in jsdom.
const { getMock, postMock, disconnectMock, stopEnginesMock, getRefreshTokenMock, setTokensMock, confirmTakeoverMock } =
	vi.hoisted(() => ({
		getMock: vi.fn(),
		postMock: vi.fn(),
		disconnectMock: vi.fn(),
		stopEnginesMock: vi.fn(),
		getRefreshTokenMock: vi.fn(),
		setTokensMock: vi.fn(),
		confirmTakeoverMock: vi.fn()
	}));

// Spread the real module so the enums/values the class picker's display helpers read at import stay
// intact; only the I/O entry points are replaced with spies.
vi.mock('$lib/api', async (importOriginal) => ({
	...((await importOriginal()) as Record<string, unknown>),
	ApiRequest: class {
		constructor(private route: string) {}
		get = () => getMock(this.route);
		post = (body: unknown) => postMock(this.route, body);
	},
	apiSocket: { disconnect: disconnectMock },
	getRefreshToken: getRefreshTokenMock,
	setTokens: setTokensMock
}));
vi.mock('$lib/engine', () => ({
	playerManager: { id: 1 },
	stopEngines: stopEnginesMock
}));
vi.mock('$routes/login/session-takeover', () => ({ confirmSessionTakeover: confirmTakeoverMock }));

import CharacterSwitcher from '../../../routes/game/CharacterSwitcher.svelte';

const SUMMARIES = [
	{ id: 1, name: 'Hero', level: 3, currentZoneId: 0, lastActivity: '2026-06-20T00:00:00Z' },
	{ id: 2, name: 'Rogue', level: 7, currentZoneId: 1, lastActivity: '2026-06-19T00:00:00Z' }
];

// The create form's class options come from Login/CharacterCreationData; one active class (id 0) so
// creating a character defaults to and sends it.
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

// The switcher issues two GETs — the character list and the class options — so route the doubles.
const getByRoute = (route: string) =>
	route === 'Login/CharacterCreationData'
		? Promise.resolve({ status: 200, data: [CREATABLE_CLASS] })
		: Promise.resolve({ status: 200, data: SUMMARIES });

let originalLocation: Location;

beforeEach(() => {
	getMock.mockReset().mockImplementation(getByRoute);
	postMock.mockReset();
	disconnectMock.mockReset();
	stopEnginesMock.mockReset();
	getRefreshTokenMock.mockReset().mockReturnValue('r');
	setTokensMock.mockReset();
	confirmTakeoverMock.mockReset().mockResolvedValue(true);
	originalLocation = window.location;
	Object.defineProperty(window, 'location', {
		configurable: true,
		writable: true,
		value: { href: '', pathname: '/game' }
	});
});

afterEach(() => {
	Object.defineProperty(window, 'location', { configurable: true, writable: true, value: originalLocation });
	cleanup();
});

describe('CharacterSwitcher', () => {
	it('lists the account characters, excluding the one currently being played', async () => {
		render(CharacterSwitcher, { open: true, onClose: vi.fn() });

		await waitFor(() => expect(screen.getAllByTestId('player-card')).toHaveLength(1));
		expect(screen.getByText('Rogue')).toBeTruthy();
		expect(screen.queryByText('Hero')).toBeNull();
		expect(getMock).toHaveBeenCalledWith('Login/Players');
	});

	it('switches: tears down the game, credits via SwitchPlayer, confirms takeover, and reloads', async () => {
		postMock.mockResolvedValue({
			status: 200,
			data: { tokens: { accessToken: 'a2', refreshToken: 'r2' }, player: { id: 2, name: 'Rogue' } }
		});
		render(CharacterSwitcher, { open: true, onClose: vi.fn() });

		await waitFor(() => expect(screen.getAllByTestId('player-card')).toHaveLength(1));
		await fireEvent.click(screen.getAllByTestId('player-card')[0]);

		await waitFor(() =>
			expect(postMock).toHaveBeenCalledWith('Login/SwitchPlayer', { playerId: 2, refreshToken: 'r' })
		);
		// The socket/engines are torn down before the credit runs.
		expect(stopEnginesMock).toHaveBeenCalled();
		expect(disconnectMock).toHaveBeenCalled();
		expect(setTokensMock).toHaveBeenCalledWith({ accessToken: 'a2', refreshToken: 'r2' });
		await waitFor(() => expect(confirmTakeoverMock).toHaveBeenCalled());
		await waitFor(() => expect(window.location.href).toBe('/'));
	});

	it('recovers with a reload when the switch fails after teardown', async () => {
		postMock.mockResolvedValue({ status: 400, error: 'nope' });
		render(CharacterSwitcher, { open: true, onClose: vi.fn() });

		await waitFor(() => expect(screen.getAllByTestId('player-card')).toHaveLength(1));
		await fireEvent.click(screen.getAllByTestId('player-card')[0]);

		await waitFor(() => expect(window.location.href).toBe('/'));
		expect(stopEnginesMock).toHaveBeenCalled();
		expect(disconnectMock).toHaveBeenCalled();
		expect(setTokensMock).not.toHaveBeenCalled();
	});

	it('closes without touching the game when cancelled', async () => {
		const onClose = vi.fn();
		render(CharacterSwitcher, { open: true, onClose });

		await waitFor(() => expect(screen.getByTestId('switcher-cancel')).toBeTruthy());
		await fireEvent.click(screen.getByTestId('switcher-cancel'));

		expect(onClose).toHaveBeenCalled();
		expect(stopEnginesMock).not.toHaveBeenCalled();
		expect(disconnectMock).not.toHaveBeenCalled();
	});

	it('closes on Escape via the shared overlay primitive', async () => {
		const onClose = vi.fn();
		render(CharacterSwitcher, { open: true, onClose });

		await waitFor(() => expect(screen.getByTestId('switcher-cancel')).toBeTruthy());
		await fireEvent.keyDown(window, { key: 'Escape' });

		expect(onClose).toHaveBeenCalled();
	});

	it('does not load characters while closed', async () => {
		render(CharacterSwitcher, { open: false, onClose: vi.fn() });

		expect(getMock).not.toHaveBeenCalled();
		expect(screen.queryByTestId('character-switcher')).toBeNull();
	});

	it('surfaces an error state when the character list fails to load', async () => {
		getMock.mockResolvedValue({ status: 500, error: 'boom' });
		render(CharacterSwitcher, { open: true, onClose: vi.fn() });

		await waitFor(() => expect(screen.getByTestId('switcher-error')).toBeTruthy());
		expect(screen.getByText('boom')).toBeTruthy();
	});

	it('creates a new character and appends it to the switch list', async () => {
		postMock.mockResolvedValue({
			status: 200,
			data: { id: 9, name: 'Mage', level: 1, currentZoneId: 0, lastActivity: '2026-06-21T00:00:00Z' }
		});
		render(CharacterSwitcher, { open: true, onClose: vi.fn() });

		await waitFor(() => expect(screen.getAllByTestId('player-card')).toHaveLength(1));
		await fireEvent.click(screen.getByTestId('show-create'));
		// A class is required, so wait for the picker (the default class is selected once it loads).
		await waitFor(() => expect(screen.getByTestId('class-picker')).toBeTruthy());
		await fireEvent.input(screen.getByTestId('new-name-input'), { target: { value: 'Mage' } });
		await fireEvent.submit(screen.getByTestId('create-form'));

		await waitFor(() => expect(postMock).toHaveBeenCalledWith('Login/CreatePlayer', { name: 'Mage', classId: 0 }));
		await waitFor(() => expect(screen.getAllByTestId('player-card')).toHaveLength(2));
		expect(screen.getByText('Mage')).toBeTruthy();
	});
});
