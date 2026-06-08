<div class="mod-tooltip" style:border-left="3px solid {rarityColor(mod.rarityId)}">
	<div class="tt-title-section">
		<div class="tt-type-row">
			<div
				class="tt-type-diamond"
				style:background={typeColor}
				style:box-shadow="0 0 6px {tintColor(typeColor, 0.67)}"
			></div>
			<span class="tt-type-label" style:color={typeColor}>{modTypeLabel(mod.itemModTypeId)}</span>
		</div>
		<div class="tt-mod-name">{mod.name}</div>
	</div>

	<div class="tt-body">
		{#if effects.length}
			<TooltipSection label="Effects" last={!mod.description}>
				<div class="tt-stats-grid">
					{#each effects as effect (effect.name)}
						<div class="tt-stat-name">{effect.name}</div>
						<div class="tt-stat-value" class:positive={effect.value > 0} class:negative={effect.value < 0}>
							{effect.value > 0 ? '+' : ''}{effect.value}
						</div>
					{/each}
				</div>
			</TooltipSection>
		{/if}

		{#if mod.description}
			<TooltipSection label="Description" last>
				<div class="tt-description">{mod.description}</div>
			</TooltipSection>
		{/if}
	</div>
</div>

<script lang="ts">
import { EAttribute, type IItemMod } from '$lib/api';
import { modTypeColor, modTypeLabel, normalizeText, rarityColor, tintColor } from '$lib/common';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';

interface Props {
	mod: IItemMod;
}

const { mod }: Props = $props();

const typeColor = $derived(modTypeColor(mod.itemModTypeId));
const effects = $derived(
	(mod.attributes ?? []).map((a) => ({ name: normalizeText(EAttribute[a.attributeId]), value: a.amount }))
);
</script>

<style lang="scss">
.mod-tooltip {
	width: 280px;
	border-radius: 3px;
	box-shadow: -4px 0 16px color-mix(in srgb, var(--black) 15%, transparent);
}

.tt-title-section {
	padding: 14px 16px 12px;
	border-bottom: 1px solid color-mix(in srgb, var(--text-primary) 8%, transparent);
}

.tt-type-row {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 4px;
}

.tt-type-diamond {
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
}

.tt-type-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
}

.tt-mod-name {
	font-size: 18px;
	font-weight: 400;
	color: var(--text-primary);
	letter-spacing: -0.2px;
	line-height: 1.15;
}

.tt-body {
	padding: 12px 16px 14px;
}

.tt-stats-grid {
	display: grid;
	grid-template-columns: 1fr auto;
	row-gap: 4px;
	column-gap: 12px;

	.tt-stat-name {
		font-size: 12px;
		color: var(--text-secondary);
	}

	.tt-stat-value {
		font-family: var(--mono);
		font-size: 11.5px;
		letter-spacing: 0.3px;
		text-align: right;
		color: color-mix(in srgb, var(--text-primary) 70%, transparent);

		&.positive {
			color: var(--success);
		}
		&.negative {
			color: var(--error);
		}
	}
}

.tt-description {
	font-size: 11.5px;
	font-style: italic;
	color: color-mix(in srgb, var(--text-primary) 60%, transparent);
	line-height: 1.55;
}
</style>
