import { IInventoryItem, IBattlerAttribute, ELogSetting, EItemCategory } from "$lib/api";
import { player } from "./player.svelte";
import { getTrashItem, Item, newItem } from "$lib/battle";
import { logMessage } from "$lib/engine/log";
import { DelayedAction } from "$lib/common/delayed-action";
import { apiSocket, } from "$lib/api/api-socket";

//Manually putting this here until codegen gets updated to load this
export enum EEquipmentSlot {
   HelmSlot = 0,
   ChestSlot = 1,
   LegSlot = 2,
   BootSlot = 3,
   WeaponSlot = 4,
   AccessorySlot = 5
}

enum ExtraSlotType {
   Trash = -1,
   All = 0,
}

export const ItemSlotType = { ...ExtraSlotType, ...EItemCategory }
export type ItemSlotType = ExtraSlotType | EItemCategory;



export interface InventorySlot {
   item?: Item;
   slotType: ItemSlotType;
   slotNumber: number;
   equippedSlot: boolean;
   canHold: (item?: Item) => boolean;
}

let inventoryItems = $state<InventorySlot[]>([]);
let equippedItems = $state<InventorySlot[]>([]);
let trashSlot = $state<InventorySlot>();
let equipmentStats = $state<IBattlerAttribute[]>([]);
let draggedSlot = $state<InventorySlot>();

export const inventory = {
   get slots() {
      return inventoryItems;
   },
   get equippedSlots() {
      return equippedItems;
   },
   get trashSlot() {
      if (!trashSlot) {
         throw new Error("Inventory data not intialized");
      }
      return trashSlot;
   },
   get draggedSlot() {
      return draggedSlot;
   },
   set draggedSlot(value) {
      draggedSlot = value;
   },
   get equipmentStats() {
      return equipmentStats;
   },
}

export const initializeInventoryItems = () => {
   let i = 0;
   for (const item of player.data.inventoryData.inventory) {
      const slot = $state<InventorySlot>({
         item: item ? newItem(item) : item,
         slotType: ItemSlotType.All,
         slotNumber: i++,
         equippedSlot: false,
         canHold: () => true
      });

      inventoryItems.push(slot);
   }

   i = 0
   for (const [index, item] of player.data.inventoryData.equipped.entries()) {
      const slot = $state<InventorySlot>({
         item: item ? newItem(item) : item,
         slotType: getEquippedSlotType(index),
         slotNumber: i++,
         equippedSlot: true,
         canHold: (i?: Item) => !i || getEquippedSlotType(index) === i.itemCategoryId
      })

      equippedItems.push(slot);
   }

   trashSlot = {
      item: getTrashItem(),
      slotType: ItemSlotType.Trash,
      slotNumber: ItemSlotType.Trash,
      equippedSlot: false,
      canHold: () => true
   }
}

const getEquippedSlotType = (index: number) => {
   switch (index) {
      case EEquipmentSlot.HelmSlot:
         return ItemSlotType.Helm;
      case EEquipmentSlot.ChestSlot:
         return ItemSlotType.Chest;
      case EEquipmentSlot.LegSlot:
         return ItemSlotType.Leg;
      case EEquipmentSlot.BootSlot:
         return ItemSlotType.Boot;
      case EEquipmentSlot.WeaponSlot:
         return ItemSlotType.Weapon;
      case EEquipmentSlot.AccessorySlot:
      default:
         return ItemSlotType.Accessory;
   }
}

export const addInventoryItems = (items: IInventoryItem[]) => {
   items.forEach(invItem => {
      const item = newItem(invItem);
      if (inventoryItems[invItem.inventorySlotNumber].item) {
         const inventorySlotNumber = nextAvailableSlot();
         inventoryItems[inventorySlotNumber].item = item;
         item.inventorySlotNumber = inventorySlotNumber;
         startSave();
      } else {
         inventoryItems[invItem.inventorySlotNumber].item = item;
      }
      logMessage(ELogSetting.Inventory, "You found a " + item.name + "!");
   });
}

export const swapSlots = (slot1: InventorySlot, slot2: InventorySlot) => {
   const item1 = slot1.item;
   const item2 = slot2.item;
   const isTrashDestination = slot2.slotType === ItemSlotType.Trash;

   if (item1 && slot2.canHold(item1) && (isTrashDestination || slot1.canHold(item2))) {
      if (isTrashDestination) {
         slot1.item = undefined;
      } else if (slot1.canHold(item2)) {
         item1.inventorySlotNumber = slot2.slotNumber;
         item1.equipped = slot2.equippedSlot;
         slot2.item = item1;

         if (item2) {
            item2.inventorySlotNumber = slot1.slotNumber;
            item2.equipped = slot1.equippedSlot;
         }

         slot1.item = item2;
      }

      if (slot1.equippedSlot || slot2.equippedSlot) {
         //this.updateEquipmentStats();
         updateInventorySlots();
      } else {
         startSave()
      }
   }
}

const nextAvailableSlot = () => {
   for (let i = 0; i < inventoryItems.length; i++) {
      if (!inventoryItems[i]?.item) {
         return i;
      }
   }
   return inventoryItems.length - 1;
}

const updateInventorySlots = () => {
   const inv = [
      ...inventoryItems.flatMap((slot) => slot.item ? { id: slot.item.id, inventorySlotNumber: slot.item.inventorySlotNumber, equipped: false } : []),
      ...equippedItems.flatMap((slot) => slot.item ? { id: slot.item.id, inventorySlotNumber: slot.item.inventorySlotNumber, equipped: true } : [])
   ];
   delayedSaveAction.cancel();
   apiSocket.sendSocketCommand("UpdateInventorySlots", inv)
}

const delayedSaveAction = new DelayedAction(5000, updateInventorySlots);

const startSave = () => {
   delayedSaveAction.start();
}