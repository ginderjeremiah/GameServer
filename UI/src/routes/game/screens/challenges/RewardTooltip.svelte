<div class="reward-tooltip" bind:this={container} style={reward ? '' : 'display: none;'}>
	{#if reward}
		{#if reward.revealed}
			<!-- Revealed rewards open the exact tooltip you'd see inspecting the thing in your bag. -->
			{#if reward.kind === 'item' && reward.item}
				<ItemTooltip item={reward.item} />
			{:else if reward.mod}
				<ModTooltip mod={reward.mod} />
			{/if}
		{:else if reward.kind === 'item' && reward.item}
			<SealedItemTooltip item={reward.item} />
		{:else if reward.mod}
			<SealedModTooltip mod={reward.mod} />
		{/if}
	{/if}
</div>

<script lang="ts">
import ItemTooltip from '../inventory/ItemTooltip.svelte';
import ModTooltip from './ModTooltip.svelte';
import SealedItemTooltip from './SealedItemTooltip.svelte';
import SealedModTooltip from './SealedModTooltip.svelte';
import type { ResolvedReward } from './challenges-view.svelte';

export const getBaseNode = () => container;

interface Props {
	reward: ResolvedReward | undefined;
}

const { reward }: Props = $props();

let container: HTMLDivElement;
</script>
