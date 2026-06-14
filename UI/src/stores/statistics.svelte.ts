/* Player statistics store — the player's tracked statistic values
   (`GetPlayerStatistics` socket command), shared across screens so the source is fetched
   once and stays consistent. The Statistics screen renders the full breakdown from it; the
   Fight screen reads the per-zone `ZonesCleared` value to show the boss "Cleared"
   seal and marks a zone cleared optimistically the moment its boss is defeated. */

import { fetchSocketData, EStatisticType, type IPlayerStatistic } from '$lib/api';

let stats = $state<IPlayerStatistic[]>([]);
let loaded = $state(false);
let error = $state(false);
/** Coalesces concurrent callers (game boot + screen mount) onto one request. */
let inFlight: Promise<void> | undefined;

const fetchStats = async () => {
	try {
		// fetchSocketData throws on a socket error, preserving this try/catch contract.
		stats = (await fetchSocketData('GetPlayerStatistics')) ?? [];
		error = false;
		loaded = true;
	} catch {
		// A failed load is not a genuine empty result; leave the seal hidden and let
		// the next load retry. The Statistics screen surfaces the error to the user.
		error = true;
	}
};

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
		if (loaded && !force) {
			return;
		}
		if (!inFlight) {
			inFlight = fetchStats().finally(() => {
				inFlight = undefined;
			});
		}
		await inFlight;
	},

	/** Whether the given zone has been cleared at least once (per-zone `ZonesCleared` > 0). */
	isZoneCleared(zoneId: number) {
		return stats.some((s) => s.statisticTypeId === EStatisticType.ZonesCleared && s.entityId === zoneId && s.value > 0);
	},

	/** Optimistically record a zone clear so the "Cleared" seal appears immediately
	 *  on a boss victory; the authoritative value is reconciled on the next load. */
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
	},

	/** Reset to the unloaded state (e.g. on logout / session replacement). */
	reset() {
		stats = [];
		loaded = false;
		error = false;
		inFlight = undefined;
	}
};
