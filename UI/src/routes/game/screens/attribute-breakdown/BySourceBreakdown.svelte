<!-- "By source" view of the selected attribute: each contributing source with
     its subtotal, itemised down to the individual stat-point / item / mod /
     derived contributions. -->
<div class="by-source">
	<span class="section-label">By source</span>
	{#each groups as group (group.source)}
		<div class="group">
			<div class="group-head">
				<span class="swatch" style:background={sourceColor(group.source)}></span>
				<span class="src-name">{sourceLabel(group.source)}</span>
				<span class="rule"></span>
				<span class="src-total">{fmtSigned(group.total, 1)}</span>
			</div>
			{#each group.lines as line, i (i)}
				<div class="line">
					<span class="line-label">
						{#if line.source === EAttributeModifierSource.Derived}
							{attributeName(line.derivedSource, staticData.attributes)} ({line.amount}× of {fmtNum(
								line.derivedValue ?? 0,
								1
							)})
						{:else if line.source === EAttributeModifierSource.ItemMod && line.modType !== undefined}
							{modifierLabel(line)} · {modTypeLabel(line.modType)}
						{:else}
							{modifierLabel(line)}
						{/if}
					</span>
					<span class="line-amount" class:negative={line.applied < 0}>{fmtSigned(line.applied)}</span>
				</div>
			{/each}
		</div>
	{/each}
</div>

<script lang="ts">
import { EAttributeModifierSource, type SourceGroup } from '$lib/battle';
import { modTypeLabel, attributeName } from '$lib/common';
import { staticData } from '$stores';
import { sourceColor, sourceLabel } from './source-display';
import { fmtNum, fmtSigned, modifierLabel, type LabeledModifier } from './attribute-breakdown-view.svelte';

interface Props {
	groups: SourceGroup<LabeledModifier>[];
}

let { groups }: Props = $props();
</script>

<style lang="scss">
.section-label {
	display: block;
	margin-bottom: 10px;
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.group {
	margin-bottom: 12px;
}

.group-head {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 5px;
}

.swatch {
	width: 9px;
	height: 9px;
	border-radius: 1px;
	flex-shrink: 0;
}

.src-name {
	font-size: 12px;
	color: var(--text-primary);
}

.rule {
	flex: 1;
	height: 1px;
	background: color-mix(in srgb, var(--white) 6%, transparent);
}

.src-total {
	font-family: var(--mono);
	font-size: 12px;
	color: var(--text-secondary);
}

.line {
	display: flex;
	justify-content: space-between;
	gap: 10px;
	padding: 2px 0 2px 17px;
}

.line-label {
	font-size: 11.5px;
	color: var(--text-tertiary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.line-amount {
	font-family: var(--mono);
	font-size: 11.5px;
	color: var(--text-secondary);

	&.negative {
		color: var(--enemy-accent);
	}
}
</style>
