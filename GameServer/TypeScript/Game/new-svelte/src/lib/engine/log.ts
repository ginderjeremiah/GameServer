import { ELogSetting } from "$lib/api"
import { player } from "$stores";
import { logs } from "$stores/logs";
import { get } from "svelte/store";

export interface LogMessage {
   id: number
   logType: ELogSetting;
   message: string;
}

let id = 0;

export const logMessage = (logType: ELogSetting, message: string) => {
   console.log(message);
   if (player.value.logPreferences.find(pref => pref.id === logType)?.enabled ?? true) {
      const newLogs = logs.value.slice();
      if (newLogs.length >= 40) {
         newLogs.shift();
      }
      id++;
      newLogs.push({
         id,
         logType,
         message
      });
      logs.set(newLogs);
   }
}