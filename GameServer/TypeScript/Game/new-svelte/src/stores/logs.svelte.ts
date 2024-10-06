import { LogMessage } from "$lib/engine/log";

const logsData = $state<LogMessage[]>([]);

export const logs = () => logsData;