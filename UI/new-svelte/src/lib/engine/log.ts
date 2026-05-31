import { ELogType } from '$lib/api';
import { playerManager } from './';
import { logs } from '$stores';

export interface LogMessage {
	id: number;
	logType: ELogType;
	message: string;
}

const maxLogEntries = 40;

let id = (logs()?.[0]?.id ?? -1) + 1;

export const logMessage = (logType: ELogType, message: string) => {
	if (playerManager.logPreferences.find((pref) => pref.id === logType)?.enabled ?? true) {
		if (logs().length >= maxLogEntries) {
			logs().pop();
		}
		id++;
		logs().unshift({
			id,
			logType,
			message
		});
	}
};
