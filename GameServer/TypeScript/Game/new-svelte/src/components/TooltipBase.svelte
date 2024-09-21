{#each $tooltips as tooltip}
	<Tooltip {tooltip} />
{/each}

<script lang="ts">
import Tooltip from './Tooltip.svelte';
import { tooltips } from '$stores/tooltip';
</script>
