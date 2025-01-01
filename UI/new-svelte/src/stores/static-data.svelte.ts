import { IAttribute, IEnemy, IItem, IItemMod, ISkill, IZone } from '$lib/api';

let zones = $state<IZone[]>();
let enemies = $state<IEnemy[]>();
let items = $state<IItem[]>();
let skills = $state<ISkill[]>();
let itemMods = $state<IItemMod[]>();
let attributes = $state<IAttribute[]>();

export const staticData = {
	get zones() {
		return zones as IZone[];
	},
	set zones(value) {
		zones = value;
	},
	get enemies() {
		return enemies as IEnemy[];
	},
	set enemies(value) {
		enemies = value;
	},
	get items() {
		return items as IItem[];
	},
	set items(value) {
		items = value;
	},
	get skills() {
		return skills as ISkill[];
	},
	set skills(value) {
		skills = value;
	},
	get itemMods() {
		return itemMods as IItemMod[];
	},
	set itemMods(value) {
		itemMods = value;
	},
	get attributes() {
		return attributes as IAttribute[];
	},
	set attributes(value) {
		attributes = value;
	},
	get loaded() {
		return zones && enemies && items && skills && itemMods && attributes;
	}
};
