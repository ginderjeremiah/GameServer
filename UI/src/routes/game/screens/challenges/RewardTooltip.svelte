<div class="reward-tooltip" bind:this={container} style={reward ? '' : 'display: none;'}>
	{#if reward}
		<!-- One tooltip per reward kind; the `masked` flag redacts an unrevealed reward in place. -->
		{#if reward.kind === 'item' && reward.item}
			<ItemTooltip item={reward.item} masked={!reward.revealed} />
		{:else if reward.mod}
			<ModTooltip mod={reward.mod} masked={!reward.revealed} />
		{/if}
	{/if}
</div>

<script lang="ts">
import ItemTooltip from '../inventory/ItemTooltip.svelte';
import ModTooltip from './ModTooltip.svelte';
import type { ResolvedReward } from './challenges-view.svelte';

export const getBaseNode = () => container;

interface Props {
	reward: ResolvedReward | undefined;
}

const { reward }: Props = $props();

// Bound to the root element and relocated into the global tooltip container by
// getBaseNode(); reactive so the relocation runs once this has mounted.
let container = $state<HTMLDivElement>();
</script>
