/* Player statistics store — the player's tracked statistic values
   (`GetPlayerStatistics` socket command), shared across screens so the source is fetched
   once and stays consistent. The Statistics screen renders the full breakdown from it; the
   Fight screen reads the per-zone `ZonesCleared` value to show the boss "Cleared"
   seal and marks a zone cleared optimistically the moment its boss is defeated. */

import { fetchSocketData, EStatisticType, type IPlayerStatistic } from '$lib/api';
import { CoalescedLoader } from '$lib/common/coalesced-loader';

let stats = $state<IPlayerStatistic[]>([]);
let loaded = $state(false);
let error = $state(false);

const fetchStats = async () => {
	const epoch = loader.currentEpoch;
	try {
		// fetchSocketData throws on a socket error, preserving this try/catch contract.
		const result = (await fetchSocketData('GetPlayerStatistics')) ?? [];
		if (loader.isStale(epoch)) {
			// Either reset() ran while this fetch was in flight (a discarded session), or markZoneCleared()
			// applied a push mid-flight — this response predates that push and would revert it.
			return;
		}
		stats = result;
		error = false;
		loaded = true;
	} catch {
		// A failed load is not a genuine empty result; leave the seal hidden and let
		// the next load retry. The Statistics screen surfaces the error to the user.
		if (!loader.isStale(epoch)) {
			error = true;
		}
	}
};

/** Coalesces concurrent callers (game boot + screen mount) onto one request; a forced load
 *  issued mid-flight chains a fresh fetch so it never resolves with stale data. */
const loader = new CoalescedLoader(fetchStats, () => loaded);

export const statistics = {
	get stats() {
		return stats;
	},
	get loaded() {
		return loaded;
	},
	get error() {
		return error;
	},

	/** Fetch the player's statistics. Idempotent — only hits the network the first
	 *  time unless `force` re-fetches the latest values (e.g. on the stats screen). */
	async load(force = false) {
		await loader.load(force);
	},

	/** Whether the given zone has been cleared at least once (per-zone `ZonesCleared` > 0). */
	isZoneCleared(zoneId: number) {
		return stats.some((s) => s.statisticTypeId === EStatisticType.ZonesCleared && s.entityId === zoneId && s.value > 0);
	},

	/** Optimistically record a zone clear so the "Cleared" seal appears immediately on a boss
	 *  victory; the authoritative value is reconciled on the next load. A fetch already in flight is
	 *  invalidated so its response (computed before this push landed) can't revert the clear. */
	markZoneCleared(zoneId: number) {
		if (this.isZoneCleared(zoneId)) {
			return;
		}
		const idx = stats.findIndex((s) => s.statisticTypeId === EStatisticType.ZonesCleared && s.entityId === zoneId);
		if (idx >= 0) {
			stats = stats.map((s, i) => (i === idx ? { ...s, value: 1 } : s));
		} else {
			stats = [...stats, { statisticTypeId: EStatisticType.ZonesCleared, entityId: zoneId, value: 1 }];
		}
		loader.invalidate();
	},

	/** Reset to the unloaded state (e.g. on logout / session replacement). */
	reset() {
		stats = [];
		loaded = false;
		error = false;
		loader.reset();
	}
};
