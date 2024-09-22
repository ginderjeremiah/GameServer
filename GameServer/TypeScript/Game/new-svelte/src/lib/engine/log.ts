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
      if (logs.value.length >= 40) {
         logs.value.pop();
      }
      id++;
      logs.value.unshift({
         id,
         logType,
         message
      });
      logs.refresh();
   }
}