<div class="item-tooltip" bind:this={container} style={item ? '' : 'display: none;'}>
	<div class="tooltip-title">{item?.name}</div>
	<div class="tooltip-content">
		{#if attributeMap?.length}
			<div class="tooltip-header">Stats:</div>
			<ul>
				{#each attributeMap as attribute}
					<li>{attribute.name} {attribute.value > 0 ? '+' : ''}{attribute.value}</li>
				{/each}
			</ul>
		{/if}
		{#if appliedMods?.length}
			<div class="tooltip-header">Applied Mods:</div>
			<ul>
				{#each appliedMods as mod}
					<li>
						<b>{mod.name}</b>: {mod.description}
					</li>
				{/each}
			</ul>
		{/if}
		<div class="tooltip-header">Description:</div>
		<p class="description-text">{item?.description}</p>
		{#if item?.equipped}
			<div class="equipped-badge">Equipped</div>
		{/if}
	</div>
</div>

<script lang="ts">
import type { Item } from '$lib/battle';

export const getBaseNode = () => container;

type Props = {
	item: Item | undefined;
};

const { item }: Props = $props();

let container: HTMLDivElement;

const attributeMap = $derived(item?.totalAttributes?.getAttributeMap());
const appliedMods = $derived(item?.appliedMods);
</script>

<style lang="scss">
.item-tooltip {
	font-size: 0.75rem;
	min-width: 10rem;
	padding: 0.5rem;

	.tooltip-title {
		font-size: 1.25rem;
		margin-bottom: 0.5rem;
		text-align: center;
	}

	.tooltip-header {
		font-size: 1rem;
		margin-bottom: 0.5rem;
	}

	ul {
		margin: 0;
		padding-left: 1rem;
		list-style: none;
	}

	* + .tooltip-header {
		margin-top: 0.5rem;
	}

	.description-text {
		margin: 0 !important;
	}

	.equipped-badge {
		margin-top: 0.5rem;
		text-align: center;
		color: gold;
		font-weight: bold;
		font-size: 0.8rem;
	}
}
</style>
