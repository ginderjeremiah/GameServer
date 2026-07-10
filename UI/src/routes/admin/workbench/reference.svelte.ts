import {
	ApiRequest,
	EAttribute,
	EDamageType,
	EDamageTypeKey,
	EEntityType,
	EEquipmentSlot,
	EItemCategory,
	EItemModType,
	ELessonTriggerType,
	EMechanicEvent,
	EModifierType,
	ERarity,
	ESkillAcquisition,
	ESkillEffectTarget,
	fetchSocketData,
	type IChallengeType,
	type IItem,
	type IItemMod,
	type ITag,
	type ITagCategory
} from '$lib/api';
import { enumPairs, hasFlag, rarityColor as rarityVar, rarityLabel, tagColor } from '$lib/common';
import { staticData } from '$stores';
import type { SelectOption } from './entities/types';

const toOptions = (pairs: { id: number; name: string }[]): SelectOption[] =>
	pairs.map((p) => ({ value: p.id, text: p.name }));

/** A zero-based-id reference record that can be retired (kept at its slot, resolvable by id). */
type RetireableRef = { id: number; name: string; retiredAt?: string | null };

/** Synthetic, never-retired refs for the fixed EDamageTypeKey enum — the EEntityType.DamageType
 *  dimension has no DB reference table to load, so its "records" are just the enum itself. */
const damageTypeKeyRefs: RetireableRef[] = enumPairs(EDamageTypeKey);

/** Mirrors the backend's `EquipmentSlot.ItemCategory` mapping (`Game.Core/Players/Inventories/EquipmentSlot.cs`). */
const equipmentSlotCategories: Record<EEquipmentSlot, EItemCategory> = {
	[EEquipmentSlot.HelmSlot]: EItemCategory.Helm,
	[EEquipmentSlot.ChestSlot]: EItemCategory.Chest,
	[EEquipmentSlot.LegSlot]: EItemCategory.Leg,
	[EEquipmentSlot.BootSlot]: EItemCategory.Boot,
	[EEquipmentSlot.WeaponSlot]: EItemCategory.Weapon,
	[EEquipmentSlot.AccessorySlot]: EItemCategory.Accessory
};

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
		const [
			enemies,
			skills,
			zones,
			items,
			itemMods,
			attributes,
			tags,
			tagCategories,
			challengeTypes,
			challenges,
			paths,
			proficiencies,
			lessons,
			classes,
			skillRecipes
		] = await Promise.all([
			fetchSocketData('GetEnemies'),
			fetchSocketData('GetSkills'),
			fetchSocketData('GetZones'),
			fetchSocketData('GetItems'),
			fetchSocketData('GetItemMods'),
			// The attribute reference set backs the searchable picker's by-type grouping; the admin can be
			// entered directly (without the game's loading screen), so it must be loaded here too.
			fetchSocketData('GetAttributes'),
			ApiRequest.get('Tags'),
			ApiRequest.get('Tags/TagCategories'),
			fetchSocketData('GetChallengeTypes'),
			fetchSocketData('GetChallenges'),
			fetchSocketData('GetPaths'),
			fetchSocketData('GetProficiencies'),
			fetchSocketData('GetLessons'),
			// Referenced (not authored) via this loader too — retire-confirm's reference computation
			// reads staticData.classes/skillRecipes, and the admin can be entered directly (#1633).
			fetchSocketData('GetClasses'),
			fetchSocketData('GetSkillRecipes')
		]);
		staticData.enemies = enemies;
		staticData.skills = skills;
		staticData.zones = zones;
		staticData.items = items;
		staticData.itemMods = itemMods;
		staticData.attributes = attributes;
		staticData.challenges = challenges;
		staticData.paths = paths;
		staticData.proficiencies = proficiencies;
		staticData.lessons = lessons;
		staticData.classes = classes;
		staticData.skillRecipes = skillRecipes;
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
	/** Attribute picker that also offers a "None" sentinel (-1), for an optional scaling attribute. */
	optionalAttributeOptions = (): SelectOption[] => [{ value: -1, text: 'None' }, ...this.attributeOptions()];
	equipmentSlotOptions = (): SelectOption[] => toOptions(enumPairs(EEquipmentSlot));
	itemCategoryOptions = (): SelectOption[] => toOptions(enumPairs(EItemCategory));
	rarityOptions = (): SelectOption[] => toOptions(enumPairs(ERarity));
	/** Leaf damage-type picker options (the eight EDamageType leaf types) for the skill editor. */
	damageTypeOptions = (): SelectOption[] => toOptions(enumPairs(EDamageType));
	/**
	 * Item weapon-type picker options: a "None" sentinel (-1) plus every damage-type leaf — any leaf is a
	 * valid WeaponType, so a caster weapon can declare its element (e.g. Fire) rather than a martial leaf. A
	 * weapon's WeaponType is required (with a granted skill); the backend enforces both on save as anti-tamper,
	 * along with the granted skill's own type being fieldable with this weapon.
	 */
	weaponTypeOptions = (): SelectOption[] => [{ value: -1, text: 'None' }, ...toOptions(enumPairs(EDamageType))];
	modTypeOptions = (): SelectOption[] => toOptions(enumPairs(EItemModType));
	skillEffectTargetOptions = (): SelectOption[] => toOptions(enumPairs(ESkillEffectTarget));
	modifierTypeOptions = (): SelectOption[] => toOptions(enumPairs(EModifierType));
	lessonTriggerTypeOptions = (): SelectOption[] => toOptions(enumPairs(ELessonTriggerType));
	mechanicEventOptions = (): SelectOption[] => toOptions(enumPairs(EMechanicEvent));
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
	/**
	 * Item granted-skill picker options: a "None" sentinel (-1) plus every active Item-flagged skill
	 * (the backend enforces the flag too). The current value stays visible — even if it's retired or no
	 * longer Item-flagged — so an already-authored grant isn't silently dropped from the list.
	 */
	grantedSkillOptions = (keep?: number): SelectOption[] => [
		{ value: -1, text: 'None' },
		...this.retireableOptions(
			(staticData.skills ?? []).filter((s) => hasFlag(s.acquisition, ESkillAcquisition.Item) || s.id === keep),
			keep
		)
	];
	/** Item picker options (active items, plus the current value even if retired). */
	itemOptions = (keep?: number): SelectOption[] => this.retireableOptions(staticData.items ?? [], keep);
	/** The item category an equipment slot accepts, mirroring the backend's `EquipmentSlot.ItemCategory`. */
	equipmentSlotCategory = (slot: EEquipmentSlot): EItemCategory => equipmentSlotCategories[slot];
	/**
	 * Item picker options narrowed to the category a given equipment slot accepts (plus the current
	 * value even if it's since become a category mismatch — the class starter-equipment table's
	 * section `warn` is the backstop for that case, mirroring the retired-`keep` convention above).
	 */
	itemOptionsForSlot = (slot: EEquipmentSlot, keep?: number): SelectOption[] => {
		const category = this.equipmentSlotCategory(slot);
		return this.retireableOptions(
			(staticData.items ?? []).filter((i) => i.itemCategoryId === category || i.id === keep),
			keep
		);
	};
	/** An item's category, for validating it against the equipment slot it's assigned to. */
	itemCategoryOf = (id: number): EItemCategory | undefined => staticData.items?.[id]?.itemCategoryId;
	skillCatalogue = () =>
		(staticData.skills ?? []).map((s) => ({
			id: s.id,
			name: s.name,
			baseDamage: s.baseDamage,
			acquisition: s.acquisition,
			retired: !!s.retiredAt
		}));
	/**
	 * Synthesis recipe result-skill picker: active skills flagged {@link ESkillAcquisition.Synthesis}
	 * (a hard block on a non-Synthesis result, mirroring the backend authoring guard). The current value
	 * stays visible even if it lost the flag or was retired, so an already-authored result isn't silently
	 * dropped — the recipe editor surfaces that stale value as a warning instead.
	 */
	synthesisResultSkillOptions = (keep?: number): SelectOption[] =>
		this.retireableOptions(
			(staticData.skills ?? []).filter((s) => hasFlag(s.acquisition, ESkillAcquisition.Synthesis) || s.id === keep),
			keep
		);
	/** Whether a skill id resolves to a live (non-retired) Synthesis-flagged skill — a valid recipe result. */
	isSynthesisResult = (id: number): boolean => {
		const skill = staticData.skills?.[id];
		return !!skill && !skill.retiredAt && hasFlag(skill.acquisition, ESkillAcquisition.Synthesis);
	};

	// ── Progression (paths & proficiencies) ──
	/**
	 * Player-acquirable skills, with a "None" sentinel (-1). Backs the milestone-reward picker, which the
	 * backend restricts to `ESkillAcquisition.Player` skills. The current value stays visible even if it
	 * lost the flag or was retired, so an existing grant isn't silently dropped.
	 */
	playerSkillOptions = (keep?: number): SelectOption[] => [
		{ value: -1, text: 'None' },
		...this.retireableOptions(
			(staticData.skills ?? []).filter((s) => hasFlag(s.acquisition, ESkillAcquisition.Player) || s.id === keep),
			keep
		)
	];
	/** Active proficiencies (plus the current value if retired) — the cross-path prerequisite picker. */
	proficiencyOptions = (keep?: number): SelectOption[] => this.retireableOptions(staticData.proficiencies ?? [], keep);
	/**
	 * Item equip-gate picker options: a "None" sentinel (-1) plus active proficiencies (the current value
	 * stays visible even if retired). Backs the item `RequiredProficiency` gate; the backend rejects a
	 * retired or out-of-range gate on save.
	 */
	requiredProficiencyOptions = (keep?: number): SelectOption[] => [
		{ value: -1, text: 'None (ungated)' },
		...this.retireableOptions(staticData.proficiencies ?? [], keep)
	];
	pathName = (id: number) => staticData.paths?.[id]?.name;
	proficiencyName = (id: number) => staticData.proficiencies?.[id]?.name;

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
			case EEntityType.DamageType:
				return damageTypeKeyRefs;
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

	/** Themeable tag-category accent trio (procedural hue, `--tag-*` lightness/chroma/alpha). */
	tagColor = (catId: number) => tagColor(catId);

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
	enemySpawnShareTotal = (
		row: Record<string, number | string>,
		_rows: Record<string, number | string>[],
		record: unknown
	): number => {
		const enemyId = (record as { id: number }).id;
		let sum = Number(row.weight) || 0;
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
