<!-- "Your record" section: the player's tracked statistics against the selected entity — every
     statistic that references it, reusing the Statistics screen's per-entity query. Runtime progress,
     not part of the entity's reference data. Shared by the enemy, zone and skill dossiers. -->
<div class="statistics">
	<div class="label">Your record</div>

	{#if loading}
		<div class="note">Loading your record…</div>
	{:else if error}
		<div class="note">Your statistics could not be loaded.</div>
	{:else if stats.length === 0}
		<div class="note">{emptyMessage}</div>
	{:else}
		<div class="grid" data-testid={testid}>
			{#each stats as stat (stat.label)}
				<div class="card">
					<div class="card-label">{stat.label}</div>
					<div class="card-val">{stat.value}</div>
				</div>
			{/each}
		</div>
		<div class="caption">Tracked from your battles — runtime progress, not part of the reference data.</div>
	{/if}
</div>

<script lang="ts">
import type { EntityStatVM } from './codex-view.svelte';

interface Props {
	/** The statistics referencing this entity (label + formatted value). */
	stats: EntityStatVM[];
	loading: boolean;
	error: boolean;
	/** Message shown when the player has no recorded statistics for this entity yet. */
	emptyMessage: string;
	/** Stable hook for tests to target this entity kind's grid. */
	testid: string;
}

let { stats, loading, error, emptyMessage, testid }: Props = $props();
</script>

<style lang="scss">
.label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin-bottom: 11px;
}

.grid {
	display: grid;
	grid-template-columns: 1fr 1fr;
	gap: 9px;
}

.card {
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	padding: 9px 11px;
}

.card-label {
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.card-val {
	font-family: var(--mono);
	font-size: 16px;
	font-weight: 500;
	color: var(--text-primary);
	margin-top: 3px;
}

.caption {
	font-size: 11px;
	color: var(--text-muted);
	font-style: italic;
	line-height: 1.45;
	margin-top: 12px;
}

.note {
	font-size: 12px;
	color: var(--text-tertiary);
	font-style: italic;
	line-height: 1.5;
}
</style>
