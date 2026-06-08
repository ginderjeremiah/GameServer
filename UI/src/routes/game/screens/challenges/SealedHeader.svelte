<!-- Diamond + category label stay colour-matched to the (still-visible) category, while the
	sealed badge + masked name carry the rarity teaser — composed over the shared TooltipTitle. -->
<TooltipTitle label={typeLabel} name="?????????" diamondColor={catAccent} labelColor={catAccent} masked>
	{#snippet trailing()}
		<div
			class="sealed-badge"
			style:background={tintColor(rarityAccent, 0.1)}
			style:border="1px solid {tintColor(rarityAccent, 0.4)}"
		>
			<svg width="9" height="9" viewBox="0 0 16 16" fill="none" stroke={rarityAccent} stroke-width="1.6">
				<rect x="3.5" y="7" width="9" height="6.5" rx="1" />
				<path d="M5.5 7V5.2a2.5 2.5 0 0 1 5 0V7" />
			</svg>
			<span style:color={rarityAccent}>Sealed</span>
		</div>
	{/snippet}
</TooltipTitle>

<script lang="ts">
import { tintColor } from '$lib/common';
import TooltipTitle from '$components/tooltip/TooltipTitle.svelte';

interface Props {
	/** Rarity hue — drives the SEALED badge and the tooltip's left border. */
	rarityAccent: string;
	/** Category/mod-type hue — drives the diamond + category label. */
	catAccent: string;
	/** Visible category/mod-type label (e.g. "Helm", "Prefix"). */
	typeLabel: string;
}

const { rarityAccent, catAccent, typeLabel }: Props = $props();
</script>

<style lang="scss">
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
</style>
