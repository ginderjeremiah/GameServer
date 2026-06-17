<!-- Registers a tooltip on mount and surfaces the returned describedById, so a test can assert it
     matches the registered entry's container id (the basis of the aria-describedby association). -->
<span data-testid="described-by-id">{describedById}</span>

<script lang="ts">
import { registerTooltipComponent } from '$stores/tooltip.svelte';

const { describedById } = registerTooltipComponent(() => ({ getBaseNode: () => document.createElement('div') }));
</script>
