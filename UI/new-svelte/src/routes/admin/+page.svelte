<div class="admin-shell" data-testid="admin-screen">
	<div class="sidebar-spacer" class:pinned={sidebarPinned}></div>

	<div class="admin-workspace">
		<CurrentTool />
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

<script lang="ts">
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import AdminSidebar from './AdminSidebar.svelte';
import { adminGroups, adminTools } from './tools/nav';

let active = $state('addItems');
let sidebarPinned = $state(false);
const CurrentTool = $derived(adminTools.find((t) => t.key === active)?.component ?? adminTools[0].component);

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
	padding: 34px 44px 28px;
}
</style>
