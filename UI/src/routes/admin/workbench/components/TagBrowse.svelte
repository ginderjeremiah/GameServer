<div class="tag-browse">
	<div class="cat-list">
		{#each reference.tagCategories as category (category.id)}
			{@const color = reference.tagColor(category.id)}
			{@const appliedCount = reference.tagsByCategory(category.id).filter((t) => ids.includes(t.id)).length}
			{@const active = !searching && cat === category.id}
			<button type="button" class="cat-item" class:active onclick={() => selectCategory(category.id)}>
				<span class="tdot" style:background={color.fg}></span>
				<span class="cat-name" class:active>{category.name}</span>
				<span class="cat-total">{reference.tagsByCategory(category.id).length}</span>
				{#if appliedCount > 0}<span class="cat-count">{appliedCount}</span>{/if}
			</button>
		{/each}
	</div>
	<div class="cat-body">
		<div class="search" style:max-width="360px" style:margin-bottom="14px">
			<WorkbenchIcon kind="search" sw={1.4} />
			<input class="inp" placeholder="Filter tags…" bind:value={q} />
		</div>
		<div class="toggle-grid">
			{#each shown as tag (tag.id)}
				{@const color = reference.tagColor(tag.tagCategoryId)}
				{@const on = ids.includes(tag.id)}
				<button
					type="button"
					class="tag-toggle"
					class:on
					style:color={on ? color.fg : undefined}
					style:border-color={on ? color.bd : undefined}
					style:background={on ? color.bg : undefined}
					onclick={() => onToggle(tag.id)}
				>
					<span class="tdot" style:background={on ? color.fg : 'var(--text-muted)'}></span>{tag.name}
				</button>
			{/each}
			{#if shown.length === 0}<span class="tag-empty">No tags here.</span>{/if}
		</div>
	</div>
</div>

<script lang="ts">
import { reference } from '../reference.svelte';
import WorkbenchIcon from '../WorkbenchIcon.svelte';

interface Props {
	ids: number[];
	onToggle: (id: number) => void;
}

const { ids, onToggle }: Props = $props();

let q = $state('');
let cat = $state(reference.tagCategories[0]?.id ?? 1);
const ql = $derived(q.trim().toLowerCase());
const searching = $derived(ql !== '');
const shown = $derived(
	searching ? reference.tags.filter((t) => t.name.toLowerCase().includes(ql)) : reference.tagsByCategory(cat)
);

const selectCategory = (id: number) => {
	cat = id;
	q = '';
};
</script>

<style lang="scss">
.tag-browse {
	margin-top: 14px;
	display: flex;
	gap: 16px;
	border: 1px solid var(--border-subtle);
	border-radius: 6px;
	overflow: hidden;
	min-height: 280px;
}
.cat-list {
	width: 196px;
	flex-shrink: 0;
	border-right: 1px solid var(--border-subtle);
	background: var(--surface);
	overflow-y: auto;
	max-height: 360px;
}
.cat-item {
	width: 100%;
	text-align: left;
	background: transparent;
	border: none;
	border-bottom: 1px solid var(--border-subtle);
	padding: 10px 14px;
	cursor: pointer;
	display: flex;
	align-items: center;
	gap: 9px;

	&.active {
		background: color-mix(in srgb, var(--accent) 8%, transparent);
	}
}
.cat-name {
	flex: 1;
	font-size: 12.5px;
	color: var(--text-secondary);

	&.active {
		color: var(--text-primary);
	}
}
.cat-total {
	font-family: var(--mono);
	font-size: 10px;
	color: var(--text-muted);
}
.cat-body {
	flex: 1;
	min-width: 0;
	padding: 14px;
	overflow-y: auto;
	max-height: 360px;
}
.toggle-grid {
	display: flex;
	flex-wrap: wrap;
	gap: 7px;
}
</style>
