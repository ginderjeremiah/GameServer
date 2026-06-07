/* Loading screen view-model — the boot sequence that fetches the static
   reference data (zones, enemies, items, …) the game needs, surfaced as a
   sequential "manifest" the player can watch tick through.

   The load orchestration lives here as a reactive view-model (mirroring
   `OptionsView`/`InventoryView`); the components only render its state. */

import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { apiSocket } from '$lib/api';
import type { ApiSocketCommandNoRequest, ApiSocketResponseTypes } from '$lib/api/types/api-socket-type-map';
import { staticData } from '$stores';
import { readReferenceCache, writeReferenceCache } from './reference-cache';

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

/* The socket has no built-in per-command timeout; without one a dead or unreachable backend
   would leave the loading screen hanging on a command that never resolves. Bounding each socket
   call surfaces the error/retry UI instead. */
const SOCKET_TIMEOUT_MS = 15000;

const withTimeout = <T>(promise: Promise<T>): Promise<T> =>
	new Promise<T>((resolve, reject) => {
		const timer = setTimeout(() => reject(new Error('Timed out waiting for the server.')), SOCKET_TIMEOUT_MS);
		promise.then(
			(value) => {
				clearTimeout(timer);
				resolve(value);
			},
			(error) => {
				clearTimeout(timer);
				reject(error);
			}
		);
	});

/* One row per reference-data set the loading screen pulls. Each set is loaded over
   the authenticated socket via its `Get*` command and cached in local storage keyed
   by the backend-supplied content version, so a refresh only re-downloads the sets
   whose version changed. Keeping them in a single table keeps the manifest DRY. */
interface RefDataSource {
	key: string;
	label: string;
	// The socket command that loads this set; also the key its version is reported under.
	command: ApiSocketCommandNoRequest;
	// Whether the in-memory store slot is already populated (so a same-session re-mount can skip it).
	loaded: () => boolean;
	// Fetch over the socket and populate the in-memory store.
	load: () => Promise<void>;
	// Populate the in-memory store from a cached payload.
	hydrate: (data: unknown) => void;
	// The current in-memory value, to write to the cache after a fresh load.
	current: () => unknown;
}

/* Builds a typed reference-data source from a store getter/setter and its socket command,
   so each row stays type-checked against the command's response type. */
function refSource<C extends ApiSocketCommandNoRequest>(
	key: string,
	label: string,
	command: C,
	read: () => ApiSocketResponseTypes[C],
	write: (data: ApiSocketResponseTypes[C]) => void
): RefDataSource {
	return {
		key,
		label,
		command,
		loaded: () => read() != null,
		load: async () => write((await withTimeout(apiSocket.sendSocketCommand(command))).data),
		hydrate: (data) => write(data as ApiSocketResponseTypes[C]),
		current: () => read()
	};
}

const REFERENCE_DATA: RefDataSource[] = [
	refSource(
		'zones',
		'Zones',
		'GetZones',
		() => staticData.zones,
		(d) => (staticData.zones = d)
	),
	refSource(
		'enemies',
		'Enemies',
		'GetEnemies',
		() => staticData.enemies,
		(d) => (staticData.enemies = d)
	),
	refSource(
		'items',
		'Items',
		'GetItems',
		() => staticData.items,
		(d) => (staticData.items = d)
	),
	refSource(
		'skills',
		'Skills',
		'GetSkills',
		() => staticData.skills,
		(d) => (staticData.skills = d)
	),
	refSource(
		'itemMods',
		'Item Mods',
		'GetItemMods',
		() => staticData.itemMods,
		(d) => (staticData.itemMods = d)
	),
	refSource(
		'attributes',
		'Attributes',
		'GetAttributes',
		() => staticData.attributes,
		(d) => (staticData.attributes = d)
	),
	refSource(
		'challenges',
		'Challenges',
		'GetChallenges',
		() => staticData.challenges,
		(d) => (staticData.challenges = d)
	),
	refSource(
		'challengeTypes',
		'Challenge Types',
		'GetChallengeTypes',
		() => staticData.challengeTypes,
		(d) => (staticData.challengeTypes = d)
	),
	refSource(
		'statisticTypes',
		'Statistic Types',
		'GetStatisticTypes',
		() => staticData.statisticTypes,
		(d) => (staticData.statisticTypes = d)
	)
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

	// Server-reported content version per set (keyed by socket command), or null when the version
	// check couldn't be reached — in which case the cache is bypassed and every set is fetched fresh.
	// Not reactive: it only drives load orchestration, nothing renders from it.
	private versions: Map<string, string> | null = null;

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
		this.versions = await this.fetchVersions();
		this.resolveCached();

		if (this.items.every((i) => i.status === 'done')) {
			this.finish();
			return;
		}

		await delay(CHECKING_DELAY_MS);

		this.phase = 'loading';
		await this.loadFrom(0);
	}

	/**
	 * Fetches the per-set content versions over the socket. Returns null (cache disabled, fetch
	 * everything fresh) if the call fails, so a transient version-check error never serves stale data.
	 */
	private async fetchVersions(): Promise<Map<string, string> | null> {
		try {
			const response = await withTimeout(apiSocket.sendSocketCommand('GetReferenceDataVersions'));
			// Plain Map: this only drives load orchestration and is never rendered, so it needn't be reactive.
			// eslint-disable-next-line svelte/prefer-svelte-reactivity
			return new Map(response.data.map((v) => [v.command, v.version]));
		} catch {
			return null;
		}
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

			const source = REFERENCE_DATA[i];
			const serverVersion = this.versions.get(source.command);
			if (serverVersion == null) {
				continue;
			}

			const cached = readReferenceCache(source.key);
			if (cached && cached.version === serverVersion) {
				source.hydrate(cached.data);
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
				this.cacheLoaded(i);
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

	/** Persists a freshly-loaded set under the version the server reported for it (if known). */
	private cacheLoaded(index: number): void {
		const source = REFERENCE_DATA[index];
		const version = this.versions?.get(source.command);
		if (version != null) {
			writeReferenceCache(source.key, version, source.current());
		}
	}
}
