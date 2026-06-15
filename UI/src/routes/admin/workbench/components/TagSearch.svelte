<div class="tag-search">
	<div class="search" style:max-width="420px">
		<WorkbenchIcon kind="search" sw={1.4} />
		<!-- svelte-ignore a11y_autofocus -->
		<input class="inp" autofocus aria-label="Search all tags" placeholder="Search all tags…" bind:value={q} />
	</div>
	<div class="result-summary">
		{matchCount} match{matchCount === 1 ? '' : 'es'} across {groups.length} categor{groups.length === 1 ? 'y' : 'ies'} · click
		to add
	</div>
	<div class="tag-results">
		{#each groups as group (group.cat.id)}
			{@const color = reference.tagColor(group.cat.id)}
			<div class="result-group">
				<div class="tag-group-head">
					<span class="tdot" style:background={color.fg}></span>{group.cat.name}<span class="muted"
						>· {group.total}</span
					>
				</div>
				<div class="result-chips">
					{#each group.tags as tag (tag.id)}
						<button
							type="button"
							class="tag-add"
							style:color={color.fg}
							style:border-color={color.bd}
							onclick={() => onAdd(tag.id)}
						>
							<WorkbenchIcon kind="plus" size={10} sw={1.8} />{tag.name}
						</button>
					{/each}
				</div>
			</div>
		{/each}
		{#if matchCount === 0}
			<span class="tag-empty">No matching tags.</span>
		{/if}
	</div>
</div>

<script lang="ts">
import { reference } from '../reference.svelte';
import WorkbenchIcon from '../WorkbenchIcon.svelte';

interface Props {
	ids: number[];
	onAdd: (id: number) => void;
}

const { ids, onAdd }: Props = $props();

let q = $state('');
const ql = $derived(q.trim().toLowerCase());
const matches = $derived(
	reference.tags.filter((t) => !ids.includes(t.id) && (ql === '' || t.name.toLowerCase().includes(ql)))
);
const matchCount = $derived(matches.length);

// Group results by category, capped at 60 chips total so a broad query stays light.
const groups = $derived.by(() => {
	let budget = Math.min(matchCount, 60);
	const out: { cat: { id: number; name: string }; tags: typeof matches; total: number }[] = [];
	for (const cat of reference.tagCategories) {
		const inCat = matches.filter((t) => t.tagCategoryId === cat.id);
		if (!inCat.length) {
			continue;
		}
		const take = inCat.slice(0, Math.max(0, budget));
		budget -= take.length;
		out.push({ cat, tags: take, total: inCat.length });
	}
	return out;
});
</script>

<style lang="scss">
.tag-search {
	margin-top: 14px;
}
.result-summary {
	font-family: var(--mono);
	font-size: 10.5px;
	color: var(--text-muted);
	margin: 12px 0 8px;
}
.result-group {
	margin-bottom: 12px;
}
.result-chips {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;
}
.muted {
	color: var(--text-muted);
}
</style>
