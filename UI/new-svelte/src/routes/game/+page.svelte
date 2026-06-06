<div class="game-container" data-testid="game-screen">
	<div class="sidebar-spacer" class:pinned={sidebarPinned}></div>

	<div class="main-content">
		<div class="screen-container" data-testid="screen-container">
			<CurrentScreen />
		</div>
		<LogPanel />
	</div>

	<NavSidebar bind:pinned={sidebarPinned} {screens} active={currentScreen} onNavigate={handleNavigate} />
</div>

<script lang="ts">
import { NavSidebar, LogPanel } from '$components';
import { screenMap, type GameScreen } from './screens';
import { startGame } from '$lib/engine';
import { logout } from '$lib/api';
import { browser } from '$app/environment';
import type { Component } from 'svelte';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';

if (browser) {
	try {
		startGame();
	} catch (e) {
		console.error('Failed to start game', e);
	}
}

let currentScreen = $state<string>('fight');
let CurrentScreen: Component = $state(screenMap.Fight as Component);
let sidebarPinned = $state(false);

interface ScreenDef {
	key: string;
	label: string;
	group: string;
	built: boolean;
}

const screens: ScreenDef[] = [
	{ key: 'fight', label: 'Fight', group: 'combat', built: true },
	{ key: 'cardGame', label: 'Card Game', group: 'combat', built: false },
	{ key: 'challenges', label: 'Challenges', group: 'combat', built: true },
	{ key: 'inventory', label: 'Inventory', group: 'character', built: true },
	{ key: 'attributes', label: 'Attributes', group: 'character', built: true },
	{ key: 'stats', label: 'Stats', group: 'character', built: false },
	{ key: 'options', label: 'Options', group: 'settings', built: true },
	{ key: 'help', label: 'Help', group: 'settings', built: false },
	{ key: 'quit', label: 'Quit', group: 'settings', built: true },
	{ key: 'admin', label: 'Admin', group: 'admin', built: true }
];

const screenKeyMap: Record<string, GameScreen> = {
	fight: 'Fight',
	cardGame: 'PlaceholderScreen',
	challenges: 'Challenges',
	inventory: 'Inventory',
	attributes: 'Attributes',
	stats: 'PlaceholderScreen',
	options: 'Options',
	help: 'PlaceholderScreen'
};

const handleNavigate = (key: string) => {
	if (key === 'admin') {
		goto(resolve('/admin'));
		return;
	}
	if (key === 'quit') {
		logout();
		return;
	}
	currentScreen = key;
	const mapped = screenKeyMap[key];
	if (mapped && mapped in screenMap) {
		CurrentScreen = screenMap[mapped] as Component;
	}
};
</script>

<style lang="scss">
.game-container {
	width: 100%;
	height: 100%;
	display: flex;
	position: relative;
	overflow: hidden;
}

.sidebar-spacer {
	width: 60px;
	flex-shrink: 0;
	transition: width 220ms cubic-bezier(0.4, 0, 0.2, 1);

	&.pinned {
		width: 240px;
	}
}

.main-content {
	flex: 1;
	display: flex;
	flex-direction: column;
	min-height: 0;
	min-width: 0;
}

.screen-container {
	flex: 1;
	min-height: 0;
	overflow: auto;
}
</style>
