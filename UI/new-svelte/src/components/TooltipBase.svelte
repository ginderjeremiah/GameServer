{#each tooltips.data as tooltip}
	<Tooltip component={tooltip.component} visible={tooltip.visible} position={tooltip.position} />
{/each}

<script lang="ts">
import Tooltip from './Tooltip.svelte';
import { tooltips } from '$stores/tooltip.svelte';
</script>
