<div class="sealed-tooltip" style:border-left="3px solid {accent}">
	<SealedHeader
		rarityAccent={accent}
		catAccent={modTypeColor(mod.itemModTypeId)}
		typeLabel={modTypeLabel(mod.itemModTypeId)}
	/>

	<div class="tt-body">
		{#if effectRows.length}
			<TooltipSection label="Effects">
				<div class="masked-grid">
					{#each effectRows as i (i)}
						<MaskBar {accent} width={EFFECT_BAR_WIDTHS[i % EFFECT_BAR_WIDTHS.length]} />
						<div class="qmark-cell"><span class="qmark" style:color={tintColor(accent, 0.7)}>???</span></div>
					{/each}
				</div>
			</TooltipSection>
		{/if}

		<TooltipSection label="Description" last>
			<div class="masked-desc">
				<MaskBar {accent} width={236} height={7} />
				<MaskBar {accent} width={170} height={7} />
			</div>
		</TooltipSection>
	</div>
</div>

<script lang="ts">
import type { IItemMod } from '$lib/api';
import { modTypeColor, modTypeLabel, rarityColor, tintColor } from '$lib/common';
import MaskBar from './MaskBar.svelte';
import SealedHeader from './SealedHeader.svelte';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';

interface Props {
	mod: IItemMod;
}

const { mod }: Props = $props();

const accent = $derived(rarityColor(mod.rarityId));
// One masked row per effect, so the *count* still reads true.
const effectRows = $derived((mod.attributes ?? []).map((_, i) => i));

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

.masked-desc {
	display: flex;
	flex-direction: column;
	gap: 4px;
}
</style>
