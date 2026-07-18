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
const {
	staticDataStub,
	statisticsStub,
	playerChallengesStub,
	playerProficienciesStub,
	resetLogs,
	toastSuccess,
	navigation,
	clearToasts
} = vi.hoisted(() => ({
	staticDataStub: { loaded: true, reset: vi.fn() } as {
		loaded: boolean;
		reset: () => void;
		challenges?: ({ id: number; name: string; rewardItemId?: number } | undefined)[];
		items?: ({ id: number; name: string; rarityId: number; itemCategoryId: number } | undefined)[];
		itemMods?: unknown[];
		skills?: ({ id: number; name: string } | undefined)[];
		skillRecipes?: ISkillRecipe[];
		proficiencies?: (
			| { id: number; name: string; levelRewards: { level: number; rewardSkillId: number }[] }
			| undefined
		)[];
	},
	statisticsStub: { load: vi.fn(), reset: vi.fn() },
	playerChallengesStub: { load: vi.fn(), reset: vi.fn(), markCompleted: vi.fn() },
	playerProficienciesStub: {
		load: vi.fn(),
		reset: vi.fn(),
		levelOf: vi.fn(() => 0),
		applyXpGained: vi.fn(),
		all: [] as { proficiencyId: number; level: number; xp: number }[]
	},
	resetLogs: vi.fn(),
	toastSuccess: vi.fn(),
	navigation: { requestScreen: vi.fn(), reset: vi.fn() },
	clearToasts: vi.fn()
}));
vi.mock('$stores', async () => {
	const modal = await vi.importActual<typeof import('$stores/modal.svelte')>('$stores/modal.svelte');
	const tutorialTourModule =
		await vi.importActual<typeof import('$stores/tutorial-tour.svelte')>('$stores/tutorial-tour.svelte');
	return {
		...modal,
		...tutorialTourModule,
		staticData: staticDataStub,
		statistics: statisticsStub,
		playerChallenges: playerChallengesStub,
		playerProficiencies: playerProficienciesStub,
		resetLogs,
		toastSuccess,
		navigation,
		clearToasts
	};
});

const {
	listenCommand,
	disconnect,
	unlistenChallengeCompleted,
	unlistenProficiencyXpGained,
	unlistenServerCommandFailed
} = vi.hoisted(() => {
	const unlistenChallengeCompleted = vi.fn();
	const unlistenProficiencyXpGained = vi.fn();
	const unlistenServerCommandFailed = vi.fn();
	const listenCommand = vi.fn().mockImplementation((command: string) => {
		if (command === 'ChallengeCompleted') {
			return unlistenChallengeCompleted;
		}
		if (command === 'ProficiencyXpGained') {
			return unlistenProficiencyXpGained;
		}
		if (command === 'ServerCommandFailed') {
			return unlistenServerCommandFailed;
		}
	});
	const disconnect = vi.fn();
	return {
		listenCommand,
		disconnect,
		unlistenChallengeCompleted,
		unlistenProficiencyXpGained,
		unlistenServerCommandFailed
	};
});
const { logMessage } = vi.hoisted(() => ({ logMessage: vi.fn() }));
const { addUnlockedSkill, playerManagerStub } = vi.hoisted(() => {
	const addUnlockedSkill = vi.fn();
	return {
		addUnlockedSkill,
		playerManagerStub: { unlockedSkills: [] as { skillId: number; selected: boolean }[], addUnlockedSkill }
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
vi.mock('$lib/engine/player/player-manager', () => ({ playerManager: playerManagerStub }));
vi.mock('$lib/engine/log', () => ({ logMessage }));
const { refreshPlayer } = vi.hoisted(() => ({ refreshPlayer: vi.fn(() => Promise.resolve()) }));
vi.mock('$lib/engine/session', () => ({ refreshPlayer }));

import {
	startGame,
	stopEngines,
	handleSocketReplaced,
	handleChallengeCompleted,
	handleProficiencyXpGained,
	handleServerCommandFailed,
	SESSION_REPLACED_TITLE,
	SESSION_REPLACED_BODY,
	logicEngine,
	renderEngine,
	enemyManager,
	battleEngine,
	inventoryManager
} from '$lib/engine/engine';
import { activeModal, clearModals, confirmActiveModal, showModal } from '$stores/modal.svelte';
import { tutorialTour } from '$stores/tutorial-tour.svelte';
import { createHook } from '$lib/common/hooks';
import { onDestroy } from 'svelte';
import {
	EItemCategory,
	ELessonTriggerType,
	ELogType,
	ERarity,
	type IApiSocketResponse,
	type IProficiencyXpResultModel,
	type IProficiencyOpenedModel,
	type ISkillRecipe
} from '$lib/api';

/** Builds a ChallengeCompleted push response with the given reward ids (defaults: no rewards). */
const challengeCompletedResponse = (
	data: Partial<IApiSocketResponse<'ChallengeCompleted'>['data']> & { challengeId: number }
): IApiSocketResponse<'ChallengeCompleted'> =>
	({ id: '', name: 'ChallengeCompleted', data }) as IApiSocketResponse<'ChallengeCompleted'>;

let hidden = false;

beforeEach(() => {
	vi.clearAllMocks();
	clearModals();
	tutorialTour.clear();
	hidden = false;
	Object.defineProperty(document, 'hidden', { configurable: true, get: () => hidden });
	staticDataStub.loaded = true;
	staticDataStub.challenges = undefined;
	staticDataStub.items = undefined;
	staticDataStub.itemMods = undefined;
	staticDataStub.skills = undefined;
	staticDataStub.proficiencies = undefined;
	staticDataStub.skillRecipes = undefined;
	playerManagerStub.unlockedSkills = [];
	playerProficienciesStub.all = [];
	// Mirror the real managers so the before/after creatable-recipe diff sees state actually change: a
	// granted skill joins the unlocked set, and an applied push updates the proficiency levels.
	addUnlockedSkill.mockImplementation((skillId: number) => {
		if (!playerManagerStub.unlockedSkills.some((skill) => skill.skillId === skillId)) {
			playerManagerStub.unlockedSkills.push({ skillId, selected: false });
		}
	});
	playerProficienciesStub.applyXpGained.mockImplementation(
		(model: { proficiencies: { proficiencyId: number; newLevel: number; newXp: number }[] }) => {
			for (const result of model.proficiencies) {
				const existing = playerProficienciesStub.all.find((p) => p.proficiencyId === result.proficiencyId);
				if (existing) {
					existing.level = result.newLevel;
					existing.xp = result.newXp;
				} else {
					playerProficienciesStub.all.push({
						proficiencyId: result.proficiencyId,
						level: result.newLevel,
						xp: result.newXp
					});
				}
			}
		}
	);
});

afterEach(() => {
	clearModals();
	tutorialTour.clear();
	// vi.unstubAllGlobals doesn't undo a defineProperty, so drop the own-property override of
	// document.hidden and let it fall back to the jsdom prototype getter.
	delete (document as unknown as Record<string, unknown>).hidden;
	// engine.ts's captured unhook closures are module-level state that outlives a single test; clear them
	// so a test calling startGame() without a matching stop can't leak its listeners into the next test.
	stopEngines();
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
	it('initializes the inventory, starts the engines and wires its screen-scoped socket listeners when data is loaded', () => {
		startGame();

		expect(inventoryManager.initialize).toHaveBeenCalledTimes(1);
		// Player progress the live battler needs is loaded before the engines start.
		expect(playerChallengesStub.load).toHaveBeenCalledTimes(1);
		expect(playerProficienciesStub.load).toHaveBeenCalledTimes(1);
		expect(logicEngine.start).toHaveBeenCalledTimes(1);
		expect(renderEngine.start).toHaveBeenCalledTimes(1);
		expect(enemyManager.start).toHaveBeenCalledTimes(1);
		expect(battleEngine.start).toHaveBeenCalledTimes(1);
		expect(listenCommand).toHaveBeenCalledWith('ChallengeCompleted', handleChallengeCompleted);
		expect(listenCommand).toHaveBeenCalledWith('ProficiencyXpGained', handleProficiencyXpGained);
		expect(listenCommand).toHaveBeenCalledWith('ServerCommandFailed', handleServerCommandFailed);
	});

	it('does not wire SocketReplaced itself — it is registered app-wide by the root layout (#1836)', () => {
		startGame();

		expect(listenCommand).not.toHaveBeenCalledWith('SocketReplaced', expect.anything());
	});

	it('does not opt into onDestroy cleanup, since it runs outside component init (#1631)', () => {
		startGame();

		for (const call of listenCommand.mock.calls) {
			expect(call).toHaveLength(2);
		}
	});

	it('registers all three listeners against the real hook without a component context (#1631)', () => {
		// This file's top-level mock stubs both apiSocket.listenCommand and svelte's onDestroy away, which
		// would hide a regression that re-adds cleanupOnDestroy: true (the mocked onDestroy never throws).
		// Route the mocked listenCommand through the real createHook instead, so startGame exercises the
		// real onNotified/onDestroy wiring; only the cleanupOnDestroy flag it passes is under test here —
		// whether onDestroy itself throws outside a component is covered directly in hooks.test.ts.
		const hooks = {
			ChallengeCompleted: createHook(),
			ProficiencyXpGained: createHook(),
			ServerCommandFailed: createHook()
		};
		// One-shot per call (consumed in startGame's fixed call order) so the override doesn't leak into
		// later tests that rely on the base mock's unlisten-spy return values.
		for (const hook of Object.values(hooks)) {
			listenCommand.mockImplementationOnce((_command: string, action: (...args: unknown[]) => void) =>
				hook.onNotified(action)
			);
		}

		startGame();

		expect(onDestroy).not.toHaveBeenCalled();
	});

	it('threads an already-known active battle (#1595/#1596/#1597) into enemyManager.start', () => {
		const activeBattle = {
			id: 2,
			level: 1,
			seed: 1,
			enemyRating: 100,
			selectedSkills: [],
			attributes: [],
			elapsedOffsetMs: 12000,
			isBossBattle: false
		};

		startGame(activeBattle);

		expect(enemyManager.start).toHaveBeenCalledWith(activeBattle);
	});

	it('does nothing when the static data is not loaded', () => {
		staticDataStub.loaded = false;
		startGame();

		expect(inventoryManager.initialize).not.toHaveBeenCalled();
		expect(logicEngine.start).not.toHaveBeenCalled();
		expect(listenCommand).not.toHaveBeenCalled();
	});

	it('stopGame unregisters ChallengeCompleted, ProficiencyXpGained and ServerCommandFailed listeners', () => {
		startGame();
		void handleSocketReplaced();

		expect(unlistenChallengeCompleted).toHaveBeenCalledTimes(1);
		expect(unlistenProficiencyXpGained).toHaveBeenCalledTimes(1);
		expect(unlistenServerCommandFailed).toHaveBeenCalledTimes(1);
	});

	it('stopGame disconnects the socket so the keepalive ping cannot reconnect after takeover', () => {
		startGame();
		void handleSocketReplaced();

		expect(disconnect).toHaveBeenCalledTimes(1);
	});

	it('stopGame clears any active toasts so they cannot leak onto the next session/login screen (#2038)', () => {
		startGame();
		void handleSocketReplaced();

		expect(clearToasts).toHaveBeenCalledTimes(1);
	});

	it('stopGame clears any queued modal rather than leaving it to show ahead of the session-replaced acknowledgement (#2038)', async () => {
		const priorModal = showModal({ title: 'Old', body: 'A stale confirm dialog' });

		void handleSocketReplaced();

		await expect(priorModal).resolves.toBe(false);
		expect(activeModal.current?.title).toBe(SESSION_REPLACED_TITLE);
	});

	it('stopGame resets the navigation intent so a queued screen request cannot leak into the next session (#2038)', () => {
		startGame();
		void handleSocketReplaced();

		expect(navigation.reset).toHaveBeenCalledTimes(1);
	});

	it("stopGame clears the in-memory reference data so a subsequent login re-checks content versions instead of reusing the prior session's copy (#2067)", () => {
		startGame();
		void handleSocketReplaced();

		expect(staticDataStub.reset).toHaveBeenCalledTimes(1);
	});

	it('stopGame clears an open coach-mark tour so it cannot leak onto the next character (#2092)', () => {
		startGame();
		tutorialTour.play({
			id: 1,
			key: 'lesson-key',
			name: 'Lesson Name',
			triggerType: ELessonTriggerType.ScreenVisit,
			screenKey: 'fight',
			ordinal: 0,
			designerNotes: '',
			steps: [{ ordinal: 0, text: 'Step text' }]
		});

		void handleSocketReplaced();

		expect(tutorialTour.activeLesson).toBeNull();
	});

	it("unhooks the previous call's listeners before re-registering, so a remount cannot leak them (#1807)", () => {
		startGame();
		startGame();

		expect(unlistenChallengeCompleted).toHaveBeenCalledTimes(1);
		expect(unlistenProficiencyXpGained).toHaveBeenCalledTimes(1);
		expect(unlistenServerCommandFailed).toHaveBeenCalledTimes(1);
	});
});

describe('stopEngines', () => {
	it('stops every loop and the background-throttle monitor', () => {
		stopEngines();

		expect(logicEngine.stop).toHaveBeenCalledTimes(1);
		expect(renderEngine.stop).toHaveBeenCalledTimes(1);
		expect(enemyManager.stop).toHaveBeenCalledTimes(1);
		expect(battleEngine.stop).toHaveBeenCalledTimes(1);
	});

	it('unhooks the three startGame-registered socket listeners (the game route unmount cleanup, #1807)', () => {
		startGame();
		stopEngines();

		expect(unlistenChallengeCompleted).toHaveBeenCalledTimes(1);
		expect(unlistenProficiencyXpGained).toHaveBeenCalledTimes(1);
		expect(unlistenServerCommandFailed).toHaveBeenCalledTimes(1);
	});

	it('is idempotent and safe to call when startGame never ran', () => {
		expect(() => {
			stopEngines();
			stopEngines();
		}).not.toThrow();
	});

	it('lets a later startGame register fresh listeners without leaking the previous set', () => {
		startGame();
		stopEngines();
		vi.clearAllMocks();

		startGame();

		expect(unlistenChallengeCompleted).not.toHaveBeenCalled();
		expect(listenCommand).toHaveBeenCalledWith('ChallengeCompleted', handleChallengeCompleted);
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

	it('does not toast while the tab is hidden (#1598), but still marks the challenge completed', () => {
		staticDataStub.challenges = [];
		staticDataStub.challenges[7] = { id: 7, name: 'First Blood' };
		hidden = true;

		handleChallengeCompleted(challengeCompletedResponse({ challengeId: 7 }));

		expect(toastSuccess).not.toHaveBeenCalled();
		expect(playerChallengesStub.markCompleted).toHaveBeenCalledWith(7);
	});
});

/** Builds a ProficiencyXpGained push response with the given results / opened nodes. */
const proficiencyXpResult = (
	over: Partial<IProficiencyXpResultModel> & { proficiencyId: number }
): IProficiencyXpResultModel => ({
	xpGained: 10,
	newLevel: 1,
	newXp: 0,
	milestonesCrossed: [],
	grantedSkillIds: [],
	...over
});
const proficiencyXpGainedResponse = (
	proficiencies: IProficiencyXpResultModel[],
	opened: IProficiencyOpenedModel[] = []
): IApiSocketResponse<'ProficiencyXpGained'> =>
	({
		id: '',
		name: 'ProficiencyXpGained',
		data: { proficiencies, opened }
	}) as IApiSocketResponse<'ProficiencyXpGained'>;

describe('handleProficiencyXpGained', () => {
	beforeEach(() => {
		staticDataStub.proficiencies = [];
		staticDataStub.proficiencies[0] = { id: 0, name: 'Fire Magic', levelRewards: [{ level: 5, rewardSkillId: 12 }] };
		staticDataStub.skills = [];
		staticDataStub.skills[12] = { id: 12, name: 'Fireball' };
	});

	it('applies the push to the proficiency store', () => {
		const response = proficiencyXpGainedResponse([proficiencyXpResult({ proficiencyId: 0, newLevel: 2 })]);
		handleProficiencyXpGained(response);

		expect(playerProficienciesStub.applyXpGained).toHaveBeenCalledWith(response.data);
	});

	it('logs the XP gained on the Proficiency channel, rounding the decimal', () => {
		handleProficiencyXpGained(proficiencyXpGainedResponse([proficiencyXpResult({ proficiencyId: 0, xpGained: 11.6 })]));

		expect(logMessage).toHaveBeenCalledWith(ELogType.Proficiency, 'Fire Magic: +12 proficiency XP');
	});

	it('does not log an XP line when the rounded gain is zero', () => {
		handleProficiencyXpGained(proficiencyXpGainedResponse([proficiencyXpResult({ proficiencyId: 0, xpGained: 0 })]));

		expect(logMessage).not.toHaveBeenCalledWith(ELogType.Proficiency, expect.stringContaining('proficiency XP'));
	});

	it('toasts and logs a plain level-up when no milestone was crossed', () => {
		playerProficienciesStub.levelOf.mockReturnValueOnce(1);
		handleProficiencyXpGained(proficiencyXpGainedResponse([proficiencyXpResult({ proficiencyId: 0, newLevel: 2 })]));

		expect(logMessage).toHaveBeenCalledWith(ELogType.Proficiency, 'Fire Magic reached level 2');
		expect(toastSuccess).toHaveBeenCalledWith('Fire Magic reached level 2', undefined);
	});

	it('does not announce a level-up when the level is unchanged', () => {
		playerProficienciesStub.levelOf.mockReturnValueOnce(2);
		handleProficiencyXpGained(proficiencyXpGainedResponse([proficiencyXpResult({ proficiencyId: 0, newLevel: 2 })]));

		expect(toastSuccess).not.toHaveBeenCalledWith(expect.stringContaining('reached level'));
	});

	it('toasts and logs a milestone naming the granted skill, and suppresses the redundant plain level-up toast', () => {
		playerProficienciesStub.levelOf.mockReturnValueOnce(4);
		handleProficiencyXpGained(
			proficiencyXpGainedResponse([
				proficiencyXpResult({ proficiencyId: 0, newLevel: 5, milestonesCrossed: [5], grantedSkillIds: [12] })
			])
		);

		const milestone = 'Fire Magic milestone reached: level 5 — unlocked Fireball';
		expect(toastSuccess).toHaveBeenCalledWith(milestone, undefined);
		expect(logMessage).toHaveBeenCalledWith(ELogType.Proficiency, milestone);
		expect(toastSuccess).not.toHaveBeenCalledWith('Fire Magic reached level 5', undefined);
	});

	it('unlocks milestone-granted skills so they are usable immediately', () => {
		handleProficiencyXpGained(
			proficiencyXpGainedResponse([proficiencyXpResult({ proficiencyId: 0, grantedSkillIds: [12] })])
		);

		expect(addUnlockedSkill).toHaveBeenCalledWith(12);
	});

	it('toasts a newly-opened proficiency (notification-only — grants no skill)', () => {
		staticDataStub.proficiencies = [];
		staticDataStub.proficiencies[3] = { id: 3, name: 'Inferno Magic', levelRewards: [] };

		handleProficiencyXpGained(proficiencyXpGainedResponse([], [{ proficiencyId: 3 }]));

		expect(toastSuccess).toHaveBeenCalledWith('New proficiency unlocked: Inferno Magic', undefined);
		expect(addUnlockedSkill).not.toHaveBeenCalled();
	});

	it('does nothing when the push carries no data', () => {
		handleProficiencyXpGained({ id: '', name: 'ProficiencyXpGained' } as IApiSocketResponse<'ProficiencyXpGained'>);

		expect(playerProficienciesStub.applyXpGained).not.toHaveBeenCalled();
		expect(toastSuccess).not.toHaveBeenCalled();
	});

	it('skips feedback for an unknown proficiency reference id but still applies the store update', () => {
		const response = proficiencyXpGainedResponse([proficiencyXpResult({ proficiencyId: 99, newLevel: 2 })]);
		handleProficiencyXpGained(response);

		expect(toastSuccess).not.toHaveBeenCalled();
		expect(playerProficienciesStub.applyXpGained).toHaveBeenCalledWith(response.data);
	});

	it('does not toast a milestone while the tab is hidden (#1598), but still logs it and applies the grant', () => {
		hidden = true;
		playerProficienciesStub.levelOf.mockReturnValueOnce(4);
		handleProficiencyXpGained(
			proficiencyXpGainedResponse([
				proficiencyXpResult({ proficiencyId: 0, newLevel: 5, milestonesCrossed: [5], grantedSkillIds: [12] })
			])
		);

		expect(toastSuccess).not.toHaveBeenCalled();
		expect(logMessage).toHaveBeenCalledWith(
			ELogType.Proficiency,
			'Fire Magic milestone reached: level 5 — unlocked Fireball'
		);
		expect(addUnlockedSkill).toHaveBeenCalledWith(12);
	});

	it('does not toast a plain level-up or a newly-opened proficiency while the tab is hidden (#1598)', () => {
		hidden = true;
		playerProficienciesStub.levelOf.mockReturnValueOnce(1);
		staticDataStub.proficiencies![3] = { id: 3, name: 'Inferno Magic', levelRewards: [] };

		handleProficiencyXpGained(
			proficiencyXpGainedResponse([proficiencyXpResult({ proficiencyId: 0, newLevel: 2 })], [{ proficiencyId: 3 }])
		);

		expect(toastSuccess).not.toHaveBeenCalled();
	});
});

describe('handleProficiencyXpGained — new-recipe-available toast', () => {
	const skillRecipe = (
		id: number,
		resultSkillId: number,
		inputSkillIds: number[],
		conditions: { proficiencyId: number; minLevel: number }[] = []
	): ISkillRecipe => ({ id, resultSkillId, designerNotes: '', inputSkillIds, conditions });

	beforeEach(() => {
		// A small world: skills 0/1/2 are inputs, skill 4 the synthesis result; proficiency 0 grants skill 1
		// at its level-5 milestone.
		staticDataStub.skills = [];
		staticDataStub.skills[0] = { id: 0, name: 'Ember Strike' };
		staticDataStub.skills[1] = { id: 1, name: 'Frost Lance' };
		staticDataStub.skills[4] = { id: 4, name: 'Frostfire' };
		staticDataStub.skills[5] = { id: 5, name: 'Lava Surge' };
		staticDataStub.proficiencies = [];
		staticDataStub.proficiencies[0] = { id: 0, name: 'Pyromancy', levelRewards: [{ level: 5, rewardSkillId: 1 }] };
	});

	it('toasts when a milestone-granted skill completes a recipe’s inputs', () => {
		staticDataStub.skillRecipes = [skillRecipe(0, 4, [0, 1])];
		playerManagerStub.unlockedSkills = [{ skillId: 0, selected: false }]; // owns one input → not yet creatable

		// The level-5 milestone grants skill 1, completing the recipe's inputs (0 + 1).
		handleProficiencyXpGained(
			proficiencyXpGainedResponse([
				proficiencyXpResult({ proficiencyId: 0, newLevel: 5, milestonesCrossed: [5], grantedSkillIds: [1] })
			])
		);

		expect(toastSuccess).toHaveBeenCalledWith('New recipe available: Frostfire', expect.anything());
	});

	it('toasts when a level-up crosses a recipe’s proficiency condition', () => {
		staticDataStub.skillRecipes = [skillRecipe(0, 5, [0, 1], [{ proficiencyId: 0, minLevel: 5 }])];
		// Owns every input, but the condition gates it until proficiency 0 reaches level 5.
		playerManagerStub.unlockedSkills = [
			{ skillId: 0, selected: false },
			{ skillId: 1, selected: false }
		];
		playerProficienciesStub.all = [{ proficiencyId: 0, level: 3, xp: 0 }];

		handleProficiencyXpGained(proficiencyXpGainedResponse([proficiencyXpResult({ proficiencyId: 0, newLevel: 5 })]));

		expect(toastSuccess).toHaveBeenCalledWith('New recipe available: Lava Surge', expect.anything());
	});

	it('does not toast while the recipe is still missing an input', () => {
		staticDataStub.skillRecipes = [skillRecipe(0, 4, [0, 1, 2])]; // needs a third input the grant won't supply
		playerManagerStub.unlockedSkills = [{ skillId: 0, selected: false }];

		handleProficiencyXpGained(
			proficiencyXpGainedResponse([
				proficiencyXpResult({ proficiencyId: 0, newLevel: 5, milestonesCrossed: [5], grantedSkillIds: [1] })
			])
		);

		expect(toastSuccess).not.toHaveBeenCalledWith(expect.stringContaining('New recipe available'), expect.anything());
	});

	it('does not re-toast a recipe that was already creatable before the push', () => {
		staticDataStub.skillRecipes = [skillRecipe(0, 4, [0, 1])];
		playerManagerStub.unlockedSkills = [
			{ skillId: 0, selected: false },
			{ skillId: 1, selected: false }
		]; // already creatable before the push

		handleProficiencyXpGained(proficiencyXpGainedResponse([proficiencyXpResult({ proficiencyId: 0, newLevel: 5 })]));

		expect(toastSuccess).not.toHaveBeenCalledWith(expect.stringContaining('New recipe available'), expect.anything());
	});

	it('skips a newly-creatable recipe whose result skill is missing from the catalogue', () => {
		staticDataStub.skillRecipes = [skillRecipe(0, 99, [0, 1])]; // result 99 is not in the catalogue
		playerManagerStub.unlockedSkills = [{ skillId: 0, selected: false }];

		handleProficiencyXpGained(
			proficiencyXpGainedResponse([
				proficiencyXpResult({ proficiencyId: 0, newLevel: 5, milestonesCrossed: [5], grantedSkillIds: [1] })
			])
		);

		expect(toastSuccess).not.toHaveBeenCalledWith(expect.stringContaining('New recipe available'), expect.anything());
	});

	it('the toast View action opens the Synthesis screen', () => {
		staticDataStub.skillRecipes = [skillRecipe(0, 4, [0, 1])];
		playerManagerStub.unlockedSkills = [{ skillId: 0, selected: false }];

		handleProficiencyXpGained(
			proficiencyXpGainedResponse([
				proficiencyXpResult({ proficiencyId: 0, newLevel: 5, milestonesCrossed: [5], grantedSkillIds: [1] })
			])
		);

		const recipeToast = toastSuccess.mock.calls.find((call) => call[0] === 'New recipe available: Frostfire');
		const options = recipeToast?.[1] as { action: { onClick: () => void } };
		options.action.onClick();
		expect(navigation.requestScreen).toHaveBeenCalledWith('synthesis');
	});

	it('does not toast a newly-available recipe while the tab is hidden (#1598)', () => {
		hidden = true;
		staticDataStub.skillRecipes = [skillRecipe(0, 4, [0, 1])];
		playerManagerStub.unlockedSkills = [{ skillId: 0, selected: false }];

		handleProficiencyXpGained(
			proficiencyXpGainedResponse([
				proficiencyXpResult({ proficiencyId: 0, newLevel: 5, milestonesCrossed: [5], grantedSkillIds: [1] })
			])
		);

		expect(toastSuccess).not.toHaveBeenCalled();
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

	it('resyncs the player and re-derives inventory when a ChallengeCompleted push was dead-lettered, so a reward item it carried appears immediately', async () => {
		handleServerCommandFailed(serverCommandFailedResponse('ChallengeCompleted'));
		await Promise.resolve();
		await Promise.resolve();

		expect(refreshPlayer).toHaveBeenCalledTimes(1);
		expect(inventoryManager.initialize).toHaveBeenCalledTimes(1);
	});

	it('force-reloads proficiency progress when a ProficiencyXpGained push was dead-lettered', () => {
		handleServerCommandFailed(serverCommandFailedResponse('ProficiencyXpGained'));

		expect(playerProficienciesStub.load).toHaveBeenCalledWith(true);
	});

	it('does not resync the player for a dead-lettered ProficiencyXpGained push', () => {
		handleServerCommandFailed(serverCommandFailedResponse('ProficiencyXpGained'));

		expect(refreshPlayer).not.toHaveBeenCalled();
	});

	it('does not reload challenges for an unrelated failed command', () => {
		handleServerCommandFailed(serverCommandFailedResponse('SocketReplaced'));

		expect(playerChallengesStub.load).not.toHaveBeenCalled();
		expect(refreshPlayer).not.toHaveBeenCalled();
	});
});
