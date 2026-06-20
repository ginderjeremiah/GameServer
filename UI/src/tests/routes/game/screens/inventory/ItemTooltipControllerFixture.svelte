<!-- Drives createItemTooltip inside a real component lifecycle (it registers an onDestroy cleanup via
     the tooltip store) so the controller can be exercised in isolation. The reactive `item` render
     state is surfaced through `onHandle` so a test can assert show/move/hide effects. -->
<div></div>

<script lang="ts">
import { createItemTooltip, type ItemTooltipHandle } from '$routes/game/screens/inventory/item-tooltip.svelte';

interface Props {
	onHandle: (handle: ItemTooltipHandle) => void;
}

const { onHandle }: Props = $props();

// The controller never invokes the component getter for show/move/hide (it only drives the store's
// position/visibility), so a minimal stand-in stands in for the rendered tooltip panel.
const handle = createItemTooltip(() => ({ getBaseNode: () => document.createElement('div') }));

// Hand the live handle to the test once; its getters stay reactive so later reads reflect mutations.
// svelte-ignore state_referenced_locally
onHandle(handle);
</script>
