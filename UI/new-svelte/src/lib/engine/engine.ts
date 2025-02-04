import { onDestroy } from 'svelte';
import { BattleEngine } from './battle/battle-engine';
import { routeTo, statify } from '$lib/common';
import { RenderEngine } from './render-engine';
import { LogicalEngine } from './logical-engine';
import { PlayerManager } from './player/player-manager';
import { InventoryManager } from './player/inventory-manager';
import { EnemyManager } from './battle/enemy-manager';
import { staticData } from '$stores';
import { apiSocket } from '$lib/api';

export const playerManager = statify(new PlayerManager());
export const inventoryManager = statify(new InventoryManager());
export const enemyManager = statify(new EnemyManager());

export const logicEngine = statify(new LogicalEngine());
export const renderEngine = statify(new RenderEngine());
export const battleEngine = statify(new BattleEngine());

export const startGame = () => {
	if (staticData.loaded) {
		inventoryManager.initialize();
		startLogicEngine();
		startRenderEngine();
		startBattleEngine();
		//TODO: pause game, alert user of another game instance, return to login page when alert is dismissed
		apiSocket.listenCommand('SocketReplaced', () => {
			window.location.href = '/';
		});
	}
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
