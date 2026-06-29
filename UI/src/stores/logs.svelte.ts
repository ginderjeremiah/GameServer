import { LogMessage, LogOutcome, ResistOutcome } from '$lib/engine/log';
import { ELogType } from '$lib/api';

const maxLogEntries = 40;

const logsData = $state<LogMessage[]>([]);
// 1-based running count of entries this session; doubles as each entry's monotonic id and
// backs the log panel's total-events readout.
let lastId = 0;

export const logs = () => logsData;

/** Prepend a combat-log entry (newest-first), assigning its id and capping the list length. */
export const addLog = (logType: ELogType, message: string, outcome?: LogOutcome, resist?: ResistOutcome) => {
	if (logsData.length >= maxLogEntries) {
		logsData.pop();
	}
	logsData.unshift({ id: ++lastId, logType, message, timestamp: Date.now(), outcome, resist });
};

/** Clear the combat log and its id counter (e.g. on logout / session replacement) so the
 *  previous session's entries don't leak into the next one. */
export const resetLogs = () => {
	logsData.length = 0;
	lastId = 0;
};
