<!-- The small uppercase mono eyebrow used by the boss affordances ("Zone Boss",
     "Boss Fight", "Zone Cleared"). -->
<span class="boss-kicker" style:--kicker-size="{size}px">{@render children()}</span>

<script lang="ts">
import type { Snippet } from 'svelte';

type Props = {
	children: Snippet;
	/** Font size in px. */
	size?: number;
};

const { children, size = 9.5 }: Props = $props();
</script>

<style lang="scss">
.boss-kicker {
	font-family: var(--mono);
	font-size: var(--kicker-size);
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--boss-accent);
	white-space: nowrap;
}
</style>
