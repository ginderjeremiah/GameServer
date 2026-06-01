import {
	ApiRequest,
	EAttribute,
	EItemCategory,
	EItemModType,
	ERarity,
	type IItem,
	type IItemMod,
	type ITag,
	type ITagCategory
} from '$lib/api';
import { enumPairs } from '$lib/common';
import { staticData } from '$stores';
import type { SelectOption } from './entities/types';

const RARITY_COLOR: Record<number, string> = {
	1: '#9aa0a6',
	2: '#7fc28b',
	3: '#a1c2f7',
	4: '#c0a8e6',
	5: '#f0c078',
	6: '#e08a78'
};

export interface TagColor {
	fg: string;
	bd: string;
	bg: string;
}

const toOptions = (pairs: { id: number; name: string }[]): SelectOption[] =>
	pairs.map((p) => ({ value: p.id, text: p.name }));

/**
 * Loads and exposes the reference data the workbench's select options, tag UI,
 * and derived spawn-share math read from. Catalogues that the workbench also
 * edits (enemies, items, …) live in {@link staticData} and reflect the last
 * saved state; tags/categories are kept here since nothing else needs them yet.
 */
class WorkbenchReference {
	tags = $state<ITag[]>([]);
	tagCategories = $state<ITagCategory[]>([]);
	loaded = $state(false);

	async load() {
		const [enemies, skills, zones, items, itemMods, tags, tagCategories] = await Promise.all([
			ApiRequest.get('Enemies', { refreshCache: true }),
			ApiRequest.get('Skills', { refreshCache: true }),
			ApiRequest.get('Zones', { refreshCache: true }),
			ApiRequest.get('Items', { refreshCache: true }),
			ApiRequest.get('ItemMods', { refreshCache: true }),
			ApiRequest.get('Tags'),
			ApiRequest.get('Tags/TagCategories')
		]);
		staticData.enemies = enemies;
		staticData.skills = skills;
		staticData.zones = zones;
		staticData.items = items;
		staticData.itemMods = itemMods;
		this.tags = tags;
		this.tagCategories = tagCategories;
		this.loaded = true;
	}

	// ── Select options ──
	attributeOptions = (): SelectOption[] => toOptions(enumPairs(EAttribute));
	itemCategoryOptions = (): SelectOption[] => toOptions(enumPairs(EItemCategory));
	rarityOptions = (): SelectOption[] => toOptions(enumPairs(ERarity));
	modTypeOptions = (): SelectOption[] => toOptions(enumPairs(EItemModType));
	tagCategoryOptions = (): SelectOption[] => this.tagCategories.map((c) => ({ value: c.id, text: c.name }));
	zoneOptions = (): SelectOption[] =>
		staticData.zones.map((z) => ({ value: z.id, text: `${z.name} · L${z.levelMin}–${z.levelMax}` }));
	enemyOptions = (): SelectOption[] => staticData.enemies.map((e) => ({ value: e.id, text: e.name }));
	skillCatalogue = () => staticData.skills.map((s) => ({ id: s.id, name: s.name, baseDamage: s.baseDamage }));

	// ── Name lookups ──
	itemCategoryName = (id: number) => EItemCategory[id] ?? '';
	rarityName = (id: number) => ERarity[id] ?? '';
	modTypeName = (id: number) => EItemModType[id] ?? '';
	rarityColor = (id: number) => RARITY_COLOR[id] ?? '#9aa0a6';

	// ── Tags ──
	tagById = (id: number) => this.tags.find((t) => t.id === id);
	tagsByCategory = (catId: number) => this.tags.filter((t) => t.tagCategoryId === catId);

	/** Deterministic, well-spread low-chroma hue per category so the set is scannable. */
	tagColor = (catId: number): TagColor => {
		const hue = Math.round((catId * 137.508) % 360);
		return {
			fg: `oklch(0.85 0.06 ${hue})`,
			bd: `oklch(0.85 0.06 ${hue} / 0.45)`,
			bg: `oklch(0.85 0.06 ${hue} / 0.12)`
		};
	};

	/** Items and mods that reference a tag — drives the read-only Tags "Usage" tab. */
	tagUsage = (tagId: number) => {
		const itemList = (staticData.items ?? []).filter((it: IItem) => it.tags.includes(tagId));
		const modList = (staticData.itemMods ?? []).filter((m: IItemMod) => m.tags.includes(tagId));
		return { items: itemList.length, mods: modList.length, itemList, modList };
	};

	/**
	 * Total spawn weight competing in a zone: this row's weight plus every OTHER
	 * enemy's weight in the same zone (so an enemy's share reflects zone competition,
	 * not its own zone list).
	 */
	enemySpawnShareTotal = (row: Record<string, number>, _rows: Record<string, number>[], record: unknown): number => {
		const enemyId = (record as { id: number }).id;
		let sum = row.weight || 0;
		for (const enemy of staticData.enemies ?? []) {
			if (enemy.id === enemyId) {
				continue;
			}
			const spawn = enemy.spawns?.find((s) => s.zoneId === row.zoneId);
			if (spawn) {
				sum += spawn.weight || 0;
			}
		}
		return sum || 1;
	};
}

export const reference = new WorkbenchReference();
