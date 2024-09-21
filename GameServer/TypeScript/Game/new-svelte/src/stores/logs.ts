import { writableEx } from "$lib/common";
import { LogMessage } from "$lib/engine/log";


export const logs = writableEx<LogMessage[]>([]);