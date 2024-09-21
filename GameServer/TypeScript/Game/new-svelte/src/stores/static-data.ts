import { IAttribute, IEnemy, IItem, IItemMod, ISkill, IZone } from "$lib/api";
import { writableEx } from "$lib/common";

export const zones = writableEx<IZone[]>();
export const enemies = writableEx<IEnemy[]>();
export const items = writableEx<IItem[]>();
export const skills = writableEx<ISkill[]>();
export const itemMods = writableEx<IItemMod[]>();
export const attributes = writableEx<IAttribute[]>();