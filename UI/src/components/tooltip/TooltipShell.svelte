<div
	class="tt-shell"
	bind:this={base}
	style:display={hidden ? 'none' : undefined}
	style:border-left="3px solid {accent}"
>
	{#if !hidden}
		{@render header?.()}
		<div class="tt-body">
			{@render children?.()}
		</div>
	{/if}
</div>

<script lang="ts">
import type { Snippet } from 'svelte';

interface Props {
	/** Rarity/teaser accent painted as the panel's 3px left border. */
	accent: string;
	/**
	 * Render the panel hidden and empty. The inventory `ItemTooltip` keeps a single
	 * instance mounted so the global tooltip can anchor to it, but shows nothing until an
	 * item is hovered; the sealed/mod tooltips always carry data and leave this `false`.
	 */
	hidden?: boolean;
	/**
	 * Bound to the panel's root element, so the `ItemTooltip` can forward it from
	 * `getBaseNode()` and let the global tooltip container relocate/position the panel.
	 */
	base?: HTMLDivElement;
	/** Header content rendered flush above the padded body (e.g. a title or sealed header). */
	header?: Snippet;
	/** Body content, rendered inside the padded `.tt-body` region. */
	children?: Snippet;
}

let { accent, hidden = false, base = $bindable(), header, children }: Props = $props();
</script>

<style lang="scss">
.tt-shell {
	width: 280px;
	border-radius: 3px;
	box-shadow: -4px 0 16px color-mix(in srgb, var(--black) 15%, transparent);
}

.tt-body {
	padding: 12px 16px 14px;
}
</style>
