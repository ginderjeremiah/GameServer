<!-- The left rail: every displayed attribute, grouped by display taxonomy
     (Primary / Secondary / Status), each a selectable row. -->
<div class="list" data-testid="attribute-list">
	{#each view.groups as group (group.type)}
		<div class="group">
			<span class="group-label">{group.label}</span>
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
}
</style>
