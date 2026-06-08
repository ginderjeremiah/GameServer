{#each tooltips.data as tooltip (tooltip.id)}
	<div data-testid="tooltip-item">{tooltip.id}</div>
{/each}

{#if screen === 'fight'}
	<Registrar />
	<Registrar />
{:else}
	<Registrar />
	<Registrar />
{/if}

<script lang="ts">
import { tooltips } from '$stores/tooltip.svelte';
import Registrar from './TooltipRegistrarFixture.svelte';

// `screen` mimics the router swapping one page branch for another within a
// single component tree, while the consumer block keeps reading the reactive
// array throughout the swap.
let { screen }: { screen: 'fight' | 'inventory' } = $props();
</script>
