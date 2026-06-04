<!-- Mirrors TooltipBase: a keyed each over tooltips.data that subscribes to the
     reactive array, so it is actively read while screens mount/unmount. -->
{#each tooltips.data as tooltip (tooltip.id)}
	<div data-testid="tooltip-item">{tooltip.id}</div>
{/each}

<script lang="ts">
import { tooltips } from '$stores/tooltip.svelte';
</script>
