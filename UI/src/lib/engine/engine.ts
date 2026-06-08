import { onDestroy } from 'svelte';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { BattleEngine } from './battle/battle-engine';
import { statify } from '$lib/common';
import { RenderEngine } from './render-engine';
import { LogicalEngine } from './logical-engine';
import { InventoryManager } from './player/inventory-manager';
import { EnemyManager } from './battle/enemy-manager';
import { staticData, statistics, acknowledgeModal } from '$stores';
import { apiSocket } from '$lib/api';

export const inventoryManager = statify(new InventoryManager());
export const enemyManager = statify(new EnemyManager());

export const logicEngine = statify(new LogicalEngine());
export const renderEngine = statify(new RenderEngine());
export const battleEngine = statify(new BattleEngine());

export const SESSION_REPLACED_TITLE = 'Session Replaced';
export const SESSION_REPLACED_BODY =
	'Another session has started elsewhere. You have been disconnected. You will be taken to the login screen.';

export const startGame = () => {
	if (staticData.loaded) {
		inventoryManager.initialize();
		// Fire-and-forget: the per-zone ZonesCleared values back the fight screen's
		// boss "Cleared" seal; a failed load just leaves the seal hidden.
		void statistics.load();
		startLogicEngine();
		startRenderEngine();
		startBattleEngine();
		apiSocket.listenCommand('SocketReplaced', handleSocketReplaced);
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
