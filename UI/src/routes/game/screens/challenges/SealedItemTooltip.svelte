<div class="sealed-tooltip" style:border-left="3px solid {accent}">
	<SealedHeader rarityAccent={accent} catAccent={itemCategoryColor(item.itemCategoryId)} typeLabel={categoryName} />

	<div class="tt-body">
		{#if statCount}
			<TooltipSection label="Stats">
				<MaskedStatsGrid {accent} rows={statCount} barWidths={STAT_BAR_WIDTHS} />
			</TooltipSection>
		{/if}

		{#if item.modSlots.length}
			<TooltipSection label="Mods · 0/{item.modSlots.length}">
				<div class="sealed-slots">
					{#each item.modSlots as slot (slot.id)}
						<div class="sealed-slot" style:border-left="2px solid {tintColor(accent, 0.5)}">
							<svg width="10" height="10" viewBox="0 0 16 16" fill="none" stroke="var(--text-muted)" stroke-width="1.5">
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
			<MaskedDescription {accent} lineWidths={[236, 188]} />
		</TooltipSection>
	</div>
</div>

<script lang="ts">
import type { Item } from '$lib/battle';
import { itemCategoryColor, itemCategoryName, rarityColor, tintColor } from '$lib/common';
import SealedHeader from './SealedHeader.svelte';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';
import MaskedStatsGrid from '$components/tooltip/MaskedStatsGrid.svelte';
import MaskedDescription from '$components/tooltip/MaskedDescription.svelte';

interface Props {
	item: Item;
}

const { item }: Props = $props();

const accent = $derived(rarityColor(item.rarityId));
const categoryName = $derived(itemCategoryName(item.itemCategoryId));
// One masked row per non-zero stat bonus, so the *count* still reads true.
const statCount = $derived(item.totalAttributes.getAttributeMap().length);

const STAT_BAR_WIDTHS = [78, 60, 92, 70];
</script>

<style lang="scss">
.sealed-tooltip {
	width: 280px;
	border-radius: 3px;
	box-shadow: -4px 0 16px color-mix(in srgb, var(--black) 15%, transparent);
}

.tt-body {
	padding: 12px 16px 14px;
}

.sealed-slots {
	display: flex;
	flex-direction: column;
	gap: 6px;
}

.sealed-slot {
	padding: 6px 10px;
	border: 1px dashed var(--border-light);
	display: flex;
	align-items: center;
	gap: 8px;
}

.sealed-slot-label {
	font-size: 11.5px;
	color: color-mix(in srgb, var(--text-primary) 45%, transparent);
	font-style: italic;
}
</style>
