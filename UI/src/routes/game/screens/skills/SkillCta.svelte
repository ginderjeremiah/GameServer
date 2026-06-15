<div class="d-cta">
	{#if !metrics.unlocked}
		<button type="button" class="btn dim" disabled>🔒 Locked</button>
		<div class="d-hint">Unlock by completing<br /><b>{metrics.source?.name ?? 'a challenge'}</b></div>
	{:else if equipped}
		<button type="button" class="btn danger" onclick={() => view.toggle(metrics.skill.id)}>✓ In loadout · Remove</button
		>
		<div class="d-hint">priority slot {view.slotOf(metrics.skill.id)} · drag below to reorder</div>
	{:else if pending}
		<button type="button" class="btn ghost" onclick={() => view.cancelSwap()}>Cancel swap</button>
		<div class="d-hint">pick an equipped card below to replace</div>
	{:else if full}
		<button type="button" class="btn ghost" onclick={() => view.toggle(metrics.skill.id)}>Loadout full · Swap…</button>
		<div class="d-hint">choose which slot to replace</div>
	{:else}
		<button type="button" class="btn" onclick={() => view.toggle(metrics.skill.id)}>Equip ▸</button>
		<div class="d-hint">{view.cap - view.equipped.length} slot(s) free</div>
	{/if}
</div>

<script lang="ts">
import type { SkillMetrics, SkillsView } from './skills-view.svelte';

type Props = {
	view: SkillsView;
	metrics: SkillMetrics;
	equipped: boolean;
	pending: boolean;
	full: boolean;
};

const { view, metrics, equipped, pending, full }: Props = $props();
</script>

<style lang="scss">
.d-cta {
	display: flex;
	flex-direction: column;
	gap: 6px;
	align-items: flex-end;
	flex-shrink: 0;
}

.d-hint {
	max-width: 170px;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.4px;
	line-height: 1.5;
	text-align: right;
	color: var(--text-muted);

	b {
		color: var(--text-secondary);
	}
}

.btn {
	padding: 8px 16px;
	border: 1px solid var(--accent);
	border-radius: 3px;
	background: var(--accent);
	color: var(--text-on-accent);
	font-family: var(--sans);
	font-size: 13px;
	font-weight: 500;
	white-space: nowrap;
	cursor: pointer;

	&.ghost {
		background: transparent;
		color: var(--text-primary);
		border-color: var(--border-medium);
	}

	&.danger {
		background: color-mix(in srgb, var(--enemy-accent) 16%, transparent);
		color: var(--enemy-accent);
		border-color: color-mix(in srgb, var(--enemy-accent) 55%, transparent);
	}

	&.dim {
		background: transparent;
		color: var(--text-muted);
		border-color: var(--border-light);
		cursor: not-allowed;
	}
}
</style>
