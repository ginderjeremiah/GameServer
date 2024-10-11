import { IBattlerAttribute, IPlayerData } from "$lib/api";
import { Item } from "$lib/battle";

let playerData = $state<IPlayerData>();
let inventoryItems = $state<(Item | undefined)[]>([]);
let equippedItems = $state<(Item | undefined)[]>([]);
let equipmentStats = $state<IBattlerAttribute[]>([]);

const maxInventoryItems = 27;

export const loadPlayerData = (data: IPlayerData) => {
   playerData = data;

}

export const player = {
   get data() {
      return playerData as IPlayerData;
   },
   get equipmentStats() {
      return equipmentStats;
   },
}

const initializeInventoryItems = (data: IPlayerData) => {
   for (const item of data.inventoryData.inventory) {

   }

   for (const item of data.inventoryData.equipped) {

   }
}