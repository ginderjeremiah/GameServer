import {
	ApiRequest,
	EAttribute,
	EEntityType,
	EItemCategory,
	EItemModType,
	EModifierType,
	ERarity,
	ESkillEffectTarget,
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

/** A zero-based-id reference record that can be retired (kept at its slot, resolvable by id). */
type RetireableRef = { id: number; name: string; retiredAt?: string | null };

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
		// post-save reads stay fresh because every admin write reloads these in-memory caches
		// server-side (AdminCacheReloadFilter). Tags have no socket command yet, so they
		// remain on HTTP.
		const [enemies, skills, zones, items, itemMods, tags, tagCategories, challengeTypes, challenges] =
			await Promise.all([
				fetchSocketData('GetEnemies'),
				fetchSocketData('GetSkills'),
				fetchSocketData('GetZones'),
				fetchSocketData('GetItems'),
				fetchSocketData('GetItemMods'),
				ApiRequest.get('Tags'),
				ApiRequest.get('Tags/TagCategories'),
				fetchSocketData('GetChallengeTypes'),
				fetchSocketData('GetChallenges')
			]);
		staticData.enemies = enemies;
		staticData.skills = skills;
		staticData.zones = zones;
		staticData.items = items;
		staticData.itemMods = itemMods;
		staticData.challenges = challenges;
		this.tags = tags;
		this.tagCategories = tagCategories;
		this.challengeTypes = challengeTypes;
		this.loaded = true;
	}

	// ── Select options ──
	/**
	 * Build select options from a retireable reference set: active records are always
	 * selectable, but a retired record is offered only when it is the current value
	 * (`keep`). This excludes retired records from new authoring (a hard block — they
	 * are never in a fresh list) while keeping an already-authored reference visible
	 * (marked "· retired") and editable, instead of silently blanking it. Once the
	 * author changes away from a kept retired value it leaves the list, so it can't be
	 * re-picked.
	 */
	private retireableOptions = <T extends RetireableRef>(
		records: T[],
		keep: number | undefined,
		label: (r: T) => string = (r) => r.name
	): SelectOption[] =>
		records
			.filter((r) => !r.retiredAt || r.id === keep)
			.map((r) => ({ value: r.id, text: r.retiredAt ? `${label(r)} · retired` : label(r) }));

	attributeOptions = (): SelectOption[] => toOptions(enumPairs(EAttribute));
	itemCategoryOptions = (): SelectOption[] => toOptions(enumPairs(EItemCategory));
	rarityOptions = (): SelectOption[] => toOptions(enumPairs(ERarity));
	modTypeOptions = (): SelectOption[] => toOptions(enumPairs(EItemModType));
	skillEffectTargetOptions = (): SelectOption[] => toOptions(enumPairs(ESkillEffectTarget));
	modifierTypeOptions = (): SelectOption[] => toOptions(enumPairs(EModifierType));
	tagCategoryOptions = (): SelectOption[] => this.tagCategories.map((c) => ({ value: c.id, text: c.name }));
	zoneOptions = (keep?: number): SelectOption[] =>
		this.retireableOptions(staticData.zones ?? [], keep, (z) => `${z.name} · L${z.levelMin}–${z.levelMax}`);
	enemyOptions = (keep?: number): SelectOption[] => this.retireableOptions(staticData.enemies ?? [], keep);
	/** Dedicated-boss picker options: a "None" sentinel (-1) plus every active boss enemy. */
	bossEnemyOptions = (keep?: number): SelectOption[] => [
		{ value: -1, text: 'None' },
		...this.retireableOptions(
			(staticData.enemies ?? []).filter((e) => e.isBoss),
			keep
		)
	];
	/** Zone unlock-gate picker options: a "None" sentinel (-1) plus every active challenge. */
	unlockChallengeOptions = (keep?: number): SelectOption[] => [
		{ value: -1, text: 'None (always open)' },
		...this.retireableOptions(staticData.challenges ?? [], keep)
	];
	skillCatalogue = () =>
		(staticData.skills ?? []).map((s) => ({
			id: s.id,
			name: s.name,
			baseDamage: s.baseDamage,
			retired: !!s.retiredAt
		}));

	// ── Challenges ──
	challengeTypeOptions = (): SelectOption[] => this.challengeTypes.map((t) => ({ value: t.id, text: t.name }));
	challengeTypeById = (id: number) => this.challengeTypes.find((t) => t.id === id);

	/** The zero-based-id reference set backing an entity dimension (indexable by id). */
	private entitySource = (entityType: EEntityType): RetireableRef[] | undefined => {
		switch (entityType) {
			case EEntityType.Enemy:
				return staticData.enemies;
			case EEntityType.Zone:
				return staticData.zones;
			case EEntityType.Skill:
				return staticData.skills;
			default:
				return undefined;
		}
	};

	/**
	 * The reference records backing a statistic's entity dimension (Enemy / Zone / Skill).
	 * When `bossOnly` is set (a boss-only statistic such as BossesDefeated) the Enemy
	 * dimension is restricted to enemies flagged `isBoss`, so the editor can't author a
	 * challenge against a non-boss that the statistic would never increment for.
	 */
	private entityRecords = (entityType: EEntityType, bossOnly: boolean): RetireableRef[] =>
		entityType === EEntityType.Enemy
			? (staticData.enemies ?? []).filter((e) => !bossOnly || e.isBoss)
			: (this.entitySource(entityType) ?? []);

	entityCatalog = (entityType: EEntityType, bossOnly = false): { id: number; name: string }[] =>
		this.entityRecords(entityType, bossOnly).map((e) => ({ id: e.id, name: e.name }));
	/** Target-entity picker options. Retired records are excluded unless `keep` is the current target. */
	entityOptions = (entityType: EEntityType, bossOnly = false, keep?: number): SelectOption[] =>
		this.retireableOptions(this.entityRecords(entityType, bossOnly), keep);
	entityName = (entityType: EEntityType, id: number): string | null =>
		this.entitySource(entityType)?.[id]?.name ?? null;

	// ── Reward lookups (item / item-mod records the challenge reward picker reads) ──
	itemRecords = () => staticData.items ?? [];
	itemModRecords = () => staticData.itemMods ?? [];
	itemRecName = (id: number) => staticData.items?.[id]?.name;
	itemRarityId = (id: number) => staticData.items?.[id]?.rarityId;
	itemRetired = (id: number) => !!staticData.items?.[id]?.retiredAt;
	itemModName = (id: number) => staticData.itemMods?.[id]?.name;
	itemModRetired = (id: number) => !!staticData.itemMods?.[id]?.retiredAt;
	itemModTypeName = (id: number) => {
		const mod = staticData.itemMods?.[id];
		return mod ? this.modTypeName(mod.itemModTypeId) : undefined;
	};
	skillRecords = () => staticData.skills ?? [];
	skillName = (id: number) => staticData.skills?.[id]?.name;
	skillBaseDamage = (id: number) => staticData.skills?.[id]?.baseDamage;
	skillRetired = (id: number) => !!staticData.skills?.[id]?.retiredAt;

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
