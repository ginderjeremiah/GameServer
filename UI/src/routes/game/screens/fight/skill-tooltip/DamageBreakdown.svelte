<div class="tt-dmg-list">
	<div class="tt-dmg-row">
		<span class="tt-dmg-label">Base</span>
		<span class="tt-dmg-value">{formatNum(base)}</span>
	</div>
	{#each multipliers as mult (mult.name)}
		<div class="tt-dmg-row">
			<span class="tt-dmg-label">
				<AttributeIcon id={mult.attributeId} size={13} />
				{mult.name}
				<span class="tt-mult-dim">x{mult.multiplier}</span>
			</span>
			<span class="tt-dmg-value positive">+{formatNum(mult.value)}</span>
		</div>
	{/each}
	{#if crit}
		<div class="tt-dmg-row">
			<span class="tt-dmg-label">
				<AttributeIcon id={EAttribute.CriticalChanceMultiplier} size={13} />
				Critical
				<span class="tt-mult-dim">{crit.chance} · ×{formatNum(crit.damage)}</span>
			</span>
			<span class="tt-dmg-value" class:positive={crit.bonus >= 0} class:negative={crit.bonus < 0}>
				{crit.bonus >= 0 ? '+' : '−'}{formatNum(Math.abs(crit.bonus))}
			</span>
		</div>
	{/if}
	{#if mitigated !== undefined}
		<div class="tt-dmg-row">
			<span class="tt-dmg-label">Toughness mitigation</span>
			<span class="tt-dmg-value negative">-{formatNum(mitigated)}</span>
		</div>
	{/if}
	<div class="tt-dmg-total">
		<span>Total</span>
		<span class="tt-total-value">{formatNum(total)}</span>
	</div>
</div>

<script lang="ts">
import { formatNum } from '$lib/common';
import { EAttribute } from '$lib/api';
import AttributeIcon from '$components/AttributeIcon.svelte';

interface DamageMultiplier {
	/** The attribute this contribution scales with. */
	attributeId: EAttribute;
	/** Attribute name. */
	name: string;
	/** The skill's multiplier for the attribute. */
	multiplier: number;
	/** The resolved damage contribution (attribute value × multiplier). */
	value: number;
}

/** The expected contribution from critical hits, folded into the total before mitigation. */
interface CritContribution {
	/** Pre-formatted crit chance (e.g. `5%`). */
	chance: string;
	/** Crit damage as a direct multiplier (e.g. 1.5). */
	damage: number;
	/** The expected damage bonus over many fires (`raw × chance × (damage − 1)`). */
	bonus: number;
}

interface Props {
	base: number;
	multipliers: DamageMultiplier[];
	/** The expected crit contribution, or undefined when no crit can occur (0 chance). */
	crit: CritContribution | undefined;
	/** Damage removed by the defender's Toughness mitigation curve, or undefined when there is no opponent. */
	mitigated: number | undefined;
	total: number;
}

const { base, multipliers, crit, mitigated, total }: Props = $props();
</script>

<style lang="scss">
.tt-dmg-list {
	display: flex;
	flex-direction: column;
	gap: 3px;
}

.tt-dmg-row {
	display: flex;
	justify-content: space-between;
	align-items: baseline;
	font-size: 11.5px;
}

.tt-dmg-label {
	display: inline-flex;
	align-items: center;
	gap: 5px;
	color: var(--text-secondary);
}

.tt-mult-dim {
	color: color-mix(in srgb, var(--text-primary) 45%, transparent);
}

.tt-dmg-value {
	font-family: var(--mono);
	font-size: 11.5px;
	color: var(--text-primary);
	letter-spacing: 0.3px;

	&.positive {
		color: var(--success);
	}
	&.negative {
		color: var(--error);
	}
}

.tt-dmg-total {
	margin-top: 4px;
	padding-top: 6px;
	border-top: 1px solid color-mix(in srgb, var(--text-primary) 10%, transparent);
	display: flex;
	justify-content: space-between;
	align-items: baseline;
	font-size: 13px;
	font-weight: 600;
	color: var(--text-primary);
}

.tt-total-value {
	font-family: var(--mono);
	color: var(--accent);
	letter-spacing: 0.3px;
}
</style>
