<!--
	Reward picker with hard exclusivity: a reward already unlocked by another
	challenge is struck-through and disabled (showing the owning challenge), so a
	reward can never be assigned to two challenges.
-->
<div class="ch-picker">
	<div class="ch-picker-bar">
		<div class="search picker-search">
			<WorkbenchIcon kind="search" sw={1.4} />
			<!-- svelte-ignore a11y_autofocus -->
			<input
				class="inp"
				autofocus
				placeholder={`Search ${kind === 'item' ? 'items' : kind === 'mod' ? 'item mods' : 'skills'}…`}
				bind:value={query}
			/>
		</div>
		<span class="ch-picker-count">{availableCount} of {records.length} unclaimed</span>
		<button type="button" class="row-x" title="Close" onclick={onClose}><WorkbenchIcon kind="x" size={11} /></button>
	</div>
	<div class="ch-picker-list">
		{#each matches.slice(0, 80) as record (record.id)}
			{@const owner = claimed.get(record.id)}
			{@const isClaimed = !!owner && record.id !== currentId}
			{@const isCurrent = record.id === currentId}
			<button
				type="button"
				class="ch-picker-row"
				class:claimed={isClaimed}
				class:current={isCurrent}
				disabled={isClaimed}
				title={isClaimed ? `Already unlocked by “${owner?.name}”` : undefined}
				onclick={() => !isClaimed && onPick(record.id)}
			>
				<span class="tdot" style="background: {isClaimed ? 'var(--text-muted)' : record.color}"></span>
				<span class="ch-picker-nm" class:retired={record.retired}>{record.name}</span>
				<span class="rare-tag" style="color: {isClaimed ? 'var(--text-muted)' : record.color}">{record.tag}</span>
				<span class="picker-spacer"></span>
				{#if record.retired}<span class="ch-picker-state retired">retired</span>{/if}
				{#if isCurrent}<span class="ch-picker-state current">selected</span>{/if}
				{#if isClaimed}<span class="ch-picker-state"><WorkbenchIcon kind="x" size={9} sw={2} />{owner?.name}</span>{/if}
			</button>
		{/each}
		{#if matches.length === 0}<div class="ch-picker-empty">No matches.</div>{/if}
	</div>
</div>

<script lang="ts">
import type { IChallenge } from '$lib/api';
import WorkbenchIcon from '../../WorkbenchIcon.svelte';

export interface PickerRecord {
	id: number;
	name: string;
	color: string;
	tag: string;
	/** A retired record — only ever present when it's the challenge's current reward (kept for display). */
	retired?: boolean;
}

interface Props {
	kind: 'item' | 'mod' | 'skill';
	records: PickerRecord[];
	currentId: number | undefined;
	claimed: Map<number, IChallenge>;
	onPick: (id: number) => void;
	onClose: () => void;
}

const { kind, records, currentId, claimed, onPick, onClose }: Props = $props();

let query = $state('');
const matches = $derived.by(() => {
	const q = query.trim().toLowerCase();
	return q === '' ? records : records.filter((r) => r.name.toLowerCase().includes(q));
});
const availableCount = $derived(records.filter((r) => !claimed.has(r.id) || r.id === currentId).length);
</script>

<style lang="scss">
.picker-search {
	flex: 1;
}
.picker-spacer {
	flex: 1;
}
.ch-picker-empty {
	font-family: var(--mono);
	font-size: 11.5px;
	color: var(--text-muted);
	padding: 14px 4px;
}
.ch-picker-nm.retired {
	text-decoration: line-through;
	color: var(--text-muted);
}
.ch-picker-state.retired {
	color: var(--text-muted);
}
</style>
