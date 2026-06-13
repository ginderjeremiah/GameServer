<div class="panel">
	<div class="panel-head">
		Derived Stats <span class="count">· {DERIVED_STATS.length}</span>
	</div>
	<div class="panel-body">
		{#each visibleGroups as group (group)}
			<div class="group">
				<div class="group-label">{group}</div>
				{#each statsIn(group) as d (d.id)}
					{@const from = view.savedDerived[d.id]}
					{@const to = view.derived[d.id]}
					{@const isChanged = from !== to}
					<div class="stat" class:changed={isChanged}>
						<div class="info">
							<div class="name">{attributeName(d.id, staticData.attributes)}</div>
							<div class="fed-by">
								{#each contributorsFor(d.id) as cid (cid)}
									<span class="src" style="color: {attributeColor(cid)}">{attributeCode(cid)}</span>
								{/each}
							</div>
						</div>
						{#if isChanged}
							<span class="stat-delta" class:up={to > from}>
								{to > from ? '+' : ''}{formatNum(round1(to - from))}
							</span>
						{/if}
						<span class="stat-val" class:changed={isChanged}>{formatNum(to)}{derivedUnit(d.id)}</span>
					</div>
				{/each}
			</div>
		{/each}
	</div>
</div>

<script lang="ts">
import { formatNum, attributeColor, attributeCode, attributeName } from '$lib/common';
import { staticData } from '$stores';
import {
	DERIVED_GROUPS,
	DERIVED_STATS,
	CORE_ATTRIBUTES,
	derivedUnit,
	feedsFor,
	type AttributesView,
	type DerivedGroup
} from './attributes-view.svelte';
import type { EAttribute } from '$lib/api';

interface Props {
	view: AttributesView;
}

const { view }: Props = $props();

const statsIn = (group: DerivedGroup) => DERIVED_STATS.filter((d) => d.group === group);
const visibleGroups = DERIVED_GROUPS.filter((g) => statsIn(g).length > 0);

/** The core attributes that feed a derived stat — the live inverse of `feedsFor`,
 *  so the "fed by" line never goes stale. */
const contributorsFor = (derivedId: EAttribute): EAttribute[] =>
	CORE_ATTRIBUTES.filter((_, i) => feedsFor(i).includes(derivedId));

const round1 = (n: number): number => Math.round(n * 10) / 10;
</script>

<style lang="scss">
.panel {
	flex: 1;
	min-height: 0;
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	background: color-mix(in srgb, var(--black) 25%, transparent);
	display: flex;
	flex-direction: column;
	overflow: hidden;
}

.panel-head {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--text-primary) 42%, transparent);
	padding: 11px 14px 9px;
	border-bottom: 1px solid color-mix(in srgb, var(--white) 7%, transparent);
	flex-shrink: 0;
}

.count {
	color: color-mix(in srgb, var(--text-primary) 25%, transparent);
}

.panel-body {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	padding: 4px 0;
}

.group {
	margin-bottom: 2px;
}

.group-label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.2px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--accent) 60%, transparent);
	padding: 7px 14px 4px;
}

.stat {
	display: flex;
	align-items: center;
	gap: 8px;
	padding: 6px 14px;

	&.changed {
		background: color-mix(in srgb, var(--accent) 5%, transparent);
	}
}

.info {
	flex: 1;
	min-width: 0;
}

.name {
	font-family: var(--mono);
	font-size: 11.5px;
	color: var(--text-secondary);
}

.fed-by {
	display: flex;
	gap: 7px;
	margin-top: 1px;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.5px;
}

.stat-delta {
	font-family: var(--mono);
	font-size: 10px;
	color: var(--enemy-accent);

	&.up {
		color: color-mix(in srgb, var(--success) 90%, transparent);
	}
}

.stat-val {
	font-family: var(--mono);
	font-size: 13px;
	min-width: 50px;
	text-align: right;
	font-weight: 500;
	color: var(--text-primary);

	&.changed {
		color: var(--accent);
	}
}
</style>
