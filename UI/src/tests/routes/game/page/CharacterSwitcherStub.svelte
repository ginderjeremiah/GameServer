<!-- Test stub for CharacterSwitcher: exposes `open` as a data attribute and a close button wired to
     the real `onClose` callback, so the page test can assert the 'switch' nav action both opens and
     closes it, without the real overlay/session-takeover machinery. -->
<div data-testid="character-switcher" data-open={open}>
	<button data-testid="character-switcher-close" onclick={onClose}>Close</button>
</div>

<script lang="ts">
const { open, onClose }: { open: boolean; onClose: () => void } = $props();
</script>
