<div>
	<div class="usage-summary">
		<span class="tdot" style:background={color.fg} style:width="9px" style:height="9px"></span>
		<span class="usage-count"
			>Applied to <b>{usage.items + usage.mods}</b> record{usage.items + usage.mods === 1 ? '' : 's'}</span
		>
		<span class="usage-note">read-only · derived from item &amp; mod tag lists</span>
	</div>
	<div class="usage-groups">
		{@render group('Items', 'box', usage.itemList, 'items')}
		{@render group('Item Mods', 'rune', usage.modList, 'mods')}
	</div>
</div>

{#snippet group(
	label: string,
	glyph: WorkbenchIconKind,
	list: { id: number; name: string; rarityId?: number }[],
	noun: string
)}
	<div class="usage-group">
		<div class="sec-title">
			<WorkbenchIcon kind={glyph} size={13} stroke="var(--text-tertiary)" />{label}
			<span class="count-badge">{list.length}</span><span class="ln"></span>
		</div>
		{#if list.length === 0}
			<span class="tag-empty">No {noun} use this tag.</span>
		{:else}
			<div class="usage-tags">
				{#each list.slice(0, 40) as record (record.id)}
					<span class="ov-tag">
						{#if record.rarityId}
							<span class="tdot" style:background={reference.rarityColor(record.rarityId)}></span>
						{/if}
						{record.name}
					</span>
				{/each}
				{#if list.length > 40}<span class="ov-tag muted">+{list.length - 40}</span>{/if}
			</div>
		{/if}
	</div>
{/snippet}

<script lang="ts">
import type { ITag } from '$lib/api';
import { reference } from '../reference.svelte';
import WorkbenchIcon, { type WorkbenchIconKind } from '../WorkbenchIcon.svelte';

interface Props {
	record: ITag;
}

const { record }: Props = $props();
const usage = $derived(reference.tagUsage(record.id));
const color = $derived(reference.tagColor(record.tagCategoryId));
</script>

<style lang="scss">
.usage-summary {
	display: flex;
	align-items: center;
	gap: 10px;
	margin-bottom: 18px;
	padding: 12px 14px;
	border: 1px solid var(--border-subtle);
	border-radius: 6px;
	background: color-mix(in srgb, var(--white) 1.5%, transparent);
}
.usage-count {
	font-size: 13.5px;
}
.usage-note {
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-muted);
	margin-left: auto;
}
.usage-groups {
	display: flex;
	gap: 28px;
	flex-wrap: wrap;
}
.usage-group {
	flex: 1 1 320px;
	min-width: 280px;
}
.usage-tags {
	display: flex;
	flex-wrap: wrap;
	gap: 7px;
}
.muted {
	color: var(--text-muted);
}
</style>
