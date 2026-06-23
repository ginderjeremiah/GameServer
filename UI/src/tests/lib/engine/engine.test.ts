import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// onDestroy is only valid inside a component's init; stub it so startGame can run in a plain test.
vi.mock('svelte', async (importOriginal) => ({
	...((await importOriginal()) as Record<string, unknown>),
	onDestroy: vi.fn()
}));

const { goto } = vi.hoisted(() => ({ goto: vi.fn() }));
vi.mock('$app/navigation', () => ({ goto }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));

// Use the real modal store (so the full show → acknowledge → navigate flow is exercised) but stub
// staticData so we can toggle whether startGame runs.
const { staticDataStub, statisticsStub, playerChallengesStub, resetLogs, toastSuccess, navigation } = vi.hoisted(
	() => ({
		staticDataStub: { loaded: true } as {
			loaded: boolean;
			challenges?: ({ id: number; name: string; rewardItemId?: number } | undefined)[];
			items?: ({ id: number; name: string; rarityId: number; itemCategoryId: number } | undefined)[];
			itemMods?: unknown[];
			skills?: unknown[];
		},
		statisticsStub: { load: vi.fn(), reset: vi.fn() },
		playerChallengesStub: { load: vi.fn(), reset: vi.fn(), markCompleted: vi.fn() },
		resetLogs: vi.fn(),
		toastSuccess: vi.fn(),
		navigation: { requestScreen: vi.fn() }
	})
);
vi.mock('$stores', async () => {
	const modal = await vi.importActual<typeof import('$stores/modal.svelte')>('$stores/modal.svelte');
	return {
		...modal,
		staticData: staticDataStub,
		statistics: statisticsStub,
		playerChallenges: playerChallengesStub,
		resetLogs,
		toastSuccess,
		navigation
	};
});

const { listenCommand, disconnect, unlistenSocketReplaced, unlistenChallengeCompleted, unlistenServerCommandFailed } =
	vi.hoisted(() => {
		const unlistenSocketReplaced = vi.fn();
		const unlistenChallengeCompleted = vi.fn();
		const unlistenServerCommandFailed = vi.fn();
		const listenCommand = vi.fn().mockImplementation((command: string) => {
			if (command === 'SocketReplaced') {
				return unlistenSocketReplaced;
			}
			if (command === 'ChallengeCompleted') {
				return unlistenChallengeCompleted;
			}
			if (command === 'ServerCommandFailed') {
				return unlistenServerCommandFailed;
			}
		});
		const disconnect = vi.fn();
		return {
			listenCommand,
			disconnect,
			unlistenSocketReplaced,
			unlistenChallengeCompleted,
			unlistenServerCommandFailed
		};
	});
// Partial mock: keep $lib/api's real exports (other modules in the graph, e.g. $lib/common, rely on
// them) but swap apiSocket for a stub whose listenCommand/disconnect we can assert against.
vi.mock('$lib/api', async (importOriginal) => ({
	...((await importOriginal()) as Record<string, unknown>),
	apiSocket: { listenCommand, disconnect }
}));

// Lightweight engine/manager stubs whose lifecycle methods are spies.
vi.mock('$lib/engine/logical-engine', () => ({
	LogicalEngine: class {
		start = vi.fn();
		stop = vi.fn();
	},
	onIdleTimeLost: vi.fn(() => vi.fn())
}));
vi.mock('$lib/engine/render-engine', () => ({
	RenderEngine: class {
		start = vi.fn();
		stop = vi.fn();
	}
}));
vi.mock('$lib/engine/battle/enemy-manager', () => ({
	EnemyManager: class {
		start = vi.fn();
		stop = vi.fn();
	}
}));
vi.mock('$lib/engine/battle/battle-engine', () => ({
	BattleEngine: class {
		start = vi.fn();
		stop = vi.fn();
	}
}));
vi.mock('$lib/engine/player/inventory-manager', () => ({
	InventoryManager: class {
		initialize = vi.fn();
		addUnlockedItem = vi.fn();
		addUnlockedMod = vi.fn();
	}
}));

import {
	startGame,
	handleSocketReplaced,
	handleChallengeCompleted,
	handleServerCommandFailed,
	SESSION_REPLACED_TITLE,
	SESSION_REPLACED_BODY,
	logicEngine,
	renderEngine,
	enemyManager,
	battleEngine,
	inventoryManager
} from '$lib/engine/engine';
import { activeModal, clearModals, confirmActiveModal } from '$stores/modal.svelte';
import { EItemCategory, ERarity, type IApiSocketResponse } from '$lib/api';

/** Builds a ChallengeCompleted push response with the given reward ids (defaults: no rewards). */
const challengeCompletedResponse = (
	data: Partial<IApiSocketResponse<'ChallengeCompleted'>['data']> & { challengeId: number }
): IApiSocketResponse<'ChallengeCompleted'> =>
	({ id: '', name: 'ChallengeCompleted', data }) as IApiSocketResponse<'ChallengeCompleted'>;

beforeEach(() => {
	vi.clearAllMocks();
	clearModals();
	staticDataStub.loaded = true;
	staticDataStub.challenges = undefined;
	staticDataStub.items = undefined;
	staticDataStub.itemMods = undefined;
	staticDataStub.skills = undefined;
});

afterEach(() => {
	clearModals();
});

describe('handleSocketReplaced', () => {
	it('stops every engine and manager', () => {
		void handleSocketReplaced();

		expect(logicEngine.stop).toHaveBeenCalledTimes(1);
		expect(renderEngine.stop).toHaveBeenCalledTimes(1);
		expect(enemyManager.stop).toHaveBeenCalledTimes(1);
		expect(battleEngine.stop).toHaveBeenCalledTimes(1);
	});

	it('resets the combat log so its entries do not leak into the next session', () => {
		void handleSocketReplaced();

		expect(resetLogs).toHaveBeenCalledTimes(1);
	});

	it('shows an acknowledgement modal with the session-replaced title and body', () => {
		void handleSocketReplaced();

		const modal = activeModal.current;
		expect(modal).not.toBeNull();
		expect(modal?.kind).toBe('acknowledge');
		expect(modal?.title).toBe(SESSION_REPLACED_TITLE);
		expect(modal?.body).toBe(SESSION_REPLACED_BODY);
	});

	it('does not navigate until the modal is acknowledged', async () => {
		const promise = handleSocketReplaced();
		expect(goto).not.toHaveBeenCalled();

		confirmActiveModal();
		await promise;

		expect(goto).toHaveBeenCalledWith('/');
	});
});

describe('startGame', () => {
	it('initializes the inventory, starts the engines and wires SocketReplaced when data is loaded', () => {
		startGame();

		expect(inventoryManager.initialize).toHaveBeenCalledTimes(1);
		expect(logicEngine.start).toHaveBeenCalledTimes(1);
		expect(renderEngine.start).toHaveBeenCalledTimes(1);
		expect(enemyManager.start).toHaveBeenCalledTimes(1);
		expect(battleEngine.start).toHaveBeenCalledTimes(1);
		expect(listenCommand).toHaveBeenCalledWith('SocketReplaced', handleSocketReplaced, true);
		expect(listenCommand).toHaveBeenCalledWith('ChallengeCompleted', handleChallengeCompleted, true);
		expect(listenCommand).toHaveBeenCalledWith('ServerCommandFailed', handleServerCommandFailed, true);
	});

	it('does nothing when the static data is not loaded', () => {
		staticDataStub.loaded = false;
		startGame();

		expect(inventoryManager.initialize).not.toHaveBeenCalled();
		expect(logicEngine.start).not.toHaveBeenCalled();
		expect(listenCommand).not.toHaveBeenCalled();
	});

	it('the wired SocketReplaced handler stops the engines and shows the disconnect modal', () => {
		startGame();
		const handler = listenCommand.mock.calls[0][1] as () => void;

		void handler();

		expect(battleEngine.stop).toHaveBeenCalledTimes(1);
		expect(activeModal.current?.body).toBe(SESSION_REPLACED_BODY);
	});

	it('stopGame unregisters SocketReplaced, ChallengeCompleted and ServerCommandFailed listeners', () => {
		startGame();
		void handleSocketReplaced();

		expect(unlistenSocketReplaced).toHaveBeenCalledTimes(1);
		expect(unlistenChallengeCompleted).toHaveBeenCalledTimes(1);
		expect(unlistenServerCommandFailed).toHaveBeenCalledTimes(1);
	});

	it('stopGame disconnects the socket so the keepalive ping cannot reconnect after takeover', () => {
		startGame();
		void handleSocketReplaced();

		expect(disconnect).toHaveBeenCalledTimes(1);
	});
});

describe('handleChallengeCompleted', () => {
	it('marks the challenge completed so completion-gated UI updates without a refetch', () => {
		handleChallengeCompleted(challengeCompletedResponse({ challengeId: 7 }));

		expect(playerChallengesStub.markCompleted).toHaveBeenCalledWith(7);
	});

	it('unlocks an item reward so it can be equipped immediately', () => {
		handleChallengeCompleted(challengeCompletedResponse({ challengeId: 7, rewardItemId: 3 }));

		expect(inventoryManager.addUnlockedItem).toHaveBeenCalledWith({
			itemId: 3,
			equipped: false,
			favorite: false,
			appliedMods: []
		});
		expect(inventoryManager.addUnlockedMod).not.toHaveBeenCalled();
	});

	it('unlocks a mod reward', () => {
		handleChallengeCompleted(challengeCompletedResponse({ challengeId: 7, rewardItemModId: 5 }));

		expect(inventoryManager.addUnlockedMod).toHaveBeenCalledWith(5);
		expect(inventoryManager.addUnlockedItem).not.toHaveBeenCalled();
	});

	it('unlocks every reward kind a single challenge carries', () => {
		handleChallengeCompleted(challengeCompletedResponse({ challengeId: 7, rewardItemId: 3, rewardItemModId: 5 }));

		expect(inventoryManager.addUnlockedItem).toHaveBeenCalledTimes(1);
		expect(inventoryManager.addUnlockedMod).toHaveBeenCalledWith(5);
	});

	it('does nothing when the push carries no data', () => {
		handleChallengeCompleted({ id: '', name: 'ChallengeCompleted' } as IApiSocketResponse<'ChallengeCompleted'>);

		expect(playerChallengesStub.markCompleted).not.toHaveBeenCalled();
		expect(inventoryManager.addUnlockedItem).not.toHaveBeenCalled();
	});

	it('surfaces a success toast announcing the completed challenge', () => {
		staticDataStub.challenges = [];
		staticDataStub.challenges[7] = { id: 7, name: 'First Blood' };

		handleChallengeCompleted(challengeCompletedResponse({ challengeId: 7 }));

		expect(toastSuccess).toHaveBeenCalledWith('Challenge complete: First Blood', expect.anything());
	});

	it('names the unlocked reward in the toast', () => {
		staticDataStub.challenges = [];
		staticDataStub.challenges[7] = { id: 7, name: 'First Blood', rewardItemId: 3 };
		staticDataStub.items = [];
		staticDataStub.items[3] = { id: 3, name: 'Iron Helm', rarityId: ERarity.Rare, itemCategoryId: EItemCategory.Helm };

		handleChallengeCompleted(challengeCompletedResponse({ challengeId: 7, rewardItemId: 3 }));

		expect(toastSuccess).toHaveBeenCalledWith(
			'Challenge complete: First Blood — unlocked Iron Helm',
			expect.anything()
		);
	});

	it('does not toast when the completed challenge is missing from static data', () => {
		handleChallengeCompleted(challengeCompletedResponse({ challengeId: 7 }));

		expect(toastSuccess).not.toHaveBeenCalled();
	});

	it('the toast View action opens the Challenges screen', () => {
		staticDataStub.challenges = [];
		staticDataStub.challenges[7] = { id: 7, name: 'First Blood' };

		handleChallengeCompleted(challengeCompletedResponse({ challengeId: 7 }));

		const options = toastSuccess.mock.calls[0][1] as { action: { onClick: () => void } };
		options.action.onClick();
		expect(navigation.requestScreen).toHaveBeenCalledWith('challenges');
	});
});

/** Builds a ServerCommandFailed notice naming the server-pushed command that failed. */
const serverCommandFailedResponse = (commandName: string): IApiSocketResponse<'ServerCommandFailed'> =>
	({ id: '', name: 'ServerCommandFailed', data: { commandName } }) as IApiSocketResponse<'ServerCommandFailed'>;

describe('handleServerCommandFailed', () => {
	it('force-reloads challenge progress when a ChallengeCompleted push was dead-lettered', () => {
		handleServerCommandFailed(serverCommandFailedResponse('ChallengeCompleted'));

		expect(playerChallengesStub.load).toHaveBeenCalledWith(true);
	});

	it('does not reload challenges for an unrelated failed command', () => {
		handleServerCommandFailed(serverCommandFailedResponse('SocketReplaced'));

		expect(playerChallengesStub.load).not.toHaveBeenCalled();
	});
});
