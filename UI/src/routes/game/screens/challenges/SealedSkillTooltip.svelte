<TooltipShell {accent}>
	{#snippet header()}
		<SealedHeader rarityAccent={accent} catAccent="var(--accent)" typeLabel="Skill" />
	{/snippet}

	{#if skill.damageMultipliers.length}
		<TooltipSection label="Scaling">
			<MaskedStatsGrid {accent} rows={skill.damageMultipliers.length} barWidths={SCALING_BAR_WIDTHS} />
		</TooltipSection>
	{/if}

	{#if skill.effects.length}
		<TooltipSection label="On hit">
			<MaskedStatsGrid {accent} rows={skill.effects.length} barWidths={EFFECT_BAR_WIDTHS} />
		</TooltipSection>
	{/if}

	<TooltipSection label="Description" last>
		<MaskedDescription {accent} lineWidths={[236, 180]} />
	</TooltipSection>
</TooltipShell>

<script lang="ts">
import type { ISkill } from '$lib/api';
import SealedHeader from './SealedHeader.svelte';
import TooltipShell from '$components/tooltip/TooltipShell.svelte';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';
import MaskedStatsGrid from '$components/tooltip/MaskedStatsGrid.svelte';
import MaskedDescription from '$components/tooltip/MaskedDescription.svelte';

interface Props {
	skill: ISkill;
}

const { skill }: Props = $props();

// Skills have no rarity tier, so the teaser uses the neutral skill accent throughout.
const accent = 'var(--accent-light)';

const SCALING_BAR_WIDTHS = [82, 64, 92];
const EFFECT_BAR_WIDTHS = [70, 88, 58];
</script>
