/* Loading screen view-model — the boot sequence that fetches the static
   reference data (zones, enemies, items, …) the game needs, surfaced as a
   sequential "manifest" the player can watch tick through.

   The load orchestration lives here as a reactive view-model (mirroring
   `OptionsView`/`InventoryView`); the components only render its state. */

import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { ApiRequest } from '$lib/api';
import { staticData } from '$stores';

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

/* One row per reference-data set the loading screen pulls. `loaded` reports
   whether the in-memory store slot is already populated (so a refresh can skip
   it) and `load` performs the typed fetch + store assignment. Keeping them in a
   single table keeps the manifest definition DRY. */
interface RefDataSource {
	key: string;
	label: string;
	loaded: () => boolean;
	load: () => Promise<void>;
}

const REFERENCE_DATA: RefDataSource[] = [
	{
		key: 'zones',
		label: 'Zones',
		loaded: () => staticData.zones != null,
		load: async () => {
			staticData.zones = await ApiRequest.get('Zones');
		}
	},
	{
		key: 'enemies',
		label: 'Enemies',
		loaded: () => staticData.enemies != null,
		load: async () => {
			staticData.enemies = await ApiRequest.get('Enemies');
		}
	},
	{
		key: 'items',
		label: 'Items',
		loaded: () => staticData.items != null,
		load: async () => {
			staticData.items = await ApiRequest.get('Items');
		}
	},
	{
		key: 'skills',
		label: 'Skills',
		loaded: () => staticData.skills != null,
		load: async () => {
			staticData.skills = await ApiRequest.get('Skills');
		}
	},
	{
		key: 'itemMods',
		label: 'Item Mods',
		loaded: () => staticData.itemMods != null,
		load: async () => {
			staticData.itemMods = await ApiRequest.get('ItemMods');
		}
	},
	{
		key: 'attributes',
		label: 'Attributes',
		loaded: () => staticData.attributes != null,
		load: async () => {
			staticData.attributes = await ApiRequest.get('Attributes');
		}
	},
	{
		key: 'challenges',
		label: 'Challenges',
		loaded: () => staticData.challenges != null,
		load: async () => {
			staticData.challenges = await ApiRequest.get('Challenges');
		}
	},
	{
		key: 'challengeTypes',
		label: 'Challenge Types',
		loaded: () => staticData.challengeTypes != null,
		load: async () => {
			staticData.challengeTypes = await ApiRequest.get('Challenges/ChallengeTypes');
		}
	},
	{
		key: 'statisticTypes',
		label: 'Statistic Types',
		loaded: () => staticData.statisticTypes != null,
		load: async () => {
			staticData.statisticTypes = await ApiRequest.get('Statistics/StatisticTypes');
		}
	}
];

/* How long the "checking cache" beat lingers before loading starts, and the
   brief pause between completed sets so the manifest reads as a sequence. */
const CHECKING_DELAY_MS = 500;
const STEP_DELAY_MS = 80;

// De-duplicates concurrent fetches of the same set (e.g. a re-mount mid-load)
// so any one key is only ever in flight once.
// eslint-disable-next-line svelte/prefer-svelte-reactivity
const pendingFetches = new Map<string, Promise<number>>();

const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

/** Runs `run` for `key` at most once concurrently, timing the call in ms. */
const dedupedFetch = (key: string, run: () => Promise<void>): Promise<number> => {
	let pending = pendingFetches.get(key);
	if (!pending) {
		const start = performance.now();
		pending = run()
			.then(() => Math.round(performance.now() - start))
			.catch((e) => {
				pendingFetches.delete(key);
				throw e;
			});
		pendingFetches.set(key, pending);
	}
	return pending;
};

export class LoadingView {
	items = $state<LoadItem[]>([]);
	phase = $state<Phase>('checking');
	activeIndex = $state(-1);

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

		// Everything already cached — skip straight to done.
		if (this.items.every((i) => i.status === 'done')) {
			this.finish();
			return;
		}

		this.phase = 'checking';
		await delay(CHECKING_DELAY_MS);

		this.phase = 'loading';
		await this.loadFrom(0);
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
