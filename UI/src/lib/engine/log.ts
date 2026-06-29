import { ELogType } from '$lib/api';
import { playerManager } from './player/player-manager';
import { addLog } from '$stores';
import type { LogOutcome, ResistOutcome } from '$lib/common';

// `LogOutcome` is defined in `$lib/common/damage-log` (so the pure message builder can type off it
// without `$lib/common` depending on `$lib/engine`); re-exported here for this module's existing importers.
export type { LogOutcome, ResistOutcome } from '$lib/common';

export interface LogMessage {
	id: number;
	logType: ELogType;
	message: string;
	/** Epoch-ms wall-clock time the event was logged, so the panel shows when it actually happened. */
	timestamp: number;
	/** Optional outcome for `Damage` entries; absent for every other channel. */
	outcome?: LogOutcome;
	/** Optional damage-type resist outcome for `Damage` entries (resisted / vulnerable / absorbed); absent
	 *  for an unresisted hit and every other channel, so `logKind` only tags the lines that need it. */
	resist?: ResistOutcome;
}

export const logMessage = (logType: ELogType, message: string, outcome?: LogOutcome, resist?: ResistOutcome) => {
	if (playerManager.logTypeEnabled(logType)) {
		addLog(logType, message, outcome, resist);
	}
};
