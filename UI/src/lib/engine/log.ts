import { ELogType } from '$lib/api';
import { playerManager } from './player/player-manager';
import { addLog } from '$stores';

export interface LogMessage {
	id: number;
	logType: ELogType;
	message: string;
}

export const logMessage = (logType: ELogType, message: string) => {
	if (playerManager.logTypeEnabled(logType)) {
		addLog(logType, message);
	}
};
