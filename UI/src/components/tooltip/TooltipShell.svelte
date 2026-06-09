<div
	class="tt-shell"
	class:glow
	bind:this={base}
	style:display={hidden ? 'none' : undefined}
	style:--tt-glow={glow ? accent : undefined}
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
	 * Tint the panel's drop shadow with {@link accent} instead of the neutral black glow.
	 * `SkillTooltip` opts in for its accent-tinted glow; the mod/item tooltips leave it
	 * `false` and keep the neutral shadow.
	 */
	glow?: boolean;
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

let { accent, glow = false, hidden = false, base = $bindable(), header, children }: Props = $props();
</script>

<style lang="scss">
.tt-shell {
	width: 280px;
	border-radius: 3px;
	box-shadow: -4px 0 16px color-mix(in srgb, var(--black) 15%, transparent);

	// Opt-in accent-tinted glow (see the `glow` prop); the tint flows from the same
	// `accent` that paints the left border, carried in via the `--tt-glow` custom property.
	&.glow {
		box-shadow: -4px 0 16px color-mix(in srgb, var(--tt-glow) 13%, transparent);
	}
}

.tt-body {
	padding: 12px 16px 14px;
}
</style>
