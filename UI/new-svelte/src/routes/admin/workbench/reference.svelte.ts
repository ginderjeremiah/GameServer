import {
	ApiRequest,
	EAttribute,
	EEntityType,
	EItemCategory,
	EItemModType,
	ERarity,
	fetchSocketData,
	type IChallengeType,
	type IItem,
	type IItemMod,
	type ITag,
	type ITagCategory
} from '$lib/api';
import { enumPairs, rarityColor as rarityVar, rarityLabel } from '$lib/common';
import { staticData } from '$stores';
import type { SelectOption } from './entities/types';

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
	challengeTypes = $state<IChallengeType[]>([]);
	loaded = $state(false);

	async load() {
		// Reference data is read over the socket (the loading screen's transport); the admin's
		// post-save reads stay fresh because every admin write invalidates these in-memory caches
		// server-side (AdminCacheInvalidationFilter). Tags have no socket command yet, so they
		// remain on HTTP.
		const [enemies, skills, zones, items, itemMods, tags, tagCategories, challengeTypes] = await Promise.all([
			fetchSocketData('GetEnemies'),
			fetchSocketData('GetSkills'),
			fetchSocketData('GetZones'),
			fetchSocketData('GetItems'),
			fetchSocketData('GetItemMods'),
			ApiRequest.get('Tags'),
			ApiRequest.get('Tags/TagCategories'),
			fetchSocketData('GetChallengeTypes')
		]);
		staticData.enemies = enemies;
		staticData.skills = skills;
		staticData.zones = zones;
		staticData.items = items;
		staticData.itemMods = itemMods;
		this.tags = tags;
		this.tagCategories = tagCategories;
		this.challengeTypes = challengeTypes;
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

	// ── Challenges ──
	challengeTypeOptions = (): SelectOption[] => this.challengeTypes.map((t) => ({ value: t.id, text: t.name }));
	challengeTypeById = (id: number) => this.challengeTypes.find((t) => t.id === id);

	/** Target-entity catalogue for a statistic's entity dimension (Enemy / Zone / Skill). */
	entityCatalog = (entityType: EEntityType): { id: number; name: string }[] => {
		const source =
			entityType === EEntityType.Enemy
				? staticData.enemies
				: entityType === EEntityType.Zone
					? staticData.zones
					: entityType === EEntityType.Skill
						? staticData.skills
						: [];
		return (source ?? []).map((e) => ({ id: e.id, name: e.name }));
	};
	entityOptions = (entityType: EEntityType): SelectOption[] =>
		this.entityCatalog(entityType).map((e) => ({ value: e.id, text: e.name }));
	entityName = (entityType: EEntityType, id: number): string | null =>
		this.entityCatalog(entityType).find((e) => e.id === id)?.name ?? null;

	// ── Reward lookups (item / item-mod records the challenge reward picker reads) ──
	itemRecords = () => staticData.items ?? [];
	itemModRecords = () => staticData.itemMods ?? [];
	itemRecName = (id: number) => (staticData.items ?? []).find((i) => i.id === id)?.name;
	itemRarityId = (id: number) => (staticData.items ?? []).find((i) => i.id === id)?.rarityId;
	itemModName = (id: number) => (staticData.itemMods ?? []).find((m) => m.id === id)?.name;
	itemModTypeName = (id: number) => {
		const mod = (staticData.itemMods ?? []).find((m) => m.id === id);
		return mod ? this.modTypeName(mod.itemModTypeId) : undefined;
	};

	// ── Name lookups ──
	itemCategoryName = (id: number) => EItemCategory[id] ?? '';
	rarityName = (id: number) => rarityLabel(id as ERarity);
	modTypeName = (id: number) => EItemModType[id] ?? '';
	rarityColor = (id: number) => rarityVar(id as ERarity);

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
