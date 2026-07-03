<!-- Challenges sub-tab: the challenges scoped to this enemy, with the player's progress. -->
<div class="challenges">
	<div class="label">Related challenges · {view.challenges.length}</div>
	{#if view.challengesError}
		<div class="note">Your challenge progress could not be loaded.</div>
	{:else}
		<div class="list">
			{#each view.challenges as challenge (challenge.id)}
				<div class="challenge" style:--ch-accent={challenge.accent}>
					<div class="text">
						<div class="name">{challenge.name}</div>
						<div class="type">{challenge.typeLabel}</div>
					</div>
					<span class="prog" class:done={challenge.completed}>{challenge.progressText}</span>
				</div>
			{/each}
		</div>
	{/if}
</div>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin-bottom: 10px;
}

.list {
	display: flex;
	flex-direction: column;
	gap: 8px;
}

.challenge {
	display: flex;
	align-items: center;
	gap: 10px;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-left: 2px solid var(--ch-accent);
	border-radius: 4px;
	padding: 9px 11px;
}

.text {
	flex: 1;
	min-width: 0;
}

.name {
	font-size: 12.5px;
	color: var(--text-primary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.type {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.8px;
	text-transform: uppercase;
	color: var(--ch-accent);
	margin-top: 2px;
}

.prog {
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-secondary);

	&.done {
		color: var(--success);
	}
}

.note {
	font-size: 12px;
	color: var(--text-tertiary);
	font-style: italic;
	line-height: 1.5;
}
</style>
