import { IInventoryItem } from "$lib/api";

export enum EItemSlotType {
   Trash = -1,
   All = 0,
   Helm = 1,
   Chest = 2,
   Leg = 3,
   Boot = 4,
   Weapon = 5,
   Accessory = 6
}

export class Inventory {
   addItems(items: IInventoryItem[]) {

   }
}