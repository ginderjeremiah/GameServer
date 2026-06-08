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
const { staticDataStub, statisticsStub } = vi.hoisted(() => ({
	staticDataStub: { loaded: true },
	statisticsStub: { load: vi.fn(), reset: vi.fn() }
}));
vi.mock('$stores', async () => {
	const modal = await vi.importActual<typeof import('$stores/modal.svelte')>('$stores/modal.svelte');
	return { ...modal, staticData: staticDataStub, statistics: statisticsStub };
});

const { listenCommand } = vi.hoisted(() => ({ listenCommand: vi.fn() }));
// Partial mock: keep $lib/api's real exports (other modules in the graph, e.g. $lib/common, rely on
// them) but swap apiSocket for a stub whose listenCommand we can assert against.
vi.mock('$lib/api', async (importOriginal) => ({
	...((await importOriginal()) as Record<string, unknown>),
	apiSocket: { listenCommand }
}));

// Lightweight engine/manager stubs whose lifecycle methods are spies.
vi.mock('$lib/engine/logical-engine', () => ({
	LogicalEngine: class {
		start = vi.fn();
		stop = vi.fn();
	}
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
	}
}));

import {
	startGame,
	handleSocketReplaced,
	SESSION_REPLACED_TITLE,
	SESSION_REPLACED_BODY,
	logicEngine,
	renderEngine,
	enemyManager,
	battleEngine,
	inventoryManager
} from '$lib/engine/engine';
import { activeModal, clearModals, confirmActiveModal } from '$stores/modal.svelte';

beforeEach(() => {
	vi.clearAllMocks();
	clearModals();
	staticDataStub.loaded = true;
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
		expect(listenCommand).toHaveBeenCalledWith('SocketReplaced', handleSocketReplaced);
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
});
