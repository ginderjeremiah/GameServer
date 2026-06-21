{#if welcome.phase === 'summary' && welcome.summary}
	<WelcomeBackGate summary={welcome.summary} onEnter={() => welcome.enter()} />
{:else if welcome.phase === 'entered'}
	<div class="game-container" data-testid="game-screen">
		<div class="sidebar-spacer" class:pinned={sidebarPinned}></div>

		<div class="main-content">
			<div class="screen-container" data-testid="screen-container">
				{#if CurrentScreen}
					<CurrentScreen />
				{:else}
					<PlaceholderScreen label={currentScreenDef?.label ?? ''} />
				{/if}
			</div>
			<LogPanel />
		</div>

		<NavSidebar bind:pinned={sidebarPinned} {screens} active={currentScreen} onNavigate={handleNavigate} />
	</div>
{:else}
	<BootSplash />
{/if}

<script lang="ts">
import { NavSidebar, LogPanel } from '$components';
import { GAME_SCREENS, visibleScreens } from './screens/screen-defs';
import PlaceholderScreen from './screens/PlaceholderScreen.svelte';
import { startGame, stopEngines, enemyManager } from '$lib/engine';
import { refreshPlayer } from '$lib/engine/session';
import { navigation } from '$stores';
import { apiSocket, getRoles } from '$lib/api';
import { browser } from '$app/environment';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { onDestroy, onMount } from 'svelte';
import { confirmQuit } from './game-actions';
import { WelcomeBackView } from './welcome-back/welcome-back-view.svelte';
import WelcomeBackGate from './welcome-back/WelcomeBackGate.svelte';
import BootSplash from '../BootSplash.svelte';

// The welcome-back gate runs the offline-progress check before the idle loop starts (so simulated and
// live battles never overlap), then either passes straight through or shows the away-summary gate. The
// engine touch points are injected so the flow stays testable without the live engine.
const welcome = new WelcomeBackView({
	fetchProgress: async () => {
		const response = await apiSocket.sendSocketCommand('GetOfflineProgress');
		return response.error ? null : (response.data ?? null);
	},
	resyncPlayer: refreshPlayer,
	reconcileMode: (autoChallengeBoss) => enemyManager.reconcilePersistedMode(autoChallengeBoss),
	enterGame: () => {
		try {
			startGame();
		} catch (e) {
			console.error('Failed to start game', e);
		}
	}
});

if (browser) {
	// Cleanup is registered once at init (the gate may start the loops later, after an await, where
	// onDestroy can't be called); stopEngines is idempotent, so it's safe even if the loops never ran.
	onDestroy(stopEngines);
	onMount(() => {
		void welcome.run();
	});
}

let currentScreen = $state<string>('fight');
let sidebarPinned = $state(false);

// Roles drive which screens appear in the sidebar (e.g. Admin). Read from the access token after
// mount so the value matches the server render (no token available there) and updates on the client
// without a hydration mismatch — mirroring the boot gate's `booting`/`hydrated` pattern.
let roles = $state<string[]>([]);
onMount(() => {
	roles = getRoles();
});

const screens = $derived(visibleScreens(GAME_SCREENS, roles));

// The active screen's component is derived from its registry entry, so navigation only updates the
// current key — no separate key→component map to keep in sync. A "wip" entry has no component and
// falls back to the placeholder in the template.
const currentScreenDef = $derived(GAME_SCREENS.find((s) => s.key === currentScreen));
const CurrentScreen = $derived(currentScreenDef?.component);

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
};

// Another screen can request a switch (carrying a one-shot payload the target consumes on mount) via
// the navigation store — e.g. the Statistics screen deep-linking an enemy into the Codex dossier.
$effect(() => {
	const requested = navigation.requestedScreen;
	if (requested) {
		handleNavigate(requested);
		navigation.clear();
	}
});
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
