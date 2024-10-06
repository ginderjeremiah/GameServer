import { IBattlerAttribute, IPlayerData } from "$lib/api";

let playerData = $state<IPlayerData>();
let equipmentStats = $state<IBattlerAttribute[]>([]);

export const player = {
   get data() {
      return playerData as IPlayerData;
   },
   set data(value) {
      playerData = value;
   },
   get equipmentStats() {
      return equipmentStats;
   },
   set equipmentStats(value) {
      equipmentStats = value;
   }
}