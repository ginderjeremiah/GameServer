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
				{#if masked}
					<!-- The sealed badge carries the rarity teaser, distinct from the (still-visible) category hue. -->
					<div
						class="sealed-badge"
						style:background={tintColor(badgeAccent, 0.1)}
						style:border="1px solid {tintColor(badgeAccent, 0.4)}"
					>
						<svg width="9" height="9" viewBox="0 0 16 16" fill="none" stroke={badgeAccent} stroke-width="1.6">
							<rect x="3.5" y="7" width="9" height="6.5" rx="1" />
							<path d="M5.5 7V5.2a2.5 2.5 0 0 1 5 0V7" />
						</svg>
						<span style:color={badgeAccent}>Sealed</span>
					</div>
				{:else}
					{@render trailing?.()}
				{/if}
			</div>
			<div class="tt-title-name" class:masked>{masked ? '?????????' : name}</div>
		</div>
	</div>
</div>

<script lang="ts">
import type { Snippet } from 'svelte';
import { tintColor } from '$lib/common';

interface Props {
	/** Category/type label shown beside the diamond (e.g. "Skill", "Weapon"). */
	label: string;
	/** The tooltip's headline name. Replaced by a masked placeholder when `masked`. */
	name: string;
	/** Colour of the diamond glyph (and its glow). */
	diamondColor: string;
	/** Colour of the category/type label text. */
	labelColor: string;
	/** Optional trailing content on the category row (e.g. a cooldown pill or equipped badge). Suppressed when `masked`. */
	trailing?: Snippet;
	/** Optional leading content to the left of the title block (e.g. an attribute icon). */
	leading?: Snippet;
	/** Render the name as a masked placeholder and show the SEALED badge (sealed/teaser tooltips). */
	masked?: boolean;
	/** Accent hue for the SEALED badge (e.g. the rarity hue); defaults to {@link labelColor}. Only used when `masked`. */
	sealedAccent?: string;
}

const { label, name, diamondColor, labelColor, trailing, leading, masked = false, sealedAccent }: Props = $props();

const badgeAccent = $derived(sealedAccent ?? labelColor);
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

.sealed-badge {
	margin-left: auto;
	display: flex;
	align-items: center;
	gap: 5px;
	padding: 2px 8px;
	border-radius: 2px;

	span {
		font-family: var(--mono);
		font-size: 9px;
		letter-spacing: 1.2px;
		text-transform: uppercase;
	}
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
