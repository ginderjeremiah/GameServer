import { IInventoryItem, IBattlerAttribute, ELogSetting, EItemCategory, apiSocket } from '$lib/api';
import { playerManager } from '$lib/engine';
import { getTrashItem, Item, newItem } from '$lib/battle';
import { logMessage } from '$lib/engine/log';
import { DelayedAction } from '$lib/common';

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
	All = 0
}

export const ItemSlotType = { ...ExtraSlotType, ...EItemCategory };
export type ItemSlotType = ExtraSlotType | EItemCategory;

export interface InventorySlot {
	item?: Item;
	slotType: ItemSlotType;
	slotNumber: number;
	equippedSlot: boolean;
	canHold: (item?: Item) => boolean;
}

export class InventoryManager {
	public slots: InventorySlot[] = [];
	public equippedSlots: InventorySlot[] = [];
	public trashSlot: InventorySlot = {} as any;
	public draggedSlot?: InventorySlot;
	public equipmentStats: IBattlerAttribute[] = [];

	private delayedSaveAction = new DelayedAction(5000, () => this.updateInventorySlots());

	public initialize() {
		let i = 0;
		for (const item of playerManager.inventoryData.inventory) {
			const slot = {
				item: item ? newItem(item) : item,
				slotType: ItemSlotType.All,
				slotNumber: i++,
				equippedSlot: false,
				canHold: () => true
			};

			this.slots.push(slot);
		}

		i = 0;
		for (const [index, item] of playerManager.inventoryData.equipped.entries()) {
			const slot = {
				item: item ? newItem(item) : item,
				slotType: getEquippedSlotType(index),
				slotNumber: i++,
				equippedSlot: true,
				canHold: (i?: Item) => !i || getEquippedSlotType(index) === i.itemCategoryId
			};

			this.equippedSlots.push(slot);
		}

		this.trashSlot = {
			item: getTrashItem(),
			slotType: ItemSlotType.Trash,
			slotNumber: ItemSlotType.Trash,
			equippedSlot: false,
			canHold: () => true
		};
	}

	public addInventoryItems(items: IInventoryItem[]) {
		items.forEach((invItem) => {
			const item = newItem(invItem);
			if (this.slots[invItem.inventorySlotNumber].item) {
				const inventorySlotNumber = this.nextAvailableSlot();
				this.slots[inventorySlotNumber].item = item;
				item.inventorySlotNumber = inventorySlotNumber;
				this.delayedSaveAction.start();
			} else {
				this.slots[invItem.inventorySlotNumber].item = item;
			}
			logMessage(ELogSetting.Inventory, 'You found a ' + item.name + '!');
		});
	}

	public swapSlots(slot1: InventorySlot, slot2: InventorySlot) {
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
				this.updateInventorySlots();
			} else {
				this.delayedSaveAction.start();
			}
		}
	}

	private nextAvailableSlot() {
		for (let i = 0; i < this.slots.length; i++) {
			if (!this.slots[i]?.item) {
				return i;
			}
		}
		return this.slots.length - 1;
	}

	private updateInventorySlots() {
		const inv = [
			...this.slots.flatMap((slot) =>
				slot.item
					? {
							id: slot.item.id,
							inventorySlotNumber: slot.item.inventorySlotNumber,
							equipped: false
						}
					: []
			),
			...this.equippedSlots.flatMap((slot) =>
				slot.item
					? { id: slot.item.id, inventorySlotNumber: slot.item.inventorySlotNumber, equipped: true }
					: []
			)
		];
		this.delayedSaveAction.cancel();
		apiSocket.sendSocketCommand('UpdateInventorySlots', inv);
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
};
