<div></div>

<script lang="ts">
import { registerTooltipComponent } from '$stores/tooltip.svelte';

// Minimal stand-in for a screen component that registers a tooltip on mount
// and relies on onDestroy to unregister it when the screen is torn down.
registerTooltipComponent(() => ({ getBaseNode: () => document.createElement('div') }));
</script>
