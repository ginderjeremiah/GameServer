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
	{#if enemyDefense !== undefined}
		<div class="tt-dmg-row">
			<span class="tt-dmg-label">Enemy defense</span>
			<span class="tt-dmg-value negative">-{formatNum(enemyDefense)}</span>
		</div>
	{/if}
	<div class="tt-dmg-total">
		<span>Total</span>
		<span class="tt-total-value">{formatNum(total)}</span>
	</div>
</div>

<script lang="ts">
import { formatNum } from '$lib/common';
import type { EAttribute } from '$lib/api';
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

interface Props {
	base: number;
	multipliers: DamageMultiplier[];
	/** Enemy defense subtracted from the total, or undefined when there is no opponent. */
	enemyDefense: number | undefined;
	total: number;
}

const { base, multipliers, enemyDefense, total }: Props = $props();
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
