{#if authorized}
	<div class="admin-shell" data-testid="admin-screen">
		<div class="sidebar-spacer" class:pinned={sidebarPinned}></div>

		<div class="admin-workspace">
			{#if active === DEAD_LETTERS_TOOL_KEY}
				<DeadLetterConsole />
			{:else if reference.loaded && activeEntity}
				{#key active}
					<Workbench entity={activeEntity} groupLabel={groupLabelFor(active)} />
				{/key}
			{:else}
				<Loading loading={true} delay={50} />
			{/if}
		</div>

		<AdminSidebar
			tools={adminTools}
			groups={adminGroups}
			{active}
			onNavigate={handleNavigate}
			onBackToGame={backToGame}
			bind:pinned={sidebarPinned}
		/>
	</div>
{/if}

<script lang="ts">
import { onMount } from 'svelte';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { Loading } from '$components';
import AdminSidebar from './AdminSidebar.svelte';
import Workbench from './workbench/Workbench.svelte';
import DeadLetterConsole from './ops/DeadLetterConsole.svelte';
import { entityByKey, groupLabelFor } from './workbench/entities';
import { adminGroups, adminTools, DEAD_LETTERS_TOOL_KEY } from './workbench/nav';
import { reference } from './workbench/reference.svelte';
import { ensureAdminAccess } from './admin-access';
import { toastError } from '$stores';

let active = $state('enemies');
let sidebarPinned = $state(false);
// Gates rendering until the client-side admin-role check passes. Starts false so the server render
// (no token available) shows nothing, and a non-admin who deep-links here is redirected before the
// workbench mounts or its reference data loads.
let authorized = $state(false);

const activeEntity = $derived(entityByKey(active));

// Guard the route to Admins, then load the shared reference catalogues (used by every entity's
// select options, tag UI, and derived spawn shares) before rendering any workbench.
onMount(() => {
	authorized = ensureAdminAccess();
	if (authorized && !reference.loaded) {
		reference.load().catch((ex) => {
			toastError(ex instanceof Error ? ex.message : 'Failed to load admin reference data.');
		});
	}
});

const handleNavigate = (key: string) => {
	active = key;
};

const backToGame = () => goto(resolve('/game'));
</script>

<style lang="scss">
.admin-shell {
	width: 100%;
	height: 100%;
	display: flex;
	position: relative;
	overflow: hidden;
	// Flat near-black page surface (matches the design), instead of the game's
	// gradient backdrop showing through behind the workbench panels.
	background: var(--page);
}

.sidebar-spacer {
	width: 60px;
	flex-shrink: 0;
	transition: width 220ms cubic-bezier(0.4, 0, 0.2, 1);

	&.pinned {
		width: 240px;
	}
}

.admin-workspace {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	overflow: hidden;
	// No outer padding: the Workbench is full-bleed (list pane flush to the rail,
	// full-width header/save bar) and supplies its own internal insets.
}
</style>
