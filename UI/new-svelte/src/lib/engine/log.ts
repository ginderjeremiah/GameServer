import { ELogType } from '$lib/api';
import { playerManager } from './engine';
import { logs } from '$stores';

export interface LogMessage {
	id: number;
	logType: ELogType;
	message: string;
}

let id = (logs()?.[0]?.id ?? -1) + 1;

export const logMessage = (logType: ELogType, message: string) => {
	if (playerManager.logPreferences.find((pref) => pref.id === logType)?.enabled ?? true) {
		if (logs().length >= 40) {
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
