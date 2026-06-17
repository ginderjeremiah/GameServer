<div class="dl-console" data-testid="dead-letter-console">
	<div class="dl-head">
		<div class="eyebrow">Admin Console · Ops</div>
		<div class="title-row">
			<h1 class="title" data-testid="dl-title">Dead Letters</h1>
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
			Player write-behind events that failed to apply. Inspect the classified failure reason before replaying — <em
				>malformed</em
			>
			and <em>unknown event type</em> entries are poison and will return to the queue immediately.
		</p>
	</div>

	<div class="dl-toolbar">
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
		<div class="dl-error" role="alert" data-testid="dl-error">{queue.error}</div>
	{/if}

	<div class="dl-body">
		{#if !queue.loaded && queue.loading}
			<Loading loading={true} delay={150} />
		{:else if queue.loaded && queue.entries.length === 0}
			<div class="dl-empty" data-testid="dl-empty">
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
				<div class="es">Failed player updates will appear here for inspection and replay.</div>
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
						<th>Event type</th>
						<th class="r">Player</th>
						<th class="c">Payload</th>
					</tr>
				</thead>
				<tbody>
					{#each queue.entries as entry (entry.index)}
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
import { onMount } from 'svelte';
import Loading from '$components/Loading.svelte';
import { confirmModal, toastError, toastSuccess } from '$stores';
import DeadLetterRow from './DeadLetterRow.svelte';
import { DeadLetterConsoleState } from './dead-letters.svelte';

const COLUMN_COUNT = 6;

const queue = new DeadLetterConsoleState();
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
			? ` ${poison} of them ${poison === 1 ? 'is' : 'are'} non-replayable (malformed / unknown event type) and will return to the queue immediately.`
			: '';
	const confirmed = await confirmModal({
		title: 'Replay selected entries',
		body: `Replay ${count} selected ${plural(count)} back onto the player update queue?${poisonNote}`,
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
		body: `Replay all ${count} ${plural(count)} in the dead-letter queue? Poison entries (malformed / unknown event type) will return to the queue immediately.`,
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
.dl-console {
	display: flex;
	flex-direction: column;
	height: 100%;
	min-height: 0;
	font-family: var(--sans);
	-webkit-font-smoothing: antialiased;
	-moz-osx-font-smoothing: grayscale;
}

.dl-head {
	padding: 20px 32px 16px;
	border-bottom: 1px solid var(--border-subtle);
}
.eyebrow {
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--accent) 70%, transparent);
	margin-bottom: 6px;
}
.title-row {
	display: flex;
	align-items: baseline;
	gap: 14px;
	flex-wrap: wrap;
}
.title {
	margin: 0;
	font-size: 22px;
	font-weight: 500;
	letter-spacing: -0.2px;
}
.summary {
	display: inline-flex;
	align-items: center;
	gap: 14px;
	font-family: var(--mono);
	font-size: 11.5px;
	color: var(--text-tertiary);

	.sel {
		color: var(--accent);
	}
	.more {
		color: var(--text-muted);
	}
}
.blurb {
	margin: 12px 0 0;
	max-width: 760px;
	font-size: 12.5px;
	line-height: 1.55;
	color: var(--text-tertiary);

	em {
		font-style: normal;
		color: var(--text-secondary);
	}
}

.dl-toolbar {
	display: flex;
	align-items: center;
	gap: 10px;
	padding: 14px 32px;
	border-bottom: 1px solid var(--border-subtle);

	.spacer {
		flex: 1;
	}
}
.btn {
	display: inline-flex;
	align-items: center;
	gap: 7px;
	background: transparent;
	border: 1px solid var(--border-light);
	color: var(--text-secondary);
	font-family: var(--mono);
	font-size: 11.5px;
	letter-spacing: 0.6px;
	text-transform: uppercase;
	padding: 8px 15px;
	border-radius: 3px;
	cursor: pointer;
	transition: all 0.14s ease;
	white-space: nowrap;

	&:hover:not(:disabled) {
		border-color: color-mix(in srgb, var(--white) 32%, transparent);
		box-shadow: 0 0 10px color-mix(in srgb, var(--accent) 40%, transparent);
	}
	&.primary {
		background: color-mix(in srgb, var(--accent) 12%, transparent);
		border-color: var(--accent);
		color: var(--accent-light);
	}
	&.primary:hover:not(:disabled) {
		box-shadow: 0 0 12px color-mix(in srgb, var(--accent) 50%, transparent);
	}
	&:disabled {
		color: var(--text-muted);
		border-color: var(--border-subtle);
		cursor: not-allowed;
		box-shadow: none;
	}
}

.dl-error {
	margin: 14px 32px 0;
	padding: 11px 14px;
	border: 1px solid color-mix(in srgb, var(--error) 45%, transparent);
	background: color-mix(in srgb, var(--error) 10%, transparent);
	border-radius: 4px;
	color: var(--error);
	font-size: 12.5px;
}

.dl-body {
	flex: 1;
	min-height: 0;
	overflow: auto;
	padding: 12px 32px 28px;
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

.dl-empty {
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	text-align: center;
	padding: 60px 20px;
	color: var(--text-muted);
	gap: 12px;

	.glyph {
		width: 56px;
		height: 56px;
		border-radius: 10px;
		border: 1px dashed var(--border-light);
		display: flex;
		align-items: center;
		justify-content: center;
		color: var(--text-tertiary);
	}
	.et {
		font-size: 14px;
		color: var(--text-tertiary);
	}
	.es {
		font-size: 12px;
	}
}
</style>
