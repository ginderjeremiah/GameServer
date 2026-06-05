<div class="sealed-tooltip" style:border-left="3px solid {accent}">
	<SealedHeader rarityAccent={accent} catAccent={itemCategoryColor(item.itemCategoryId)} typeLabel={categoryName} />

	<div class="tt-body">
		{#if statRows.length}
			<TooltipSection label="Stats">
				<div class="masked-grid">
					{#each statRows as i (i)}
						<MaskBar {accent} width={STAT_BAR_WIDTHS[i % STAT_BAR_WIDTHS.length]} />
						<div class="qmark-cell"><span class="qmark" style:color={tintColor(accent, 0.7)}>???</span></div>
					{/each}
				</div>
			</TooltipSection>
		{/if}

		{#if item.modSlots.length}
			<TooltipSection label="Mods · 0/{item.modSlots.length}">
				<div class="sealed-slots">
					{#each item.modSlots as slot (slot.id)}
						<div class="sealed-slot" style:border-left="2px solid {tintColor(accent, 0.5)}">
							<svg
								width="10"
								height="10"
								viewBox="0 0 16 16"
								fill="none"
								stroke="rgba(240,240,240,0.4)"
								stroke-width="1.5"
							>
								<rect x="3.5" y="7" width="9" height="6.5" rx="1" />
								<path d="M5.5 7V5.2a2.5 2.5 0 0 1 5 0V7" />
							</svg>
							<span class="sealed-slot-label">Sealed slot</span>
						</div>
					{/each}
				</div>
			</TooltipSection>
		{/if}

		<TooltipSection label="Description" last>
			<div class="masked-desc">
				<MaskBar {accent} width={236} height={7} />
				<MaskBar {accent} width={188} height={7} />
			</div>
		</TooltipSection>
	</div>
</div>

<script lang="ts">
import type { Item } from '$lib/battle';
import { itemCategoryColor, itemCategoryName, rarityColor, tintColor } from '$lib/common';
import MaskBar from './MaskBar.svelte';
import SealedHeader from './SealedHeader.svelte';
import TooltipSection from './TooltipSection.svelte';

interface Props {
	item: Item;
}

const { item }: Props = $props();

const accent = $derived(rarityColor(item.rarityId));
const categoryName = $derived(itemCategoryName(item.itemCategoryId));
// One masked row per non-zero stat bonus, so the *count* still reads true.
const statRows = $derived(item.totalAttributes.getAttributeMap().map((_, i) => i));

const STAT_BAR_WIDTHS = [78, 60, 92, 70];
</script>

<style lang="scss">
.sealed-tooltip {
	width: 280px;
	border-radius: 3px;
	box-shadow: -4px 0 16px rgba(0, 0, 0, 0.15);
}

.tt-body {
	padding: 12px 16px 14px;
}

.masked-grid {
	display: grid;
	grid-template-columns: 1fr auto;
	row-gap: 6px;
	column-gap: 12px;
	align-items: center;
}

.qmark-cell {
	text-align: right;
}

.qmark {
	font-family: var(--mono);
	font-size: 11.5px;
	letter-spacing: 1px;
}

.sealed-slots {
	display: flex;
	flex-direction: column;
	gap: 6px;
}

.sealed-slot {
	padding: 6px 10px;
	border: 1px dashed rgba(255, 255, 255, 0.14);
	display: flex;
	align-items: center;
	gap: 8px;
}

.sealed-slot-label {
	font-size: 11.5px;
	color: rgba(240, 240, 240, 0.45);
	font-style: italic;
}

.masked-desc {
	display: flex;
	flex-direction: column;
	gap: 4px;
}
</style>
