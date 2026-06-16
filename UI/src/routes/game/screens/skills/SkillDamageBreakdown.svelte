<div class="d-sec">
	<div class="label">Damage breakdown</div>
	{#if metrics.contributions.length}
		{#each metrics.contributions as contribution (contribution.attributeId)}
			<div class="scale" style:--ac={attributeColor(contribution.attributeId)}>
				<AttributeChip attributeId={contribution.attributeId} wide />
				<div class="bar"><i style:width="{Math.round((contribution.value / maxContribution) * 100)}%"></i></div>
				<span class="contrib"
					>{attributeName(contribution.attributeId, staticData.attributes)} ×{contribution.multiplier} = +{fmt(
						contribution.value
					)}</span
				>
			</div>
		{/each}
	{:else}
		<div class="d-desc">No attribute scaling.</div>
	{/if}
	<div class="brk-line def">
		<span class="brk-k">Enemy defense</span><span class="brk-v">−{fmt(view.appliedDefense(metrics.skill.id))}</span>
	</div>
	<div class="brk-line total">
		<span class="brk-k">Effective hit</span><span class="brk-v">{fmt(view.effective(metrics.skill.id))}</span>
	</div>
</div>

<script lang="ts">
import { attributeColor, attributeName, formatNum } from '$lib/common';
import { staticData } from '$stores';
import AttributeChip from '$components/AttributeChip.svelte';
import type { SkillMetrics, SkillsView } from './skills-view.svelte';

type Props = {
	view: SkillsView;
	metrics: SkillMetrics;
};

const { view, metrics }: Props = $props();

const fmt = (n: number) => formatNum(Math.round(n));

const maxContribution = $derived(Math.max(metrics.skill.baseDamage, ...metrics.contributions.map((c) => c.value), 1));
</script>

<style lang="scss">
.d-sec {
	margin-top: 16px;
}

.label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.d-desc {
	margin-top: 3px;
	font-size: 13.5px;
	color: var(--text-tertiary);
}

.scale {
	display: flex;
	align-items: center;
	gap: 11px;
	margin-top: 8px;
}

.bar {
	flex: 1;
	height: 6px;
	border-radius: 4px;
	background: color-mix(in srgb, var(--white) 7%, transparent);
	overflow: hidden;

	i {
		display: block;
		height: 100%;
		border-radius: 4px;
		background: var(--ac);
	}
}

.contrib {
	min-width: 128px;
	font-family: var(--mono);
	font-size: 9.5px;
	color: var(--text-tertiary);
	white-space: nowrap;
}

.brk-line {
	display: flex;
	justify-content: space-between;
	align-items: baseline;
	padding-top: 7px;
	font-size: 12px;

	.brk-k {
		color: var(--text-secondary);
	}

	.brk-v {
		font-family: var(--mono);
		font-size: 11.5px;
	}

	&.def {
		.brk-k {
			color: var(--enemy-accent);
		}

		.brk-v {
			color: var(--error);
		}
	}

	&.total {
		margin-top: 6px;
		padding-top: 8px;
		border-top: 1px solid color-mix(in srgb, var(--text-primary) 10%, transparent);
		font-weight: 600;

		.brk-v {
			color: var(--accent);
			font-size: 13px;
		}
	}
}
</style>
