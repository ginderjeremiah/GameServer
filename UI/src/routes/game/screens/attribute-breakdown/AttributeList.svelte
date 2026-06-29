<!-- The left rail: every displayed attribute, grouped by display taxonomy (Primary / Secondary /
     Status), with the damage-type amplification/resistance family further split into per-type sub-groups
     (#1320, Area F) — each a selectable row. -->
<div class="list" data-testid="attribute-list">
	{#each view.groups as group (group.key)}
		<div class="group">
			{#if group.damageTypeKey !== undefined}
				<span class="group-label damage-type" style:color={damageTypeKeyColor(group.damageTypeKey)}>
					<DamageTypeGlyph
						glyph={damageTypeKeyGlyph(group.damageTypeKey)}
						color={damageTypeKeyColor(group.damageTypeKey)}
						size={12}
					/>
					{group.label}
				</span>
			{:else}
				<span class="group-label">{group.label}</span>
			{/if}
			{#each group.attrs as entry (entry.meta.id)}
				<AttributeListRow
					meta={entry.meta}
					computed={entry.computed}
					active={view.selected === entry.meta.id}
					onSelect={(id) => view.select(id)}
				/>
			{/each}
		</div>
	{/each}
</div>

<script lang="ts">
import AttributeListRow from './AttributeListRow.svelte';
import DamageTypeGlyph from '$components/DamageTypeGlyph.svelte';
import { damageTypeKeyColor, damageTypeKeyGlyph } from '$lib/common';
import type { AttributeBreakdownView } from './attribute-breakdown-view.svelte';

interface Props {
	view: AttributeBreakdownView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.list {
	overflow: auto;
	padding-right: 4px;
	border-right: 1px solid color-mix(in srgb, var(--white) 6%, transparent);
}

.group {
	margin-bottom: 12px;
}

.group-label {
	display: block;
	margin: 4px 0 6px;
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-muted);

	// Damage-type sub-group headers carry their type's accent (set inline) plus its glyph.
	&.damage-type {
		display: flex;
		align-items: center;
		gap: 5px;
	}
}
</style>
