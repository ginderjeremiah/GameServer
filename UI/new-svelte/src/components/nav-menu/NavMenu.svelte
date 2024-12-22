<div class="nav-menu">
	<div class="nav-menu-items">
		{#each navMenuItems as item}
			<NavMenuItem {...item} />
		{/each}
	</div>
	{#if showTickCounter}
		<div>
			<span class="tick-counter">{logicalState.tickRate}</span>
			<span class="tick-counter">{renderState.tickRate}</span>
		</div>
	{/if}
</div>

<script lang="ts">
import { logicalState, renderState } from '$lib/engine';
import type { INavMenuItem } from './types';
import NavMenuItem from './NavMenuItem.svelte';

interface Props {
	navMenuItems: INavMenuItem[];
	showTickCounter?: boolean;
}

const { navMenuItems, showTickCounter = true }: Props = $props();
</script>

<style lang="scss">
.nav-menu {
	height: fit-content;
	width: 100%;
	background-color: var(--nav-bar-color);
	display: flex;
	justify-content: space-between;

	.nav-menu-items {
		height: fit-content;
		background-color: inherit;
		display: flex;
	}

	.tick-counter {
		color: var(--title-color);
		font-size: 1.5rem;
		vertical-align: middle;
		margin-right: 0.25rem;
	}
}
</style>
