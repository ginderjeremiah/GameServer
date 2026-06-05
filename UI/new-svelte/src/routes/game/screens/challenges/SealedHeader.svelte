<div class="sealed-head">
	<div class="head-top">
		<!-- Diamond + category label are colour-matched to the (still-visible) category. -->
		<div class="cat-diamond" style:background={catAccent} style:box-shadow="0 0 6px {tintColor(catAccent, 0.67)}"></div>
		<div class="cat-label" style:color={catAccent}>{typeLabel}</div>
		<!-- Sealed badge + border accent carry the rarity tier (the teaser). -->
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
	</div>
	<div class="masked-name">?????????</div>
</div>

<script lang="ts">
import { tintColor } from '$lib/common';

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
.sealed-head {
	padding: 14px 16px 12px;
	border-bottom: 1px solid rgba(240, 240, 240, 0.08);
}

.head-top {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 5px;
}

.cat-diamond {
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
}

.cat-label {
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

.masked-name {
	font-size: 18px;
	font-weight: 400;
	color: rgba(240, 240, 240, 0.45);
	letter-spacing: 2px;
}
</style>
