<TooltipShell accent={rarityAccent} glow hidden={!skill} bind:base={container}>
	{#snippet header()}
		<TooltipTitle label="Skill" name={skill?.name ?? ''} diamondColor="var(--accent)" labelColor="var(--accent)">
			{#snippet trailing()}
				<CooldownPill progress={cooldownProgress} ready={isReady} remainingFormatted={remainingCdFormatted} />
			{/snippet}
		</TooltipTitle>
	{/snippet}

	<TooltipSection label="Damage breakdown">
		<DamageBreakdown
			base={baseDamage}
			{multipliers}
			{crit}
			enemyDefense={opponent ? enemyDefense : undefined}
			{total}
		/>
	</TooltipSection>

	<TooltipSection label="Tempo" last={effectLines.length === 0}>
		<TempoMetrics cooldown={adjustedCd} dps={damagePerSecond(total, adjustedCd)} />
	</TooltipSection>

	{#if effectLines.length > 0}
		<TooltipSection label="On hit" last>
			<EffectLines lines={effectLines} />
		</TooltipSection>
	{/if}
</TooltipShell>

<script lang="ts">
import { EAttribute } from '$lib/api';
import { applyDefense, expectedCritMultiplier, scaledEffectAmount, skillContributions, type Skill } from '$lib/battle';
import {
	attributeIsHarmful,
	attributeName,
	damagePerSecond,
	describeEffect,
	formatAttributeValue,
	rarityColor
} from '$lib/common';
import { battleEngine } from '$lib/engine';
import { staticData } from '$stores';
import TooltipShell from '$components/tooltip/TooltipShell.svelte';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';
import TooltipTitle from '$components/tooltip/TooltipTitle.svelte';
import CooldownPill from './skill-tooltip/CooldownPill.svelte';
import DamageBreakdown from './skill-tooltip/DamageBreakdown.svelte';
import EffectLines from './skill-tooltip/EffectLines.svelte';
import TempoMetrics from './skill-tooltip/TempoMetrics.svelte';
import { cooldownReadout } from './skill-cooldown';

export const getBaseNode = () => container;

type Props = {
	skill: Skill | undefined;
};

const { skill }: Props = $props();

// Bound to the shell's root element and relocated into the global tooltip container
// by getBaseNode(); reactive so the relocation runs once the shell has mounted.
let container = $state<HTMLDivElement>();

const opponent = $derived(skill?.owner ? battleEngine.getOpponent(skill.owner) : undefined);

// The skill's rarity tier tints the tooltip accent (display only — the battle never reads rarity).
const rarityAccent = $derived(skill ? rarityColor(skill.rarityId) : 'var(--accent)');

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

// Crit folds into the shown damage as its long-run average: the expected multiplier scales the raw
// damage BEFORE Defense (mirroring the battle, where a crit punches through Defense), then Defense is
// subtracted once. Display-only — the live battle rolls each crit individually.
const critChance = $derived(skill?.owner.attributes.getValue(EAttribute.CriticalChance) ?? 0);
const critDamage = $derived(skill?.owner.attributes.getValue(EAttribute.CriticalDamage) ?? 0);
const critBonus = $derived(totalDamage * (expectedCritMultiplier(critChance, critDamage) - 1));
// Surface the crit row only when a crit can occur — a 0 chance contributes nothing.
const crit = $derived(
	critChance > 0
		? {
				chance: formatAttributeValue(critChance, EAttribute.CriticalChance, staticData.attributes),
				damage: critDamage,
				bonus: critBonus
			}
		: undefined
);
const total = $derived(applyDefense(totalDamage + critBonus, enemyDefense));
const cdMultiplier = $derived(skill?.owner.cdMultiplier ?? 1);
// Pure readout guards a zero/non-positive cooldown (an always-ready skill) against NaN/Infinity.
const cooldown = $derived(cooldownReadout(skill?.cooldownMs ?? 0, skill?.renderChargeTime ?? 0, cdMultiplier));
const adjustedCd = $derived(cooldown.adjustedCd);
const isReady = $derived(cooldown.isReady);
const cooldownProgress = $derived(cooldown.progress);
const remainingCdFormatted = $derived(cooldown.remainingCd.toFixed(2));

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
