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
		{#if itemMods?.length}
			<div class="tooltip-header">Mods:</div>
			<ul>
				{#each itemMods as mod}
					<li>
						<b>{mod.name}</b>: {mod.description}
					</li>
				{/each}
			</ul>
		{/if}
		<div class="tooltip-header">Description:</div>
		<p class="description-text">{item?.description}</p>
	</div>
</div>

<script lang="ts">
import { type InventorySlot } from '$lib/engine';

export const getBaseNode = () => container;

type Props = {
	slot: InventorySlot | undefined;
};

const { slot }: Props = $props();

let container: HTMLDivElement;

const item = $derived(slot?.item);
const attributeMap = $derived(item?.totalAttributes?.getAttributeMap());
const itemMods = $derived(item?.itemMods);
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
}
</style>
