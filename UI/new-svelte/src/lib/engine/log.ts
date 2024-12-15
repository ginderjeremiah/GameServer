import { ELogSetting } from '$lib/api';
import { player } from '$stores';
import { logs } from '$stores';

export interface LogMessage {
	id: number;
	logType: ELogSetting;
	message: string;
}

let id = 0;

export const logMessage = (logType: ELogSetting, message: string) => {
	if (player.data.logPreferences.find((pref) => pref.id === logType)?.enabled ?? true) {
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
