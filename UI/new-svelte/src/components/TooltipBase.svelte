{#each tooltips.data as tooltip (tooltip.id)}
	<Tooltip {...tooltip} />
{/each}

<script lang="ts">
import Tooltip from './Tooltip.svelte';
import { tooltips } from '$stores';
</script>
