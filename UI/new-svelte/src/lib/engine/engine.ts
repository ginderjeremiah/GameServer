import { onDestroy } from 'svelte';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { BattleEngine } from './battle/battle-engine';
import { statify } from '$lib/common';
import { RenderEngine } from './render-engine';
import { LogicalEngine } from './logical-engine';
import { InventoryManager } from './player/inventory-manager';
import { EnemyManager } from './battle/enemy-manager';
import { staticData, toastWarning } from '$stores';
import { apiSocket } from '$lib/api';

export const inventoryManager = statify(new InventoryManager());
export const enemyManager = statify(new EnemyManager());

export const logicEngine = statify(new LogicalEngine());
export const renderEngine = statify(new RenderEngine());
export const battleEngine = statify(new BattleEngine());

/** Message shown when the backend reports the player's session was taken over by another connection. */
export const SESSION_REPLACED_MESSAGE = 'Another session has started elsewhere. You have been disconnected.';

export const startGame = () => {
	if (staticData.loaded) {
		inventoryManager.initialize();
		startLogicEngine();
		startRenderEngine();
		startBattleEngine();
		apiSocket.listenCommand('SocketReplaced', handleSocketReplaced);
	}
};

// Use goto() not location.href — a reload re-runs the boot gate and bounces an authenticated client back into the game.
export const handleSocketReplaced = () => {
	stopGame();
	toastWarning(SESSION_REPLACED_MESSAGE, {
		duration: 0,
		onDismiss: () => {
			void goto(resolve('/'));
		}
	});
};

const stopGame = () => {
	logicEngine.stop();
	renderEngine.stop();
	enemyManager.stop();
	battleEngine.stop();
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
