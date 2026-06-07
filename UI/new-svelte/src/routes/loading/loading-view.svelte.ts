/* Loading screen view-model — the boot sequence that fetches the static reference data (zones,
   enemies, items, …) the game needs, surfaced as a sequential "manifest" the player can watch tick
   through.

   The load orchestration lives here as a reactive view-model (mirroring `OptionsView`/`InventoryView`);
   the components only render its state. The reference-data table and the version/cache plumbing it
   drives are shared with the silent session-resume path, so they live in `$lib/engine/reference-data`. */

import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import {
	REFERENCE_DATA,
	cacheSet,
	dedupedFetch,
	fetchReferenceVersions,
	hydrateFromCacheIfCurrent,
	type ReferenceVersions
} from '$lib/engine/reference-data';

export type ItemStatus = 'pending' | 'loading' | 'done' | 'error';
export type Phase = 'checking' | 'loading' | 'done' | 'error';

export interface LoadItem {
	key: string;
	label: string;
	status: ItemStatus;
	durationMs: number;
	error: string | null;
	fetch: () => Promise<number>;
}

const TITLES: Record<Phase, string> = {
	checking: 'Checking for updates.',
	loading: 'Preparing the realm.',
	error: 'Connection failed.',
	done: 'Ready.'
};

/* How long the brief pause lasts between completed sets so the manifest reads as a sequence. */
const STEP_DELAY_MS = 80;

const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

export class LoadingView {
	items = $state<LoadItem[]>([]);
	phase = $state<Phase>('checking');
	activeIndex = $state(-1);

	// Server-reported content version per set (keyed by socket command), or null when the version
	// check couldn't be reached — in which case the cache is bypassed and every set is fetched fresh.
	// Not reactive: it only drives load orchestration, nothing renders from it.
	private versions: ReferenceVersions | null = null;

	readonly completed = $derived(this.items.filter((i) => i.status === 'done').length);
	readonly progressPct = $derived(this.items.length ? (this.completed / this.items.length) * 100 : 0);
	readonly currentItem = $derived(
		this.activeIndex >= 0 && this.activeIndex < this.items.length ? this.items[this.activeIndex] : null
	);
	readonly title = $derived(TITLES[this.phase]);

	/** Build the manifest and run the boot sequence. */
	async start(): Promise<void> {
		this.items = REFERENCE_DATA.map((src) => ({
			key: src.key,
			label: src.label,
			status: src.loaded() ? 'done' : 'pending',
			durationMs: 0,
			error: null,
			fetch: () => dedupedFetch(src.key, src.load)
		}));

		// Everything already in memory (same-session re-mount) — no version check needed.
		if (this.items.every((i) => i.status === 'done')) {
			this.finish();
			return;
		}

		// Ask the server for the current version of every set, then resolve from the local-storage
		// cache anything whose version still matches, leaving only genuinely stale sets to fetch.
		this.phase = 'checking';
		this.versions = await fetchReferenceVersions();
		this.resolveCached();

		if (this.items.every((i) => i.status === 'done')) {
			this.finish();
			return;
		}

		this.phase = 'loading';
		await this.loadFrom(0);
	}

	/** Hydrates from the local-storage cache every not-yet-loaded set whose cached version is current. */
	private resolveCached(): void {
		if (!this.versions) {
			return;
		}

		for (let i = 0; i < REFERENCE_DATA.length; i++) {
			if (this.items[i].status === 'done') {
				continue;
			}

			if (hydrateFromCacheIfCurrent(REFERENCE_DATA[i], this.versions)) {
				this.items[i].status = 'done';
			}
		}
	}

	/** Re-run the failed (current) set and continue from there. */
	async retryFailed(): Promise<void> {
		if (this.phase !== 'error' || this.activeIndex < 0) {
			return;
		}
		this.items[this.activeIndex].status = 'loading';
		this.items[this.activeIndex].error = null;
		this.phase = 'loading';
		await this.loadFrom(this.activeIndex);
	}

	enterGame(): void {
		if (this.phase === 'done') {
			goto(resolve('/game'));
		}
	}

	private finish(): void {
		this.phase = 'done';
		this.activeIndex = this.items.length;
	}

	private async loadFrom(startIdx: number): Promise<void> {
		for (let i = startIdx; i < this.items.length; i++) {
			if (this.items[i].status === 'done') {
				continue;
			}

			this.activeIndex = i;
			this.items[i].status = 'loading';
			this.items[i].error = null;

			try {
				this.items[i].durationMs = await this.items[i].fetch();
				this.items[i].status = 'done';
				cacheSet(REFERENCE_DATA[i], this.versions);
			} catch (e) {
				this.items[i].status = 'error';
				this.items[i].error = e instanceof Error ? e.message : 'Network error — could not reach server.';
				this.phase = 'error';
				return;
			}

			await delay(STEP_DELAY_MS);
		}

		this.finish();
	}
}
