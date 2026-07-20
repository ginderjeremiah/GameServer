import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
	EAttribute,
	EChallengeGoalComparison,
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
	ESkillEffectTarget
} from '$lib/api';

// WorkbenchReference reads its catalogues from the in-memory staticData store, so
// it is mocked here (mirrors statistics-view.test.ts). The tags/categories/challenge-types
// it owns are set directly on the singleton's reactive fields.
const { staticData } = vi.hoisted(() => ({
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));
vi.mock('$stores', () => ({ staticData }));

import { reference } from '$routes/admin/workbench/reference.svelte';

beforeEach(() => {
	staticData.enemies = [
		{ id: 0, name: 'Cave Bat', isBoss: false, spawns: [{ zoneId: 0, weight: 5 }] },
		{ id: 1, name: 'Catacomb Lich', isBoss: true, spawns: [{ zoneId: 0, weight: 15 }] },
		{
			id: 2,
			name: 'Goblin',
			isBoss: false,
			spawns: [
				{ zoneId: 0, weight: 30 },
				{ zoneId: 1, weight: 5 }
			]
		},
		{ id: 3, name: 'Ancient Wyrm', isBoss: true, spawns: [] }
	];
	staticData.zones = [
		{ id: 0, name: 'Verdant Hollow', levelMin: 1, levelMax: 5 },
		{ id: 1, name: 'Frost Cavern', levelMin: 6, levelMax: 10 }
	];
	staticData.skills = [
		{ id: 0, name: 'Cleave', baseDamage: 12 },
		{ id: 1, name: 'Fireball', baseDamage: 25 }
	];
	staticData.items = [
		{ id: 0, name: 'Iron Helm', itemCategoryId: EItemCategory.Helm, rarityId: ERarity.Common, tags: [10, 20] },
		{ id: 1, name: 'Dragon Blade', itemCategoryId: EItemCategory.Weapon, rarityId: ERarity.Legendary, tags: [10] }
	];
	staticData.itemMods = [
		{ id: 0, name: 'Sharp', itemModTypeId: EItemModType.Prefix, rarityId: ERarity.Rare, tags: [10] },
		{ id: 1, name: 'of Power', itemModTypeId: EItemModType.Suffix, rarityId: ERarity.Epic, tags: [30] }
	];
	staticData.challenges = [
		{ id: 0, name: 'First Blood' },
		{ id: 1, name: 'Boss Slayer' }
	];
	reference.tags = [
		{ id: 10, name: 'Fire', tagCategoryId: 100 },
		{ id: 20, name: 'Metal', tagCategoryId: 100 },
		{ id: 30, name: 'Holy', tagCategoryId: 200 }
	];
	reference.tagCategories = [
		{ id: 100, name: 'Element' },
		{ id: 200, name: 'School' }
	];
	reference.challengeTypes = [
		{ id: 1, name: 'Enemies Killed', goalComparison: EChallengeGoalComparison.AtLeast },
		{ id: 2, name: 'Bosses Defeated', goalComparison: EChallengeGoalComparison.AtLeast }
	];
});

describe('enum-backed select options', () => {
	it('builds attribute options from the EAttribute enum', () => {
		const opts = reference.attributeOptions();
		expect(opts.find((o) => o.value === 0)?.text).toBe('Strength');
		expect(opts.find((o) => o.value === 6)?.text).toBe('Max Health');
	});

	it('exposes the crit/dodge attributes for gear/item-mod authoring', () => {
		// Now that crit/dodge are sourced (#799) rather than unused, the item/item-mod attribute
		// pickers — which read this same enum-backed option set — must offer them.
		const opts = reference.attributeOptions();
		const byValue = (value: EAttribute) => opts.find((o) => o.value === value)?.text;
		expect(byValue(EAttribute.CriticalChanceMultiplier)).toBe('Critical Chance Multiplier');
		expect(byValue(EAttribute.CriticalDamage)).toBe('Critical Damage');
		expect(byValue(EAttribute.DodgeChance)).toBe('Dodge Chance');
	});

	it('exposes the authored-only DamageReflection for gear/mod/proficiency/class authoring (#1330)', () => {
		// DamageReflection has no core-attribute derivation, so authoring is the ONLY way to grant it. Every
		// attribute picker (gear / item-mod / proficiency / class) reads this enum-backed set, so it must offer
		// DamageReflection despite the retired Block gap in the enum.
		const opts = reference.attributeOptions();
		expect(opts.find((o) => o.value === EAttribute.DamageReflection)?.text).toBe('Damage Reflection');
	});

	it('builds item-category, rarity and mod-type options', () => {
		expect(reference.itemCategoryOptions().find((o) => o.value === EItemCategory.Weapon)?.text).toBe('Weapon');
		expect(reference.rarityOptions().find((o) => o.value === ERarity.Legendary)?.text).toBe('Legendary');
		expect(reference.modTypeOptions().find((o) => o.value === EItemModType.Prefix)?.text).toBe('Prefix');
	});

	it('builds skill-effect-target and modifier-type options from their enums', () => {
		expect(reference.skillEffectTargetOptions().find((o) => o.value === ESkillEffectTarget.Self)?.text).toBe('Self');
		expect(reference.skillEffectTargetOptions().find((o) => o.value === ESkillEffectTarget.Opponent)?.text).toBe(
			'Opponent'
		);
		expect(reference.modifierTypeOptions().find((o) => o.value === EModifierType.Additive)?.text).toBe('Additive');
		expect(reference.modifierTypeOptions().find((o) => o.value === EModifierType.Multiplicative)?.text).toBe(
			'Multiplicative'
		);
	});

	it('builds lesson-trigger-type and mechanic-event options from their enums', () => {
		expect(reference.lessonTriggerTypeOptions().find((o) => o.value === ELessonTriggerType.ScreenVisit)?.text).toBe(
			'Screen Visit'
		);
		expect(reference.lessonTriggerTypeOptions().find((o) => o.value === ELessonTriggerType.MechanicEvent)?.text).toBe(
			'Mechanic Event'
		);
		expect(reference.mechanicEventOptions().find((o) => o.value === EMechanicEvent.FirstCrit)?.text).toBe('First Crit');
		expect(reference.mechanicEventOptions().find((o) => o.value === EMechanicEvent.FirstDodge)?.text).toBe(
			'First Dodge'
		);
	});
});

describe('reference-data-backed select options', () => {
	it('labels zone options with their level range', () => {
		expect(reference.zoneOptions()).toEqual([
			{ value: 0, text: 'Verdant Hollow · L1–5' },
			{ value: 1, text: 'Frost Cavern · L6–10' }
		]);
	});

	it('lists every enemy as an enemy option', () => {
		expect(reference.enemyOptions().map((o) => o.text)).toEqual([
			'Cave Bat',
			'Catacomb Lich',
			'Goblin',
			'Ancient Wyrm'
		]);
	});

	it('prefixes the boss picker with a None sentinel and lists only bosses', () => {
		expect(reference.bossEnemyOptions()).toEqual([
			{ value: -1, text: 'None' },
			{ value: 1, text: 'Catacomb Lich' },
			{ value: 3, text: 'Ancient Wyrm' }
		]);
	});

	it('keeps the current boss picker value visible after it loses the isBoss flag (#1996)', () => {
		// Cave Bat (id 0) isn't a boss, but it's the zone's currently-authored value — it must
		// stay selectable/visible rather than silently vanishing from the option list.
		expect(reference.bossEnemyOptions(0)).toContainEqual({ value: 0, text: 'Cave Bat' });
		// Dropping `keep` (or picking a different current value) excludes it again.
		expect(reference.bossEnemyOptions().map((o) => o.value)).not.toContain(0);
	});

	it('prefixes the unlock-gate picker with an always-open sentinel and lists every challenge', () => {
		expect(reference.unlockChallengeOptions()).toEqual([
			{ value: -1, text: 'None (always open)' },
			{ value: 0, text: 'First Blood' },
			{ value: 1, text: 'Boss Slayer' }
		]);
	});

	it('exposes the skill catalogue with base damage', () => {
		expect(reference.skillCatalogue()).toEqual([
			{ id: 0, name: 'Cleave', baseDamage: 12, retired: false },
			{ id: 1, name: 'Fireball', baseDamage: 25, retired: false }
		]);
	});

	it('falls back to empty lists when a catalogue is absent', () => {
		staticData.zones = undefined;
		staticData.enemies = undefined;
		staticData.challenges = undefined;
		staticData.skills = undefined;
		expect(reference.zoneOptions()).toEqual([]);
		expect(reference.enemyOptions()).toEqual([]);
		expect(reference.bossEnemyOptions()).toEqual([{ value: -1, text: 'None' }]);
		expect(reference.unlockChallengeOptions()).toEqual([{ value: -1, text: 'None (always open)' }]);
		expect(reference.skillCatalogue()).toEqual([]);
	});
});

describe('retired records in selectable options', () => {
	beforeEach(() => {
		// Retire one record per zero-based-id set (kept at its slot, resolvable by id).
		staticData.enemies[2].retiredAt = '2026-01-01T00:00:00Z'; // Goblin (non-boss)
		staticData.enemies[3].retiredAt = '2026-01-01T00:00:00Z'; // Ancient Wyrm (boss)
		staticData.zones[1].retiredAt = '2026-01-01T00:00:00Z'; // Frost Cavern
		staticData.challenges[1].retiredAt = '2026-01-01T00:00:00Z'; // Boss Slayer
		staticData.skills[1].retiredAt = '2026-01-01T00:00:00Z'; // Fireball
		staticData.items[1].retiredAt = '2026-01-01T00:00:00Z'; // Dragon Blade
		staticData.itemMods[1].retiredAt = '2026-01-01T00:00:00Z'; // of Power
	});

	it('omits retired records from a fresh enemy/zone option list', () => {
		expect(reference.enemyOptions().map((o) => o.value)).toEqual([0, 1]);
		expect(reference.zoneOptions().map((o) => o.value)).toEqual([0]);
	});

	it('keeps a retired record visible (marked) when it is the current value', () => {
		expect(reference.enemyOptions(2)).toEqual([
			{ value: 0, text: 'Cave Bat' },
			{ value: 1, text: 'Catacomb Lich' },
			{ value: 2, text: 'Goblin · retired' }
		]);
		expect(reference.zoneOptions(1)).toContainEqual({ value: 1, text: 'Frost Cavern · L6–10 · retired' });
	});

	it('drops a retired record again once the author keeps a different value', () => {
		// keep references an active record, so the retired Goblin is no longer offered.
		expect(reference.enemyOptions(0).map((o) => o.value)).toEqual([0, 1]);
	});

	it('excludes retired bosses / challenges from their sentinel-prefixed pickers', () => {
		expect(reference.bossEnemyOptions().map((o) => o.value)).toEqual([-1, 1]);
		expect(reference.unlockChallengeOptions().map((o) => o.value)).toEqual([-1, 0]);
		// …unless one is the current value.
		expect(reference.bossEnemyOptions(3)).toContainEqual({ value: 3, text: 'Ancient Wyrm · retired' });
		expect(reference.unlockChallengeOptions(1)).toContainEqual({ value: 1, text: 'Boss Slayer · retired' });
	});

	it('excludes a retired target entity from the picker but keeps the current one', () => {
		expect(reference.entityOptions(EEntityType.Skill).map((o) => o.value)).toEqual([0]);
		expect(reference.entityOptions(EEntityType.Skill, false, 1)).toContainEqual({
			value: 1,
			text: 'Fireball · retired'
		});
	});

	it('flags retired skills in the catalogue and resolves retired reward status by id', () => {
		expect(reference.skillCatalogue().find((s) => s.id === 1)?.retired).toBe(true);
		expect(reference.skillRetired(1)).toBe(true);
		expect(reference.skillRetired(0)).toBe(false);
		expect(reference.itemRetired(1)).toBe(true);
		expect(reference.itemRetired(0)).toBe(false);
		expect(reference.itemModRetired(1)).toBe(true);
		expect(reference.itemModRetired(0)).toBe(false);
	});

	it('still resolves a retired record by id for display lookups', () => {
		// entityName / itemRecName remain index-based so an authored reference still renders.
		expect(reference.entityName(EEntityType.Enemy, 2)).toBe('Goblin');
		expect(reference.entityName(EEntityType.Skill, 1)).toBe('Fireball');
	});
});

describe('granted-skill picker options', () => {
	beforeEach(() => {
		// Cleave is Item-grantable, Fireball is Player-only, Smite is Item-grantable but retired.
		staticData.skills = [
			{ id: 0, name: 'Cleave', baseDamage: 12, acquisition: ESkillAcquisition.Item },
			{ id: 1, name: 'Fireball', baseDamage: 25, acquisition: ESkillAcquisition.Player },
			{
				id: 2,
				name: 'Smite',
				baseDamage: 30,
				acquisition: ESkillAcquisition.Item,
				retiredAt: '2026-01-01T00:00:00Z'
			}
		];
	});

	it('offers a None sentinel plus only the active Item-flagged skills', () => {
		expect(reference.grantedSkillOptions()).toEqual([
			{ value: -1, text: 'None' },
			{ value: 0, text: 'Cleave' }
		]);
	});

	it('keeps the current value visible (marked retired) even when it is retired or not Item-flagged', () => {
		// A retired Item-flagged skill stays when it is the current grant…
		expect(reference.grantedSkillOptions(2)).toContainEqual({ value: 2, text: 'Smite · retired' });
		// …and a Player-only skill that was somehow already authored stays selectable rather than vanishing.
		expect(reference.grantedSkillOptions(1)).toContainEqual({ value: 1, text: 'Fireball' });
	});
});

describe('equipment slot / item category', () => {
	it('maps each equipment slot to the item category it accepts', () => {
		expect(reference.equipmentSlotCategory(EEquipmentSlot.HelmSlot)).toBe(EItemCategory.Helm);
		expect(reference.equipmentSlotCategory(EEquipmentSlot.ChestSlot)).toBe(EItemCategory.Chest);
		expect(reference.equipmentSlotCategory(EEquipmentSlot.LegSlot)).toBe(EItemCategory.Leg);
		expect(reference.equipmentSlotCategory(EEquipmentSlot.BootSlot)).toBe(EItemCategory.Boot);
		expect(reference.equipmentSlotCategory(EEquipmentSlot.WeaponSlot)).toBe(EItemCategory.Weapon);
		expect(reference.equipmentSlotCategory(EEquipmentSlot.AccessorySlot)).toBe(EItemCategory.Accessory);
	});

	it('narrows the item picker to only items matching the slot category', () => {
		// staticData.items: Iron Helm (Helm) and Dragon Blade (Weapon).
		expect(reference.itemOptionsForSlot(EEquipmentSlot.HelmSlot)).toEqual([{ value: 0, text: 'Iron Helm' }]);
		expect(reference.itemOptionsForSlot(EEquipmentSlot.WeaponSlot)).toEqual([{ value: 1, text: 'Dragon Blade' }]);
		expect(reference.itemOptionsForSlot(EEquipmentSlot.ChestSlot)).toEqual([]);
	});

	it('keeps a category-mismatched current value visible in the narrowed picker', () => {
		// Dragon Blade (Weapon) kept as the current value on a Helm-slot row, as a backstop for
		// already-authored bad data — the section warn is what actually flags it.
		expect(reference.itemOptionsForSlot(EEquipmentSlot.HelmSlot, 1)).toEqual([
			{ value: 0, text: 'Iron Helm' },
			{ value: 1, text: 'Dragon Blade' }
		]);
	});

	it('resolves an item id to its category', () => {
		expect(reference.itemCategoryOf(0)).toBe(EItemCategory.Helm);
		expect(reference.itemCategoryOf(1)).toBe(EItemCategory.Weapon);
		expect(reference.itemCategoryOf(99)).toBeUndefined();
	});
});

describe('challenge-type lookups', () => {
	it('lists challenge-type options and resolves one by id', () => {
		expect(reference.challengeTypeOptions()).toEqual([
			{ value: 1, text: 'Enemies Killed' },
			{ value: 2, text: 'Bosses Defeated' }
		]);
		expect(reference.challengeTypeById(2)?.name).toBe('Bosses Defeated');
		expect(reference.challengeTypeById(99)).toBeUndefined();
	});
});

describe('entityOptions target-entity picker', () => {
	it('lists every enemy when the statistic is not boss-only', () => {
		expect(reference.entityOptions(EEntityType.Enemy).map((o) => o.value)).toEqual([0, 1, 2, 3]);
	});

	it('restricts the enemy picker to bosses for a boss-only statistic', () => {
		const opts = reference.entityOptions(EEntityType.Enemy, true);
		expect(opts.map((o) => o.text)).toEqual(['Catacomb Lich', 'Ancient Wyrm']);
	});

	it('ignores the boss-only flag for non-enemy dimensions', () => {
		expect(reference.entityOptions(EEntityType.Zone, true).map((o) => o.value)).toEqual([0, 1]);
		expect(reference.entityOptions(EEntityType.Skill, true).map((o) => o.value)).toEqual([0, 1]);
	});

	it('keeps a boss-only target visible after it loses the isBoss flag (#1996)', () => {
		// Cave Bat (id 0) isn't a boss, but it's the challenge's currently-authored target.
		expect(reference.entityOptions(EEntityType.Enemy, true, 0)).toContainEqual({ value: 0, text: 'Cave Bat' });
		expect(reference.entityOptions(EEntityType.Enemy, true).map((o) => o.value)).not.toContain(0);
	});

	it('resolves a target name across all enemies regardless of the boss filter', () => {
		// entityName reads the unfiltered catalogue so an already-saved (or non-boss) target
		// still renders in the objective sentence.
		expect(reference.entityName(EEntityType.Enemy, 0)).toBe('Cave Bat');
		expect(reference.entityName(EEntityType.Enemy, 1)).toBe('Catacomb Lich');
		expect(reference.entityName(EEntityType.Zone, 1)).toBe('Frost Cavern');
		expect(reference.entityName(EEntityType.Skill, 0)).toBe('Cleave');
	});

	it('returns null when the target id or dimension does not resolve', () => {
		expect(reference.entityName(EEntityType.Enemy, 99)).toBeNull();
		expect(reference.entityName(EEntityType.None, 0)).toBeNull();
	});

	it('resolves the DamageType dimension from the fixed EDamageTypeKey enum, not a DB catalogue', () => {
		// Not a retireable reference table — every key is always offered, none ever "· retired".
		const opts = reference.entityOptions(EEntityType.DamageType);
		expect(opts).toHaveLength(Object.keys(EDamageTypeKey).length / 2);
		expect(opts).toContainEqual({ value: EDamageTypeKey.Fire, text: 'Fire' });
		expect(opts.every((o) => !o.text.includes('retired'))).toBe(true);
		expect(reference.entityName(EEntityType.DamageType, EDamageTypeKey.Elemental)).toBe('Elemental');
	});
});

describe('reward record lookups', () => {
	it('exposes the raw item / mod record lists', () => {
		expect(reference.itemRecords()).toHaveLength(2);
		expect(reference.itemModRecords()).toHaveLength(2);
	});

	it('resolves item name, rarity and skill name by id', () => {
		expect(reference.itemRecName(1)).toBe('Dragon Blade');
		expect(reference.itemRarityId(1)).toBe(ERarity.Legendary);
		expect(reference.skillName(1)).toBe('Fireball');
	});

	it('resolves item-mod name and its type name', () => {
		expect(reference.itemModName(0)).toBe('Sharp');
		expect(reference.itemModTypeName(0)).toBe('Prefix');
		expect(reference.itemModTypeName(1)).toBe('Suffix');
	});

	it('returns undefined for a mod-type lookup on a missing mod', () => {
		expect(reference.itemModTypeName(99)).toBeUndefined();
	});
});

describe('name and colour lookups', () => {
	it('names item categories, rarities and mod types', () => {
		expect(reference.itemCategoryName(EItemCategory.Helm)).toBe('Helm');
		expect(reference.rarityName(ERarity.Rare)).toBe('Rare');
		expect(reference.modTypeName(EItemModType.Suffix)).toBe('Suffix');
	});

	it('maps a rarity id to its themeable colour var', () => {
		expect(reference.rarityColor(ERarity.Legendary)).toBe('var(--rarity-legendary)');
	});

	it('returns an empty string for an unknown category or mod type', () => {
		expect(reference.itemCategoryName(99)).toBe('');
		expect(reference.modTypeName(99)).toBe('');
	});
});

describe('tag lookups and colour', () => {
	it('resolves a tag by id and lists tags within a category', () => {
		expect(reference.tagById(20)?.name).toBe('Metal');
		expect(reference.tagsByCategory(100).map((t) => t.name)).toEqual(['Fire', 'Metal']);
		expect(reference.tagsByCategory(200).map((t) => t.name)).toEqual(['Holy']);
	});

	it('builds the category option list from the loaded categories', () => {
		expect(reference.tagCategoryOptions()).toEqual([
			{ value: 100, text: 'Element' },
			{ value: 200, text: 'School' }
		]);
	});

	it('derives a deterministic themeable colour per category', () => {
		const a = reference.tagColor(100);
		const b = reference.tagColor(100);
		expect(a).toEqual(b); // deterministic
		expect(a.fg).toMatch(/^oklch\(var\(--tag-lightness\) var\(--tag-chroma\)/);
		expect(a.bd).toContain('/ var(--tag-border-alpha))');
		expect(a.bg).toContain('/ var(--tag-bg-alpha))');
		// Different categories yield different hues.
		expect(reference.tagColor(200).fg).not.toBe(a.fg);
	});
});

describe('tagUsage', () => {
	it('counts and lists the items and mods that reference a tag', () => {
		const fire = reference.tagUsage(10);
		expect(fire.items).toBe(2);
		expect(fire.mods).toBe(1);
		expect(fire.itemList.map((i) => i.name)).toEqual(['Iron Helm', 'Dragon Blade']);
		expect(fire.modList.map((m) => m.name)).toEqual(['Sharp']);
	});

	it('reports zero usage for an unreferenced tag', () => {
		const usage = reference.tagUsage(999);
		expect(usage).toEqual({ items: 0, mods: 0, itemList: [], modList: [] });
	});
});

describe('enemySpawnShareTotal', () => {
	it('sums this row plus every OTHER enemy competing in the same zone', () => {
		// Zone 0: Goblin's own weight (30) + Cave Bat (5) + Catacomb Lich (15) = 50.
		const total = reference.enemySpawnShareTotal({ zoneId: 0, weight: 30 }, [], { id: 2 });
		expect(total).toBe(50);
	});

	it('excludes the row owner so its own weight is not double-counted', () => {
		// Zone 1: only Goblin (id 2) spawns; competing against itself contributes nothing extra.
		const total = reference.enemySpawnShareTotal({ zoneId: 1, weight: 5 }, [], { id: 2 });
		expect(total).toBe(5);
	});

	it('never returns 0 so a share denominator stays safe', () => {
		const total = reference.enemySpawnShareTotal({ zoneId: 99, weight: 0 }, [], { id: 2 });
		expect(total).toBe(1);
	});

	it('excludes a retired competitor — it never rolls, so its weight is not real competition', () => {
		// Zone 0 without retirement: Goblin (30) + Cave Bat (5) + Catacomb Lich (15) = 50 (see above).
		staticData.enemies[0].retiredAt = '2026-01-01T00:00:00Z'; // Cave Bat
		const total = reference.enemySpawnShareTotal({ zoneId: 0, weight: 30 }, [], { id: 2 });
		expect(total).toBe(45);
	});
});
