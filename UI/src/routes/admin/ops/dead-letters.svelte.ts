import { ApiRequest, EDeadLetterReason, type IDeadLetterEntry, type IDeadLetterReplayResult } from '$lib/api';
import { SvelteSet } from 'svelte/reactivity';

/** Head-first page size requested per inspection (the backend clamps at 500). */
export const DEAD_LETTER_PAGE_SIZE = 200;

/**
 * Which dead-letter queue a console instance targets. Both queues share the same backend contracts
 * (`DeadLetterEntry`/`DeadLetterInspection`/`DeadLetterReplayResult`) and this same state class — only the
 * routes differ, so a new queue is a new variant here rather than a duplicated component (#1542).
 */
export type DeadLetterQueueVariant = 'player-update' | 'socket-command';

interface QueueRoutes {
	inspect: 'AdminTools/GetPlayerUpdateDeadLetters' | 'AdminTools/GetSocketCommandDeadLetters';
	replay: 'AdminTools/ReplayPlayerUpdateDeadLetters' | 'AdminTools/ReplaySocketCommandDeadLetters';
}

const QUEUE_ROUTES: Record<DeadLetterQueueVariant, QueueRoutes> = {
	'player-update': {
		inspect: 'AdminTools/GetPlayerUpdateDeadLetters',
		replay: 'AdminTools/ReplayPlayerUpdateDeadLetters'
	},
	'socket-command': {
		inspect: 'AdminTools/GetSocketCommandDeadLetters',
		replay: 'AdminTools/ReplaySocketCommandDeadLetters'
	}
};

/** How a classified dead-letter reason is surfaced to an operator. */
export interface ReasonMeta {
	label: string;
	/** One-line operator hint — why the entry is here and whether replaying it is worthwhile. */
	hint: string;
	/** True only when replaying the entry can plausibly succeed; the rest just re-dead-letter. */
	replayable: boolean;
	/** Semantic tone the badge/styling keys off (never a hard-coded colour). */
	tone: 'poison' | 'warn' | 'ok';
}

const REASON_META: Record<EDeadLetterReason, ReasonMeta> = {
	[EDeadLetterReason.Malformed]: {
		label: 'Malformed',
		hint: 'Could not be parsed — replaying it will just re-fail.',
		replayable: false,
		tone: 'poison'
	},
	[EDeadLetterReason.UnknownEventType]: {
		label: 'Unknown event type',
		hint: 'Nothing recognizes this type — replaying it will just re-fail.',
		replayable: false,
		tone: 'warn'
	},
	[EDeadLetterReason.Replayable]: {
		label: 'Replayable',
		hint: 'Exhausted its retries or delivery attempts — safe to replay once the cause is fixed.',
		replayable: true,
		tone: 'ok'
	},
	[EDeadLetterReason.NotReplayable]: {
		label: 'Not replayable',
		hint: 'Only meaningful at the moment it was originally emitted — replaying it now would act on stale intent.',
		replayable: false,
		tone: 'warn'
	}
};

/** Display metadata for a classified dead-letter reason. */
export const reasonMeta = (reason: EDeadLetterReason): ReasonMeta =>
	REASON_META[reason] ?? { label: 'Unknown', hint: '', replayable: false, tone: 'warn' };

/**
 * Pretty-prints a raw dead-letter payload as indented JSON for inspection, falling back to the
 * verbatim string when it is not valid JSON (e.g. a malformed entry).
 */
export const formatPayload = (raw: string): string => {
	try {
		return JSON.stringify(JSON.parse(raw), null, 2);
	} catch {
		return raw;
	}
};

/**
 * View-model for the admin dead-letter ops console: holds the inspected queue snapshot and the
 * operator's selection, and drives the inspect/replay endpoints. Selection is keyed by each entry's
 * queue {@link IDeadLetterEntry.index} (unique per position) rather than its raw payload, because the
 * backend replays by payload honouring duplicate multiplicity — collapsing the selection onto payloads
 * would silently drop one copy of two identical entries.
 */
export class DeadLetterConsoleState {
	totalCount = $state(0);
	entries = $state<IDeadLetterEntry[]>([]);
	loading = $state(false);
	replaying = $state(false);
	loaded = $state(false);
	error = $state<string | null>(null);
	readonly selected = new SvelteSet<number>();

	/**
	 * Bumped on every successful {@link load}. `entry.index` is only a queue position, reused by whatever
	 * entry now sits there, so the console keys rows by `generation` + `index` rather than `index` alone —
	 * otherwise a row's local expanded-payload state would leak onto a different entry after a refresh.
	 */
	generation = $state(0);

	private readonly routes: QueueRoutes;

	constructor(variant: DeadLetterQueueVariant = 'player-update') {
		this.routes = QUEUE_ROUTES[variant];
	}

	get selectedCount(): number {
		return this.selected.size;
	}

	/** True when the inspected page is a partial view of a deeper queue. */
	get hasMore(): boolean {
		return this.totalCount > this.entries.length;
	}

	get allVisibleSelected(): boolean {
		return this.entries.length > 0 && this.entries.every((entry) => this.selected.has(entry.index));
	}

	get selectedEntries(): IDeadLetterEntry[] {
		return this.entries.filter((entry) => this.selected.has(entry.index));
	}

	/** Selected payloads in queue order — duplicates preserved so replay multiplicity is honoured. */
	get selectedPayloads(): string[] {
		return this.selectedEntries.map((entry) => entry.rawPayload);
	}

	/** How many selected entries are genuinely poison (will immediately return to the dead-letter queue). */
	get nonReplayableSelectedCount(): number {
		return this.selectedEntries.filter((entry) => !reasonMeta(entry.reason).replayable).length;
	}

	isSelected(index: number): boolean {
		return this.selected.has(index);
	}

	toggle(index: number): void {
		if (this.selected.has(index)) {
			this.selected.delete(index);
		} else {
			this.selected.add(index);
		}
	}

	setAllVisible(on: boolean): void {
		if (on) {
			for (const entry of this.entries) {
				this.selected.add(entry.index);
			}
		} else {
			this.clearSelection();
		}
	}

	clearSelection(): void {
		this.selected.clear();
	}

	/** Inspects the queue (non-destructive) and replaces the snapshot, clearing any stale selection. */
	async load(): Promise<boolean> {
		this.loading = true;
		this.error = null;
		try {
			const inspection = await ApiRequest.get(this.routes.inspect, {
				max: DEAD_LETTER_PAGE_SIZE
			});
			this.totalCount = inspection.totalCount;
			this.entries = inspection.entries;
			this.generation++;
			this.clearSelection();
			this.loaded = true;
			return true;
		} catch (ex) {
			this.error = ex instanceof Error ? ex.message : 'Failed to load the dead-letter queue.';
			return false;
		} finally {
			this.loading = false;
		}
	}

	/**
	 * Replays either every queued entry or just the current selection, then refreshes the snapshot.
	 * Returns the backend's replayed/remaining counts, or null on failure.
	 */
	async replay(scope: 'all' | 'selected'): Promise<IDeadLetterReplayResult | null> {
		this.replaying = true;
		this.error = null;
		try {
			const result = await ApiRequest.post(this.routes.replay, {
				all: scope === 'all',
				payloads: scope === 'selected' ? this.selectedPayloads : undefined
			});
			// The replay itself succeeded (entries are durably re-enqueued); a failed refresh afterwards
			// must not raise the blocking error panel and contradict the success result. Swallow it — the
			// returned counts are accurate and the operator can Refresh to re-sync the now-stale list.
			if (!(await this.load())) {
				this.error = null;
			}
			return result;
		} catch (ex) {
			this.error = ex instanceof Error ? ex.message : 'Failed to replay dead-letter entries.';
			return null;
		} finally {
			this.replaying = false;
		}
	}
}
