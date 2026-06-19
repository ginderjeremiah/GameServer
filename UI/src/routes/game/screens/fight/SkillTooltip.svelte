<TooltipShell accent="var(--accent)" glow hidden={!skill} bind:base={container}>
	{#snippet header()}
		<TooltipTitle label="Skill" name={skill?.name ?? ''} diamondColor="var(--accent)" labelColor="var(--accent)">
			{#snippet trailing()}
				<CooldownPill progress={cooldownProgress} ready={isReady} remainingFormatted={remainingCdFormatted} />
			{/snippet}
		</TooltipTitle>
	{/snippet}

	<TooltipSection label="Damage breakdown">
		<DamageBreakdown base={baseDamage} {multipliers} enemyDefense={opponent ? enemyDefense : undefined} {total} />
	</TooltipSection>

	<TooltipSection label="Tempo" last={effectLines.length === 0}>
		<TempoMetrics cooldown={adjustedCd} dps={total / adjustedCd} />
	</TooltipSection>

	{#if effectLines.length > 0}
		<TooltipSection label="On hit" last>
			<EffectLines lines={effectLines} />
		</TooltipSection>
	{/if}
</TooltipShell>

<script lang="ts">
import { EAttribute } from '$lib/api';
import { applyDefense, scaledEffectAmount, skillContributions, type Skill } from '$lib/battle';
import { attributeIsHarmful, attributeName, describeEffect } from '$lib/common';
import { battleEngine } from '$lib/engine';
import { staticData } from '$stores';
import TooltipShell from '$components/tooltip/TooltipShell.svelte';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';
import TooltipTitle from '$components/tooltip/TooltipTitle.svelte';
import CooldownPill from './skill-tooltip/CooldownPill.svelte';
import DamageBreakdown from './skill-tooltip/DamageBreakdown.svelte';
import EffectLines from './skill-tooltip/EffectLines.svelte';
import TempoMetrics from './skill-tooltip/TempoMetrics.svelte';

export const getBaseNode = () => container;

type Props = {
	skill: Skill | undefined;
};

const { skill }: Props = $props();

// Bound to the shell's root element and relocated into the global tooltip container
// by getBaseNode(); reactive so the relocation runs once the shell has mounted.
let container = $state<HTMLDivElement>();

const opponent = $derived(skill?.owner ? battleEngine.getOpponent(skill.owner) : undefined);

const baseDamage = $derived(skill?.baseDamage ?? 0);
const multipliers = $derived(
	(skill ? skillContributions(skill, skill.owner.attributes) : []).map((contribution) => ({
		attributeId: contribution.attributeId,
		name: attributeName(contribution.attributeId, staticData.attributes),
		multiplier: contribution.multiplier,
		value: contribution.value
	}))
);
const totalDamage = $derived(skill?.calculateDamage() ?? 0);
const enemyDefense = $derived(opponent?.attributes.getValue(EAttribute.Defense) ?? 0);
const total = $derived(applyDefense(totalDamage, enemyDefense));
const cdMultiplier = $derived(skill?.owner.cdMultiplier ?? 1);
const adjustedCd = $derived((skill?.cooldownMs ?? 0) / 1000 / cdMultiplier);
const remainingCd = $derived(Math.max(adjustedCd - (skill?.renderChargeTime ?? 0) / 1000 / cdMultiplier, 0));
const isReady = $derived(remainingCd <= 0.01);
const cooldownProgress = $derived(isReady ? 100 : Math.max(0, ((adjustedCd - remainingCd) / adjustedCd) * 100));
const remainingCdFormatted = $derived(remainingCd.toFixed(2));

// Show each effect's magnitude resolved against the caster's (owner's) attributes, so a scaling effect
// reads like the damage total does — the number the skill would actually apply right now.
const effectLines = $derived(
	(skill?.effects ?? []).map((effect) =>
		describeEffect(
			effect,
			attributeName(effect.attributeId, staticData.attributes),
			attributeIsHarmful(effect.attributeId, staticData.attributes),
			skill ? scaledEffectAmount(effect, skill.owner.attributes) : effect.amount
		)
	)
);
</script>
