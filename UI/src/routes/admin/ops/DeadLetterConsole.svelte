<div class="ops-console" data-testid="dead-letter-console">
	<div class="head">
		<div class="eyebrow">Admin Console · Ops</div>
		<div class="title-row">
			<h1 class="title" data-testid="dl-title">{title}</h1>
			<div class="summary">
				<span class="depth" data-testid="dl-depth">{queue.totalCount} in queue</span>
				{#if queue.selectedCount > 0}
					<span class="sel">{queue.selectedCount} selected</span>
				{/if}
				{#if queue.hasMore}
					<span class="more">showing first {queue.entries.length}</span>
				{/if}
			</div>
		</div>
		<p class="blurb">
			{#if isSocket}
				Server-initiated socket pushes (challenge/proficiency notices) that failed to deliver. Replay redelivers to
				whatever socket is currently live for the player — <em>malformed</em> and
				<em>unknown command type</em> entries are poison and will return to the queue immediately.
			{:else}
				Player write-behind events that failed to apply. Inspect the classified failure reason before replaying — <em
					>malformed</em
				>
				and <em>unknown event type</em> entries are poison and will return to the queue immediately.
			{/if}
		</p>
	</div>

	<div class="toolbar">
		<button
			type="button"
			class="btn"
			data-testid="dl-refresh"
			disabled={queue.loading || queue.replaying}
			onclick={refresh}
		>
			Refresh
		</button>
		<div class="spacer"></div>
		<button
			type="button"
			class="btn"
			data-testid="dl-replay-selected"
			disabled={queue.selectedCount === 0 || queue.loading || queue.replaying}
			onclick={replaySelected}
		>
			Replay selected
		</button>
		<button
			type="button"
			class="btn primary"
			data-testid="dl-replay-all"
			disabled={queue.totalCount === 0 || queue.loading || queue.replaying}
			onclick={replayAll}
		>
			Replay all
		</button>
	</div>

	{#if queue.error}
		<div class="error-panel" role="alert" data-testid="dl-error">{queue.error}</div>
	{/if}

	<div class="body">
		{#if !queue.loaded && queue.loading}
			<Loading loading={true} delay={150} />
		{:else if queue.loaded && queue.entries.length === 0}
			<div class="empty-state" data-testid="dl-empty">
				<div class="glyph">
					<svg
						width="26"
						height="26"
						viewBox="0 0 16 16"
						fill="none"
						stroke="currentColor"
						stroke-width="1.2"
						aria-hidden="true"
					>
						<path d="M2 5.5L8 2l6 3.5v5L8 14l-6-3.5z" stroke-linejoin="round" />
						<path d="M5 6.5l3 1.8 3-1.8" stroke-linejoin="round" />
					</svg>
				</div>
				<div class="et">The dead-letter queue is empty</div>
				<div class="es">
					{isSocket
						? 'Failed socket pushes will appear here for inspection and replay.'
						: 'Failed player updates will appear here for inspection and replay.'}
				</div>
			</div>
		{:else if queue.entries.length > 0}
			<table class="dl-table" data-testid="dl-table">
				<thead>
					<tr>
						<th class="c">
							<input
								type="checkbox"
								class="dl-check"
								bind:this={selectAllEl}
								checked={queue.allVisibleSelected}
								data-testid="dl-select-all"
								aria-label="Select all visible entries"
								onchange={() => queue.setAllVisible(!queue.allVisibleSelected)}
							/>
						</th>
						<th class="r">#</th>
						<th>Reason</th>
						<th>{isSocket ? 'Command' : 'Event type'}</th>
						<th class="r">Player</th>
						<th class="c">Payload</th>
					</tr>
				</thead>
				<tbody>
					{#each queue.entries as entry (`${queue.generation}-${entry.index}`)}
						<DeadLetterRow
							{entry}
							selected={queue.isSelected(entry.index)}
							columns={COLUMN_COUNT}
							onToggle={() => queue.toggle(entry.index)}
						/>
					{/each}
				</tbody>
			</table>
			{#if queue.hasMore}
				<div class="dl-more-note" data-testid="dl-more-note">
					Showing the first {queue.entries.length} of {queue.totalCount}. Replay individual entries from this page, or
					use <strong>Replay all</strong> to drain the entire queue.
				</div>
			{/if}
		{/if}
	</div>
</div>

<script lang="ts">
import { onMount, untrack } from 'svelte';
import Loading from '$components/Loading.svelte';
import { confirmModal, toastError, toastSuccess } from '$stores';
import './ops-console.scss';
import DeadLetterRow from './DeadLetterRow.svelte';
import { DeadLetterConsoleState, type DeadLetterQueueVariant } from './dead-letters.svelte';

interface Props {
	variant?: DeadLetterQueueVariant;
}

const { variant = 'player-update' }: Props = $props();
const isSocket = $derived(variant === 'socket-command');
const title = $derived(isSocket ? 'Socket Dead Letters' : 'Dead Letters');
const destinationLabel = $derived(isSocket ? "the player's live socket" : 'the player update queue');
const poisonLabel = $derived(isSocket ? 'malformed / unknown command type' : 'malformed / unknown event type');

const COLUMN_COUNT = 6;

// The queue targets whichever variant this instance was mounted with; the caller re-mounts the whole
// component (a nav switch) rather than swapping variants on a live instance, so only the initial value
// matters here — untrack() makes that intentional rather than tripping the "state referenced locally"
// warning a bare read of a reactive prop would.
const queue = new DeadLetterConsoleState(untrack(() => variant));
let selectAllEl = $state<HTMLInputElement>();

onMount(() => {
	void refresh();
});

// The select-all box shows a third "some but not all" state; indeterminate is a DOM property only.
$effect(() => {
	if (selectAllEl) {
		selectAllEl.indeterminate = queue.selectedCount > 0 && !queue.allVisibleSelected;
	}
});

const plural = (count: number) => (count === 1 ? 'entry' : 'entries');

const refresh = async () => {
	if (!(await queue.load())) {
		toastError(queue.error ?? 'Failed to load the dead-letter queue.');
	}
};

const announce = (replayed: number, remaining: number) => {
	toastSuccess(`Replayed ${replayed} ${plural(replayed)}; ${remaining} remaining.`);
};

const replaySelected = async () => {
	const count = queue.selectedCount;
	if (count === 0) {
		return;
	}

	const poison = queue.nonReplayableSelectedCount;
	const poisonNote =
		poison > 0
			? ` ${poison} of them ${poison === 1 ? 'is' : 'are'} non-replayable (${poisonLabel}) and will return to the queue immediately.`
			: '';
	const confirmed = await confirmModal({
		title: 'Replay selected entries',
		body: `Replay ${count} selected ${plural(count)} back onto ${destinationLabel}?${poisonNote}`,
		confirmLabel: 'Replay'
	});
	if (!confirmed) {
		return;
	}

	const result = await queue.replay('selected');
	if (result) {
		announce(result.replayedCount, result.remainingCount);
	} else {
		toastError(queue.error ?? 'Failed to replay dead-letter entries.');
	}
};

const replayAll = async () => {
	const count = queue.totalCount;
	if (count === 0) {
		return;
	}

	const confirmed = await confirmModal({
		title: 'Replay all entries',
		body: `Replay all ${count} ${plural(count)} in the dead-letter queue? Poison entries (${poisonLabel}) will return to the queue immediately.`,
		confirmLabel: 'Replay all'
	});
	if (!confirmed) {
		return;
	}

	const result = await queue.replay('all');
	if (result) {
		announce(result.replayedCount, result.remainingCount);
	} else {
		toastError(queue.error ?? 'Failed to replay dead-letter entries.');
	}
};
</script>

<style lang="scss">
.blurb {
	max-width: 760px;
}
.summary {
	.sel {
		color: var(--accent);
	}
	.more {
		color: var(--text-muted);
	}
}

.dl-table {
	width: 100%;
	border-collapse: collapse;

	thead th {
		font-family: var(--mono);
		font-size: 9px;
		font-weight: 400;
		letter-spacing: 1.3px;
		text-transform: uppercase;
		color: var(--text-muted);
		text-align: left;
		padding: 0 12px 10px;
		border-bottom: 1px solid var(--border-subtle);

		&.c {
			text-align: center;
		}
		&.r {
			text-align: right;
		}
	}
}
.dl-check {
	accent-color: var(--accent);
	cursor: pointer;
	width: 14px;
	height: 14px;
}

.dl-more-note {
	margin-top: 16px;
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-muted);
	letter-spacing: 0.2px;

	strong {
		color: var(--text-secondary);
		font-weight: 500;
	}
}
</style>
