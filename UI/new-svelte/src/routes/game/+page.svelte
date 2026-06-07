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
import { screenMap, type GameScreen, GAME_SCREENS, visibleScreens } from './screens';
import { startGame } from '$lib/engine';
import { getRoles, logout } from '$lib/api';
import { confirmModal } from '$stores';
import { browser } from '$app/environment';
import type { Component } from 'svelte';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { onMount } from 'svelte';

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

// Roles drive which screens appear in the sidebar (e.g. Admin). Read from the access token after
// mount so the value matches the server render (no token available there) and updates on the client
// without a hydration mismatch — mirroring the boot gate's `booting`/`hydrated` pattern.
let roles = $state<string[]>([]);
onMount(() => {
	roles = getRoles();
});

const screens = $derived(visibleScreens(GAME_SCREENS, roles));

const screenKeyMap: Record<string, GameScreen> = {
	fight: 'Fight',
	cardGame: 'PlaceholderScreen',
	challenges: 'Challenges',
	inventory: 'Inventory',
	attributes: 'Attributes',
	attributeBreakdown: 'AttributeBreakdown',
	stats: 'Statistics',
	options: 'Options',
	help: 'PlaceholderScreen'
};

const handleNavigate = (key: string) => {
	if (key === 'admin') {
		goto(resolve('/admin'));
		return;
	}
	if (key === 'quit') {
		void confirmQuit();
		return;
	}
	currentScreen = key;
	const mapped = screenKeyMap[key];
	if (mapped && mapped in screenMap) {
		CurrentScreen = screenMap[mapped] as Component;
	}
};

// Confirm before ending the session — logging out tears down all in-memory game state, so guard
// against an accidental click on the quit control.
const confirmQuit = async () => {
	const confirmed = await confirmModal({
		title: 'Log out?',
		body: "You'll be signed out and returned to the login screen.",
		confirmLabel: 'Log out',
		cancelLabel: 'Stay'
	});
	if (confirmed) {
		logout();
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
