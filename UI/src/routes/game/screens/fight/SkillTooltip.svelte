<div
	class="skill-tooltip"
	bind:this={container}
	style={skill ? '' : 'display: none;'}
	style:border-left="3px solid var(--accent)"
>
	{#if skill}
		<TooltipTitle label="Skill" name={skill.name} diamondColor="var(--accent)" labelColor="var(--accent)">
			{#snippet trailing()}
				<CooldownPill progress={cooldownProgress} ready={isReady} remainingFormatted={remainingCdFormatted} />
			{/snippet}
		</TooltipTitle>

		<div class="tt-body">
			<TooltipSection label="Damage breakdown">
				<DamageBreakdown base={baseDamage} {multipliers} enemyDefense={opponent ? enemyDefense : undefined} {total} />
			</TooltipSection>

			<TooltipSection label="Tempo" last>
				<TempoMetrics cooldown={adjustedCd} dps={total / adjustedCd} />
			</TooltipSection>
		</div>
	{/if}
</div>

<script lang="ts">
import { EAttribute, type IAttributeMultiplier } from '$lib/api';
import { type Skill } from '$lib/battle';
import { battleEngine } from '$lib/engine';
import { staticData } from '$stores';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';
import TooltipTitle from '$components/tooltip/TooltipTitle.svelte';
import CooldownPill from './skill-tooltip/CooldownPill.svelte';
import DamageBreakdown from './skill-tooltip/DamageBreakdown.svelte';
import TempoMetrics from './skill-tooltip/TempoMetrics.svelte';

export const getBaseNode = () => container;

type Props = {
	skill: Skill | undefined;
};

const { skill }: Props = $props();

let container: HTMLDivElement;

const opponent = $derived(skill?.owner ? battleEngine.getOpponent(skill.owner) : undefined);

const baseDamage = $derived(skill?.baseDamage ?? 0);
const multipliers = $derived(
	(skill?.damageMultipliers ?? []).map((mult) => ({
		name: attributeName(mult.attributeId),
		multiplier: mult.multiplier,
		value: getMultiplier(mult)
	}))
);
const totalDamage = $derived(baseDamage + multipliers.reduce((a, b) => a + b.value, 0));
const enemyDefense = $derived(opponent?.attributes.getValue(EAttribute.Defense) ?? 0);
const total = $derived(Math.max(totalDamage - enemyDefense, 0));
const cdMultiplier = $derived(skill?.owner.cdMultiplier ?? 1);
const adjustedCd = $derived((skill?.cooldownMs ?? 0) / 1000 / cdMultiplier);
const remainingCd = $derived(Math.max(adjustedCd - (skill?.renderChargeTime ?? 0) / 1000 / cdMultiplier, 0));
const isReady = $derived(remainingCd <= 0.01);
const cooldownProgress = $derived(isReady ? 100 : Math.max(0, ((adjustedCd - remainingCd) / adjustedCd) * 100));
const remainingCdFormatted = $derived(remainingCd.toFixed(2));

const getMultiplier = (mult: IAttributeMultiplier) => {
	return (skill?.owner.attributes.getValue(mult.attributeId) ?? 0) * mult.multiplier;
};

const attributeName = (attrId: number) => {
	return staticData.attributes?.[attrId]?.name ?? EAttribute[attrId] ?? 'Unknown';
};
</script>

<style lang="scss">
.skill-tooltip {
	width: 280px;
	border-radius: 3px;
	box-shadow: -4px 0 16px color-mix(in srgb, var(--accent) 13%, transparent);
}

.tt-body {
	padding: 12px 16px 14px;
}
</style>
