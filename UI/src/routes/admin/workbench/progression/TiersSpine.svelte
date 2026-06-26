<div class="spine">
	{#each tiers as tier, index (tier.id)}
		{@const status = store.profStatus(tier)}
		<div class="tier-row">
			<div class="reorder">
				<button
					type="button"
					class="rbtn"
					aria-label="Move up"
					disabled={index === 0}
					onclick={() => store.reorderTiers(pathId, index, index - 1)}>▲</button
				>
				<button
					type="button"
					class="rbtn"
					aria-label="Move down"
					disabled={index === tiers.length - 1}
					onclick={() => store.reorderTiers(pathId, index, index + 1)}>▼</button
				>
			</div>
			<span class="node">{tier.pathOrdinal}</span>
			<button type="button" class="card" class:edited={status !== 'clean'} onclick={() => store.drillTier(tier.id)}>
				<div class="card-main">
					<div class="card-name" class:blank={!tier.name}>{tier.name || 'Unnamed tier'}</div>
					<div class="card-meta">{metaFor(tier)}</div>
				</div>
				<div class="spacer"></div>
				{#if store.isRetired(tier)}<span class="badge retired">retired</span>{/if}
				{#if status === 'added'}<span class="badge added">new</span>{:else if status === 'modified'}<span
						class="badge modified">edited</span
					>{/if}
				{#if tier.word}<span class="badge word">word · <WordOfPower text={tier.word} label={tier.name} /></span>{/if}
				<WorkbenchIcon kind="chevR" size={14} stroke="var(--text-muted)" />
			</button>
		</div>
	{/each}

	<button type="button" class="add-tier" data-testid="progression-add-tier" onclick={() => store.addTier(pathId)}>
		<span class="add-circle"><WorkbenchIcon kind="plus" size={12} /></span>
		Add tier — create a proficiency
	</button>
</div>

<script lang="ts">
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import WordOfPower from '$components/WordOfPower.svelte';
import type { ProgressionStore } from './progression-store.svelte';
import { tiersOfPath } from './progression-helpers';
import type { WorkbenchProficiency } from './types';

interface Props {
	store: ProgressionStore;
	pathId: number;
}

const { store, pathId }: Props = $props();

const tiers = $derived(tiersOfPath(store.profs, pathId));

const metaFor = (tier: WorkbenchProficiency): string => {
	const milestones = tier.levelRewards.length;
	const skills = (store.selectedPath?.contributions ?? []).filter((c) => c.homeTier === tier.pathOrdinal).length;
	const gated = tier.prerequisiteIds.length > 0 ? ' · gated ✦' : '';
	return `cap ${tier.maxLevel} · ${milestones} ${milestones === 1 ? 'milestone' : 'milestones'} · ${skills} ${skills === 1 ? 'skill' : 'skills'}${gated}`;
};
</script>

<style lang="scss">
.spine {
	display: flex;
	flex-direction: column;
	gap: 10px;
}
.tier-row {
	display: flex;
	align-items: center;
	gap: 12px;
}
.reorder {
	display: flex;
	flex-direction: column;
	gap: 2px;
}
.rbtn {
	background: transparent;
	border: none;
	color: var(--text-muted);
	font-size: 9px;
	line-height: 1;
	padding: 1px 2px;
	cursor: pointer;

	&:hover:not(:disabled) {
		color: var(--accent);
	}
	&:disabled {
		opacity: 0.3;
		cursor: not-allowed;
	}
}
.node {
	width: 26px;
	height: 26px;
	flex-shrink: 0;
	border-radius: 50%;
	background: var(--panel-2);
	border: 1px solid var(--border-light);
	color: var(--text-tertiary);
	font-family: var(--mono);
	font-size: 11px;
	display: flex;
	align-items: center;
	justify-content: center;
}
.card {
	flex: 1;
	background: var(--panel);
	border: 1px solid var(--border-light);
	border-radius: 4px;
	padding: 12px 16px;
	display: flex;
	align-items: center;
	gap: 12px;
	cursor: pointer;
	text-align: left;

	&.edited {
		border-left: 2px solid var(--change-modified);
	}
	&:hover {
		border-color: var(--accent);
		background: color-mix(in srgb, var(--accent) 5%, transparent);
	}
}
.card-name {
	font-size: 15px;
	font-weight: 500;
	color: var(--text-primary);
	margin-bottom: 3px;

	&.blank {
		color: var(--text-muted);
	}
}
.card-meta {
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-tertiary);
}
.spacer {
	flex: 1;
}
.badge {
	display: inline-flex;
	align-items: center;
	gap: 4px;
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	text-transform: uppercase;
	border-radius: 3px;
	padding: 3px 8px;

	&.word {
		color: var(--accent);
		border: 1px solid color-mix(in srgb, var(--accent) 35%, transparent);
		background: color-mix(in srgb, var(--accent) 8%, transparent);
	}
	&.added {
		color: var(--change-added);
		border: 1px solid color-mix(in srgb, var(--change-added) 40%, transparent);
		background: color-mix(in srgb, var(--change-added) 10%, transparent);
	}
	&.modified {
		color: var(--change-modified);
		border: 1px solid color-mix(in srgb, var(--change-modified) 40%, transparent);
		background: color-mix(in srgb, var(--change-modified) 10%, transparent);
	}
	&.retired {
		color: var(--text-muted);
		border: 1px solid var(--border-light);
	}
}
.add-tier {
	display: inline-flex;
	align-items: center;
	gap: 12px;
	background: transparent;
	border: 1px dashed var(--border-light);
	border-radius: 4px;
	color: var(--text-tertiary);
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.5px;
	text-transform: uppercase;
	padding: 10px 16px;
	cursor: pointer;
	margin-top: 2px;
	align-self: flex-start;

	&:hover {
		border-color: var(--accent);
		color: var(--accent);
	}
}
.add-circle {
	width: 26px;
	height: 26px;
	border-radius: 50%;
	border: 1px dashed var(--border-light);
	color: var(--text-muted);
	display: flex;
	align-items: center;
	justify-content: center;
}
</style>
