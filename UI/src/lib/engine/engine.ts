import { onDestroy } from 'svelte';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { BattleEngine } from './battle/battle-engine';
import { statify, type Action } from '$lib/common';
import { RenderEngine } from './render-engine';
import { LogicalEngine } from './logical-engine';
import { InventoryManager } from './player/inventory-manager';
import { EnemyManager } from './battle/enemy-manager';
import { staticData, statistics, playerChallenges, acknowledgeModal, resetLogs } from '$stores';
import { apiSocket, type IApiSocketResponse } from '$lib/api';
import { playerManager } from './player/player-manager';

export const inventoryManager = statify(new InventoryManager());
export const enemyManager = statify(new EnemyManager());

export const logicEngine = statify(new LogicalEngine());
export const renderEngine = statify(new RenderEngine());
export const battleEngine = statify(new BattleEngine());

export const SESSION_REPLACED_TITLE = 'Session Replaced';
export const SESSION_REPLACED_BODY =
	'Another session has started elsewhere. You have been disconnected. You will be taken to the login screen.';

let socketReplacedUnhook: Action | undefined;
let challengeCompletedUnhook: Action | undefined;
let serverCommandFailedUnhook: Action | undefined;

export const startGame = () => {
	if (staticData.loaded) {
		inventoryManager.initialize();
		// Fire-and-forget: the per-zone ZonesCleared values back the fight screen's
		// boss "Cleared" seal; a failed load just leaves the seal hidden.
		void statistics.load();
		// Challenge completion gates zone navigation (a zone unlocks once its gating challenge is
		// completed); a failed load just leaves zones gated as-is until the next load.
		void playerChallenges.load();
		startLogicEngine();
		startRenderEngine();
		startBattleEngine();
		socketReplacedUnhook = apiSocket.listenCommand('SocketReplaced', handleSocketReplaced, true);
		challengeCompletedUnhook = apiSocket.listenCommand('ChallengeCompleted', handleChallengeCompleted, true);
		serverCommandFailedUnhook = apiSocket.listenCommand('ServerCommandFailed', handleServerCommandFailed, true);
	}
};

/**
 * Reacts to a ServerCommandFailed notice: the server dead-lettered a server-pushed command that threw and
 * is telling us to re-sync rather than silently diverge from the authoritative state. The only push that
 * leaves divergent client state is ChallengeCompleted (its completion gates zone navigation), so a failed
 * one force-reloads the authoritative challenge progress; any reward it carried still surfaces on the next
 * natural load of the inventory/skills stores.
 */
export const handleServerCommandFailed = (response: IApiSocketResponse<'ServerCommandFailed'>) => {
	const failedCommand = response.data?.commandName;
	console.warn(`A server-pushed command failed on the server and was dead-lettered: ${failedCommand ?? 'unknown'}.`);
	if (failedCommand === 'ChallengeCompleted') {
		void playerChallenges.load(true);
	}
};

/**
 * Applies a completed-challenge push from the server: marks the challenge complete (so completion-gated
 * UI like zone navigation and the reward reveal update without a refetch) and unlocks each reward it
 * carries so the player can use it immediately — equip the item/mod, select the skill — instead of only
 * after a page refresh.
 */
export const handleChallengeCompleted = (response: IApiSocketResponse<'ChallengeCompleted'>) => {
	const data = response.data;
	if (!data) {
		return;
	}

	playerChallenges.markCompleted(data.challengeId);

	if (data.rewardItemId != null) {
		inventoryManager.addUnlockedItem({
			itemId: data.rewardItemId,
			equipped: false,
			favorite: false,
			appliedMods: []
		});
	}
	if (data.rewardItemModId != null) {
		inventoryManager.addUnlockedMod(data.rewardItemModId);
	}
	if (data.rewardSkillId != null) {
		playerManager.addUnlockedSkill(data.rewardSkillId);
	}
};

// Use goto() not location.href — a reload re-runs the boot gate and bounces an authenticated client back into the game.
export const handleSocketReplaced = async () => {
	stopGame();
	await acknowledgeModal({
		title: SESSION_REPLACED_TITLE,
		body: SESSION_REPLACED_BODY,
		confirmLabel: 'Go to Login'
	});
	void goto(resolve('/'));
};

const stopGame = () => {
	logicEngine.stop();
	renderEngine.stop();
	enemyManager.stop();
	battleEngine.stop();
	statistics.reset();
	playerChallenges.reset();
	resetLogs();
	socketReplacedUnhook?.();
	challengeCompletedUnhook?.();
	serverCommandFailedUnhook?.();
	// SocketReplaced routes back to login client-side (no reload), so the socket singleton survives. Tear
	// it down explicitly, otherwise the keepalive ping would silently reconnect and fight the session that
	// just took over.
	apiSocket.disconnect();
};

const startLogicEngine = () => {
	logicEngine.start();
	onDestroy(() => {
		logicEngine.stop();
	});
};

const startRenderEngine = () => {
	renderEngine.start();
	onDestroy(() => {
		renderEngine.stop();
	});
};

const startBattleEngine = () => {
	enemyManager.start();
	battleEngine.start();
	onDestroy(() => {
		enemyManager.stop();
		battleEngine.stop();
	});
};
