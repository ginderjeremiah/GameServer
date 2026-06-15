<div class="d-sec">
	<div class="label">Damage breakdown</div>
	{#if metrics.contributions.length}
		{#each metrics.contributions as contribution (contribution.attributeId)}
			<div class="scale" style:--ac={attributeColor(contribution.attributeId)}>
				<!-- svelte-ignore a11y_no_static_element_interactions -->
				<span
					class="achip"
					onmouseenter={(e) => tip.controller.show(contribution.attributeId, e)}
					onmousemove={(e) => tip.controller.move(e)}
					onmouseleave={() => tip.controller.hide()}
				>
					<AttributeIcon id={contribution.attributeId} size={12} />
					{attributeCode(contribution.attributeId, staticData.attributes)}
				</span>
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

<!-- One shared tooltip for the scaling chips, anchored to whichever chip is hovered. -->
<AttributeTooltip bind:this={tooltip} attributeId={tip.attributeId} />

<script lang="ts">
import { attributeCode, attributeColor, attributeName, formatNum } from '$lib/common';
import { staticData, type TooltipComponent } from '$stores';
import AttributeIcon from '$components/AttributeIcon.svelte';
import AttributeTooltip from '$components/tooltip/AttributeTooltip.svelte';
import { createAttributeTooltip } from '$components/tooltip/attribute-tooltip.svelte';
import type { SkillMetrics, SkillsView } from './skills-view.svelte';

type Props = {
	view: SkillsView;
	metrics: SkillMetrics;
};

const { view, metrics }: Props = $props();

let tooltip = $state<TooltipComponent>();
const tip = createAttributeTooltip(() => tooltip);

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

.achip {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	gap: 4px;
	min-width: 46px;
	padding: 2px 8px;
	border: 1px solid color-mix(in srgb, var(--ac) 38%, transparent);
	border-radius: 3px;
	background: color-mix(in srgb, var(--ac) 12%, transparent);
	color: var(--ac);
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 0.5px;
	text-align: center;
	white-space: nowrap;
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
