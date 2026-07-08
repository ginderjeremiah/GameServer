<!-- Test stub for the 'fight' screen: records a mount in the shared tracker so the page test can
     assert whether `{#key screenNonce}` remounted it (same-screen deep-link handling). -->
<div data-testid="fight-screen"></div>

<script lang="ts">
import { onMount } from 'svelte';
import { mountTracker } from './screen-mount-tracker';

onMount(() => {
	mountTracker.fight++;
});
</script>
