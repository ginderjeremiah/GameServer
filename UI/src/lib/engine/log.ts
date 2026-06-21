import { ELogType } from '$lib/api';
import { playerManager } from './player/player-manager';
import { addLog } from '$stores';

/**
 * Structured combat-outcome discriminator carried alongside a `Damage`-channel entry. The battle
 * engine knows each hit's outcome explicitly (player vs enemy, crit/dodge/block) at the log site, so
 * it sets this rather than encoding the outcome only in the prose — letting `logKind` pick the glyph
 * from a typed value instead of sniffing the message text, which would silently drift on a reword.
 */
export type LogOutcome = 'player-hit' | 'player-crit' | 'player-dodge' | 'player-block' | 'enemy-hit';

export interface LogMessage {
	id: number;
	logType: ELogType;
	message: string;
	/** Epoch-ms wall-clock time the event was logged, so the panel shows when it actually happened. */
	timestamp: number;
	/** Optional outcome for `Damage` entries; absent for every other channel. */
	outcome?: LogOutcome;
}

export const logMessage = (logType: ELogType, message: string, outcome?: LogOutcome) => {
	if (playerManager.logTypeEnabled(logType)) {
		addLog(logType, message, outcome);
	}
};
