<div class="panel">
	<div class="panel-head">
		Derived Stats <span class="count">· {DERIVED_STATS.length}</span>
	</div>
	<div class="panel-body">
		{#each visibleGroups as group (group)}
			<div class="group">
				<div class="group-label">{group}</div>
				{#each STATS_BY_GROUP[group] as d (d.id)}
					{@const from = view.savedDerived[d.id]}
					{@const to = view.derived[d.id]}
					{@const isChanged = from !== to}
					<div class="stat" class:changed={isChanged}>
						<AttributeIcon id={d.id} size={24} />
						<div class="info">
							<div class="name">{attributeName(d.id, staticData.attributes)}</div>
							<div class="fed-by">
								{#each contributorsFor(d.id) as cid (cid)}
									<span class="src" style="color: {attributeColor(cid)}"
										>{attributeCode(cid, staticData.attributes)}</span
									>
								{/each}
							</div>
						</div>
						{#if isChanged}
							<span class="stat-delta" class:up={to > from}>
								{formatAttributeDelta(to - from, d.id, staticData.attributes)}
							</span>
						{/if}
						<span class="stat-val" class:changed={isChanged}
							>{formatAttributeValue(to, d.id, staticData.attributes)}</span
						>
					</div>
				{/each}
			</div>
		{/each}
	</div>
</div>

<script lang="ts">
import { formatAttributeValue, formatAttributeDelta, attributeColor, attributeCode, attributeName } from '$lib/common';
import { staticData } from '$stores';
import AttributeIcon from '$components/AttributeIcon.svelte';
import {
	DERIVED_GROUPS,
	DERIVED_STATS,
	contributorsFor,
	type AttributesView,
	type DerivedGroup,
	type DerivedStatDef
} from './attributes-view.svelte';

interface Props {
	view: AttributesView;
}

const { view }: Props = $props();

// The derived stats grouped once (static reference data) so the panel doesn't
// re-scan DERIVED_STATS on every reactive update.
const STATS_BY_GROUP = DERIVED_GROUPS.reduce(
	(map, group) => {
		map[group] = DERIVED_STATS.filter((d) => d.group === group);
		return map;
	},
	{} as Record<DerivedGroup, DerivedStatDef[]>
);
const visibleGroups = DERIVED_GROUPS.filter((g) => STATS_BY_GROUP[g].length > 0);
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
