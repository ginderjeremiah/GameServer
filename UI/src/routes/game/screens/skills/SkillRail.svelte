<div class="rail">
	<div class="rail-top">
		<div class="searchrow">
			<label class="search">
				<span class="search-glyph" aria-hidden="true">⌕</span>
				<input
					type="text"
					placeholder="search skills…"
					value={view.search}
					oninput={(e) => (view.search = e.currentTarget.value)}
				/>
			</label>
			<button type="button" class="filt-btn" onclick={() => (view.modalOpen = true)}>
				⇅ Sort·Filter
				{#if view.activeFilterCount}
					<span class="badge">{view.activeFilterCount}</span>
				{/if}
			</button>
		</div>
		<div class="rail-meta">
			<span class="label">Sort: {sortLabel}</span>
			<span class="label">{filterLabel}</span>
		</div>
	</div>

	<div class="rail-list">
		{#if view.equippedRail.length}
			<div class="grp">
				<span>◆ Equipped</span>
				<span>{view.equipped.length}/{view.cap}</span>
			</div>
			{#each view.equippedRail as metrics (metrics.skill.id)}
				<SkillRow {metrics} {view} />
			{/each}
		{/if}
		<div class="grp">
			<span>Available</span>
			<span>{view.availableRail.length}</span>
		</div>
		{#if view.availableRail.length}
			{#each view.availableRail as metrics (metrics.skill.id)}
				<SkillRow {metrics} {view} />
			{/each}
		{:else}
			<div class="empty">none match the filter</div>
		{/if}
	</div>
</div>

<script lang="ts">
import { SKILL_SORTS, type SkillsView } from './skills-view.svelte';
import SkillRow from './SkillRow.svelte';
import { attributeCode } from '$lib/common';

type Props = {
	view: SkillsView;
};

const { view }: Props = $props();

const sortLabel = $derived(SKILL_SORTS.find((s) => s.key === view.sort)?.label ?? 'DPS');
const filterLabel = $derived.by(() => {
	const attrs = view.filterAttributes.length
		? 'Attr: ' + view.filterAttributes.map((a) => attributeCode(a)).join(', ')
		: 'All attributes';
	return attrs + (view.showLocked ? ' · +locked' : '');
});
</script>

<style lang="scss">
.rail {
	display: flex;
	flex-direction: column;
	overflow: hidden;
	background: color-mix(in srgb, var(--white) 2.5%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
}

.rail-top {
	display: flex;
	flex-direction: column;
	gap: 8px;
	padding: 10px 10px 9px;
	border-bottom: 1px solid var(--border-subtle);
}

.searchrow {
	display: flex;
	gap: 7px;
}

.search {
	flex: 1;
	display: flex;
	align-items: center;
	gap: 7px;
	padding: 0 10px;
	border: 1px solid var(--border-light);
	border-radius: 3px;

	.search-glyph {
		color: var(--text-muted);
		font-size: 12px;
	}

	input {
		flex: 1;
		min-width: 0;
		padding: 7px 0;
		border: none;
		background: transparent;
		outline: none;
		font-family: var(--mono);
		font-size: 10.5px;
		color: var(--text-primary);

		&::placeholder {
			color: var(--text-muted);
		}
	}
}

.filt-btn {
	display: flex;
	align-items: center;
	gap: 6px;
	padding: 7px 10px;
	border: 1px solid var(--border-light);
	border-radius: 3px;
	background: transparent;
	cursor: pointer;
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-secondary);

	.badge {
		padding: 0 5px;
		border-radius: 8px;
		background: var(--accent);
		color: var(--text-on-accent);
		font-size: 8.5px;
		line-height: 14px;
	}
}

.rail-meta {
	display: flex;
	align-items: center;
	justify-content: space-between;
}

.label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.rail-list {
	flex: 1;
	padding: 5px 7px 8px;
	overflow-y: auto;
}

.grp {
	display: flex;
	justify-content: space-between;
	padding: 9px 5px 5px;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.empty {
	padding: 9px 6px;
	font-family: var(--mono);
	font-size: 9px;
	color: var(--text-muted);
}
</style>
