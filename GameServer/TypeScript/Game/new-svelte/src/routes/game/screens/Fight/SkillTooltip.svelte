<div class="skill-tooltip" bind:this="{container}" style="{skill ? '' : 'display: none;'}">
	<div class="tooltip-title">{`${skill?.name} (${remainingCd.toFixed(2)}s)`}</div>
	<div class="tooltip-content">
		<div class="tooltip-header">Damage:</div>
		<ul>
			<li>Base: {baseDamage}</li>
			{#each multipliers as mult}
				<li>
					{$attributes[mult.attributeId].name}: {formatNum(getMultiplier(mult))} ({mult.multiplier}x)
				</li>
			{/each}
			<li>Total: {formatNum(totalDamage)}</li>
			{#if opponent}
				<li>Adjusted Total: {formatNum(adjustedTotal)}</li>
			{/if}
			<li>Cooldown: {formatNum(adjustedCd)}s</li>
			<li>DPS: {formatNum(totalDamage / adjustedCd)}</li>
			{#if opponent}
				<li>Adjusted DPS: {formatNum(adjustedTotal / adjustedCd)}</li>
			{/if}
		</ul>
	</div>
</div>

<script lang="ts">
import { EAttribute, type IAttributeMultiplier } from '$lib/api';
import { Skill } from '$lib/battle';
import { formatNum } from '$lib/common';
import { renderDelta, getOpponent } from '$lib/engine';
import { attributes } from '$stores';

export const getBaseNode = () => container;

export let skill: Skill | undefined;

let container: HTMLDivElement;

$: opponent = skill?.owner ? getOpponent(skill.owner) : undefined;

$: baseDamage = skill?.baseDamage ?? 0;
$: multipliers = skill?.damageMultipliers ?? [];
$: totalDamage = baseDamage + multipliers.reduce((a, b) => a + getMultiplier(b), 0);
$: adjustedTotal = Math.max(
	totalDamage - (opponent?.attributes.getValue(EAttribute.Defense) ?? 0),
	0
);
$: cdMultiplier = skill?.owner.cdMultiplier ?? 1;
$: adjustedCd = (skill?.cooldownMS ?? 0) / 1000 / cdMultiplier;
$: remainingCd =
	adjustedCd - ($renderDelta * cdMultiplier + (skill?.chargeTime ?? 0)) / 1000 / cdMultiplier;

const getMultiplier = (mult: IAttributeMultiplier) => {
	return (skill?.owner.attributes.getValue(mult.attributeId) ?? 0) * mult.multiplier;
};
</script>

<style lang="scss">
.skill-tooltip {
	font-size: 0.75rem;
	min-width: 10rem;
	padding: 0.5rem;

	.tooltip-title {
		font-size: 1.25rem;
		margin-bottom: 0.5rem;
		text-align: center;
	}

	.tooltip-header {
		font-size: 1rem;
		margin-bottom: 0.5rem;
	}

	ul {
		margin: 0;
		padding-left: 1rem;
		list-style: none;
	}
}
</style>
