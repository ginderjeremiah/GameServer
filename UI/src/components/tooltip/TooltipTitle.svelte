<div class="tt-title-section">
	<div class="tt-title-layout">
		{#if leading}
			<div class="tt-title-leading">{@render leading()}</div>
		{/if}
		<div class="tt-title-main">
			<div class="tt-category-row">
				<div
					class="tt-category-diamond"
					style:background={diamondColor}
					style:box-shadow="0 0 6px {tintColor(diamondColor, 0.67)}"
				></div>
				<span class="tt-category-label" style:color={labelColor}>{label}</span>
				{@render trailing?.()}
			</div>
			<div class="tt-title-name" class:masked>{name}</div>
		</div>
	</div>
</div>

<script lang="ts">
import type { Snippet } from 'svelte';
import { tintColor } from '$lib/common';

interface Props {
	/** Category/type label shown beside the diamond (e.g. "Skill", "Weapon"). */
	label: string;
	/** The tooltip's headline name. */
	name: string;
	/** Colour of the diamond glyph (and its glow). */
	diamondColor: string;
	/** Colour of the category/type label text. */
	labelColor: string;
	/** Optional trailing content on the category row (e.g. a cooldown pill or equipped badge). */
	trailing?: Snippet;
	/** Optional leading content to the left of the title block (e.g. an attribute icon). */
	leading?: Snippet;
	/** Render the name as a dimmed, wide-tracked placeholder (sealed/teaser tooltips). */
	masked?: boolean;
}

const { label, name, diamondColor, labelColor, trailing, leading, masked = false }: Props = $props();
</script>

<style lang="scss">
.tt-title-section {
	padding: 14px 16px 12px;
	border-bottom: 1px solid color-mix(in srgb, var(--text-primary) 8%, transparent);
}

// Wrapper holding an optional leading element (e.g. an icon) beside the title block. With no
// leading element the single main child fills the width, so titles without it are unchanged.
.tt-title-layout {
	display: flex;
	align-items: center;
	gap: 11px;
}

.tt-title-leading {
	flex-shrink: 0;
	display: flex;
}

.tt-title-main {
	flex: 1;
	min-width: 0;
}

.tt-category-row {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 4px;
}

.tt-category-diamond {
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
	flex-shrink: 0;
}

.tt-category-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
}

.tt-title-name {
	font-size: 18px;
	font-weight: 400;
	color: var(--text-primary);
	letter-spacing: -0.2px;
	line-height: 1.15;

	&.masked {
		color: color-mix(in srgb, var(--text-primary) 45%, transparent);
		letter-spacing: 2px;
	}
}
</style>
