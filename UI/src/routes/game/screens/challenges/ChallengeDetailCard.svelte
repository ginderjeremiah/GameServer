<div
	class="detail-card"
	class:done
	style:background={done ? tintColor('var(--success)', 0.045) : 'var(--panel)'}
	style:border="1px solid {done ? tintColor('var(--success)', 0.28) : 'var(--border-subtle)'}"
>
	<div>
		<div class="card-head">
			<StatusNode state={c.state} accent={c.typeAccent} size={20} />
			<span class="card-name" class:done>{c.name}</span>
			{#if c.target}
				<span
					class="card-target"
					style:color={tintColor(c.typeAccent, 0.85)}
					style:border="1px solid {tintColor(c.typeAccent, 0.3)}">{c.target}</span
				>
			{/if}
			<div class="card-spacer"></div>
			{#if done && completedAtDisplay}
				<span class="card-completed">{completedAtDisplay}</span>
			{/if}
		</div>
		<div class="card-desc">{c.description}</div>
	</div>
	<ProgressReadout {c} />
	<RewardAffordance reward={c.reward} variant="tile" />
	{#if c.unlocksZones.length > 0}
		<div class="card-unlocks">
			<span class="unlocks-label">Unlocks</span>
			{#each c.unlocksZones as zoneName (zoneName)}
				<span
					class="unlocks-zone"
					style:color={tintColor(c.typeAccent, 0.85)}
					style:border="1px solid {tintColor(c.typeAccent, 0.3)}">Zone · {zoneName}</span
				>
			{/each}
		</div>
	{/if}
</div>

<script lang="ts">
import { tintColor } from '$lib/common';
import type { ChallengeVM } from './challenges-view.svelte';
import ProgressReadout from './ProgressReadout.svelte';
import RewardAffordance from './RewardAffordance.svelte';
import StatusNode from './StatusNode.svelte';

interface Props {
	c: ChallengeVM;
}

const { c }: Props = $props();

const done = $derived(c.state === 'done');
const completedAtDisplay = $derived(
	c.completedAt ? new Date(c.completedAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : null
);
</script>

<style lang="scss">
.detail-card {
	border-radius: 4px;
	padding: 15px 17px;
	display: flex;
	flex-direction: column;
	gap: 12px;
}

.card-head {
	display: flex;
	align-items: center;
	gap: 9px;
}

.card-name {
	font-size: 16px;
	color: var(--text-primary);
	letter-spacing: -0.2px;

	&.done {
		color: var(--text-secondary);
	}
}

.card-target {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.5px;
	border-radius: 2px;
	padding: 1px 6px;
}

.card-spacer {
	flex: 1;
}

.card-completed {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.card-desc {
	font-size: 12px;
	color: var(--text-tertiary);
	margin-top: 6px;
	line-height: 1.5;
}

.card-unlocks {
	display: flex;
	align-items: center;
	flex-wrap: wrap;
	gap: 7px;
}

.unlocks-label {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.unlocks-zone {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.5px;
	border-radius: 2px;
	padding: 1px 6px;
}
</style>
