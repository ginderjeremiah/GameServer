<div class="sealed-tooltip" style:border-left="3px solid {accent}">
	<SealedHeader
		rarityAccent={accent}
		catAccent={modTypeColor(mod.itemModTypeId)}
		typeLabel={modTypeLabel(mod.itemModTypeId)}
	/>

	<div class="tt-body">
		{#if effectCount}
			<TooltipSection label="Effects">
				<MaskedStatsGrid {accent} rows={effectCount} barWidths={EFFECT_BAR_WIDTHS} />
			</TooltipSection>
		{/if}

		<TooltipSection label="Description" last>
			<MaskedDescription {accent} lineWidths={[236, 170]} />
		</TooltipSection>
	</div>
</div>

<script lang="ts">
import type { IItemMod } from '$lib/api';
import { modTypeColor, modTypeLabel, rarityColor } from '$lib/common';
import SealedHeader from './SealedHeader.svelte';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';
import MaskedStatsGrid from '$components/tooltip/MaskedStatsGrid.svelte';
import MaskedDescription from '$components/tooltip/MaskedDescription.svelte';

interface Props {
	mod: IItemMod;
}

const { mod }: Props = $props();

const accent = $derived(rarityColor(mod.rarityId));
// One masked row per effect, so the *count* still reads true.
const effectCount = $derived((mod.attributes ?? []).length);

const EFFECT_BAR_WIDTHS = [70, 88, 58];
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
</style>
