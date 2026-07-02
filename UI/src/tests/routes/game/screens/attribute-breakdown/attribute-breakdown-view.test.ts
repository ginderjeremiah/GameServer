import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
	EAttribute,
	EAttributeType,
	EDamageTypeKey,
	EItemModType,
	type IAttribute,
	type IBattlerAttribute,
	type ISignaturePassive
} from '$lib/api';
import {
	BattleAttributes,
	EAttributeModifierSource,
	EModifierType,
	type AppliedModifier,
	type AttributeModifier,
	type ComputedAttribute
} from '$lib/battle';
import { classLockedBaseModifiers, classSignaturePassiveModifier } from '$lib/battle/class-modifiers';
import { proficiencyModifiers } from '$lib/battle/proficiency-modifiers';

// Stand-ins for the player + inventory managers, proficiency store, and static reference data.
// `buildPlayerModifiers` reads the player's allocations, equipped loadout, class locked base /
// signature passive, and proficiency bonuses from these; the view aggregates them through the real
// attribute pipeline and reads the display taxonomy / precision off `staticData.attributes`.
const { mockPlayerManager, mockInventoryManager, staticData, playerProficiencies } = vi.hoisted(() => ({
	mockPlayerManager: {
		attributes: [] as IBattlerAttribute[],
		name: 'Aelara',
		level: 12,
		battleLockedBaseModifiers: [] as AttributeModifier[],
		// Reassigned per-test (and to a flat no-op in beforeEach); the placeholder keeps the property
		// present so the hoisted factory needn't reference the not-yet-imported enums.
		battleSignaturePassiveModifier: undefined as unknown as (
			resolve: (attribute: EAttribute) => number
		) => AttributeModifier
	},
	mockInventoryManager: { equippedSlots: [] as unknown[] },
	staticData: { attributes: undefined as IAttribute[] | undefined },
	playerProficiencies: { battleModifiers: [] as AttributeModifier[] }
}));

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager, inventoryManager: mockInventoryManager }));
vi.mock('$stores', () => ({ staticData, playerProficiencies }));

import { attributeName } from '$lib/common';
import {
	ATTRIBUTE_TYPE_GROUPS,
	AttributeBreakdownView,
	buildGroups,
	buildPlayerModifiers,
	fmtNum,
	fmtSigned,
	hasNonCombatModifier,
	modifierLabel,
	type LabeledModifier
} from '$routes/game/screens/attribute-breakdown/attribute-breakdown-view.svelte';
import { makeAttribute } from '../../../../fixtures/attributes';

// Reference data mirroring the backend `Attribute` display metadata (taxonomy / order / decimals).
const refAttributes: IAttribute[] = [
	makeAttribute(EAttribute.Strength, 'Strength', {
		code: 'STR',
		attributeType: EAttributeType.Primary,
		displayOrder: 0
	}),
	makeAttribute(EAttribute.Endurance, 'Endurance', {
		code: 'END',
		attributeType: EAttributeType.Primary,
		displayOrder: 1
	}),
	makeAttribute(EAttribute.Intellect, 'Intellect', {
		code: 'INT',
		attributeType: EAttributeType.Primary,
		displayOrder: 2
	}),
	makeAttribute(EAttribute.Agility, 'Agility', { code: 'AGI', attributeType: EAttributeType.Primary, displayOrder: 3 }),
	makeAttribute(EAttribute.Dexterity, 'Dexterity', {
		code: 'DEX',
		attributeType: EAttributeType.Primary,
		displayOrder: 4
	}),
	makeAttribute(EAttribute.Luck, 'Luck', { code: 'LUK', attributeType: EAttributeType.Primary, displayOrder: 5 }),
	makeAttribute(EAttribute.MaxHealth, 'Max Health', { attributeType: EAttributeType.Secondary, displayOrder: 6 }),
	makeAttribute(EAttribute.Toughness, 'Toughness', { attributeType: EAttributeType.Secondary, displayOrder: 7 }),
	makeAttribute(EAttribute.CooldownRecovery, 'Cooldown Recovery', {
		attributeType: EAttributeType.Secondary,
		displayOrder: 8,
		decimals: 0,
		isPercentage: true
	}),
	makeAttribute(EAttribute.BleedDamagePerSecond, 'Bleed Damage Per Second', {
		attributeType: EAttributeType.Status,
		displayOrder: 14,
		isHarmful: true
	}),
	makeAttribute(EAttribute.HealthRegenPerSecond, 'Health Regen Per Second', {
		attributeType: EAttributeType.Status,
		displayOrder: 15
	})
];

const mockItem = (
	name: string,
	attributes: IBattlerAttribute[],
	appliedMods: { name: string; itemModTypeId: EItemModType; attributes: IBattlerAttribute[] }[] = []
) => ({ name, attributes, appliedMods });

/** A minimal computed attribute carrying one line per supplied source — enough to exercise the
 *  self-selecting membership rule, which only inspects each line's `source`. */
const computedWithSources = (
	attribute: EAttribute,
	sources: EAttributeModifierSource[]
): ComputedAttribute<LabeledModifier> => ({
	attribute,
	total: 0,
	additiveSubtotal: 0,
	multUplift: 0,
	lines: sources.map((source) => ({
		attribute,
		amount: 1,
		type: EModifierType.Additive,
		source,
		applied: 1,
		running: 1,
		multiplied: false
	})) as AppliedModifier<LabeledModifier>[]
});

// A flat no-op signature passive — the default a player whose class has no passive carries. It
// contributes nothing, so the breakdown must omit it (and not let it self-select Strength).
const noOpPassive: ISignaturePassive = {
	attributeId: EAttribute.Strength,
	amount: 0,
	scalingAmount: 0,
	modifierType: EModifierType.Additive
};

beforeEach(() => {
	mockPlayerManager.attributes = [];
	mockPlayerManager.level = 12;
	mockPlayerManager.battleLockedBaseModifiers = [];
	mockPlayerManager.battleSignaturePassiveModifier = (resolve) => classSignaturePassiveModifier(noOpPassive, resolve);
	mockInventoryManager.equippedSlots = [];
	playerProficiencies.battleModifiers = [];
	staticData.attributes = refAttributes;
});

describe('buildPlayerModifiers', () => {
	it('emits an additive PlayerStatPoints modifier per non-zero allocation', () => {
		mockPlayerManager.attributes = [
			{ attributeId: EAttribute.Strength, amount: 14 },
			{ attributeId: EAttribute.Endurance, amount: 0 } // skipped
		];
		const mods = buildPlayerModifiers();
		const points = mods.filter((m) => m.source === EAttributeModifierSource.PlayerStatPoints);
		expect(points).toHaveLength(1);
		expect(points[0]).toMatchObject({ attribute: EAttribute.Strength, amount: 14, type: EModifierType.Additive });
	});

	it('emits Item and ItemMod modifiers from equipped gear, carrying provenance', () => {
		mockInventoryManager.equippedSlots = [
			mockItem(
				'Aegis Greathelm',
				[{ attributeId: EAttribute.Toughness, amount: 14 }],
				[
					{
						name: 'Tempered',
						itemModTypeId: EItemModType.Prefix,
						attributes: [{ attributeId: EAttribute.Endurance, amount: 10 }]
					}
				]
			)
		];
		const mods = buildPlayerModifiers();
		const item = mods.find((m) => m.source === EAttributeModifierSource.Item);
		expect(item).toMatchObject({ attribute: EAttribute.Toughness, amount: 14, label: 'Aegis Greathelm' });
		const mod = mods.find((m) => m.source === EAttributeModifierSource.ItemMod);
		expect(mod).toMatchObject({
			attribute: EAttribute.Endurance,
			amount: 10,
			label: 'Tempered',
			modType: EItemModType.Prefix
		});
	});

	it('always appends the engine static base + derived modifiers', () => {
		const mods = buildPlayerModifiers();
		expect(
			mods.some((m) => m.source === EAttributeModifierSource.BaseValue && m.attribute === EAttribute.MaxHealth)
		).toBe(true);
		expect(
			mods.some((m) => m.source === EAttributeModifierSource.Derived && m.derivedSource === EAttribute.Endurance)
		).toBe(true);
	});

	it('composes the class locked base and proficiency bonuses after gear and before the static modifiers', () => {
		mockInventoryManager.equippedSlots = [mockItem('Aegis', [{ attributeId: EAttribute.Toughness, amount: 14 }])];
		mockPlayerManager.battleLockedBaseModifiers = classLockedBaseModifiers(
			[{ attributeId: EAttribute.Strength, baseAmount: 5, amountPerLevel: 1 }],
			mockPlayerManager.level // 5 + 1×12 = 17
		);
		playerProficiencies.battleModifiers = proficiencyModifiers(
			[{ level: 1, attributeId: EAttribute.Endurance, amount: 6, modifierTypeId: EModifierType.Additive }],
			3
		);
		const sources = buildPlayerModifiers().map((m) => m.source);
		const gear = sources.indexOf(EAttributeModifierSource.Item);
		const locked = sources.indexOf(EAttributeModifierSource.AttributeDistribution);
		const prof = sources.indexOf(EAttributeModifierSource.Proficiency);
		const firstStatic = sources.findIndex(
			(s) => s === EAttributeModifierSource.BaseValue || s === EAttributeModifierSource.Derived
		);
		// Apply order: gear → locked base → proficiency → statics.
		expect(gear).toBeLessThan(locked);
		expect(locked).toBeLessThan(prof);
		expect(prof).toBeLessThan(firstStatic);
		const mods = buildPlayerModifiers();
		expect(mods[locked]).toMatchObject({ attribute: EAttribute.Strength, amount: 17 });
		expect(mods[prof]).toMatchObject({ attribute: EAttribute.Endurance, amount: 6 });
	});

	it('composes the class signature passive last, resolved against the assembled scaling value', () => {
		mockPlayerManager.attributes = [{ attributeId: EAttribute.Strength, amount: 20 }];
		mockPlayerManager.battleSignaturePassiveModifier = (resolve) =>
			classSignaturePassiveModifier(
				{
					attributeId: EAttribute.Toughness,
					amount: 0,
					scalingAmount: 0.5,
					scalingAttributeId: EAttribute.Strength,
					modifierType: EModifierType.Additive
				},
				resolve
			);
		const mods = buildPlayerModifiers();
		const last = mods[mods.length - 1];
		// Strength resolves to 20 (allocation; no static base), so the passive adds 0.5 × 20 = 10 to Toughness.
		expect(last).toMatchObject({ source: EAttributeModifierSource.Class, attribute: EAttribute.Toughness });
		expect(last.amount).toBeCloseTo(10);
	});

	it('omits the class signature passive when it is a flat no-op (the default)', () => {
		// The beforeEach default passive contributes nothing, so no Class line is emitted — otherwise it
		// would spuriously self-select Strength into the inspector.
		const mods = buildPlayerModifiers();
		expect(mods.some((m) => m.source === EAttributeModifierSource.Class)).toBe(false);
	});
});

describe('AttributeBreakdownView', () => {
	it('aggregates allocations + gear + mods + derived into the right totals', () => {
		mockPlayerManager.attributes = [
			{ attributeId: EAttribute.Strength, amount: 14 },
			{ attributeId: EAttribute.Endurance, amount: 12 }
		];
		mockInventoryManager.equippedSlots = [
			mockItem(
				'Aegis Greathelm',
				[
					{ attributeId: EAttribute.Endurance, amount: 8 },
					{ attributeId: EAttribute.Toughness, amount: 14 }
				],
				[
					{
						name: 'Tempered',
						itemModTypeId: EItemModType.Prefix,
						attributes: [{ attributeId: EAttribute.Endurance, amount: 10 }]
					}
				]
			)
		];
		const view = new AttributeBreakdownView();

		expect(view.computedFor(EAttribute.Strength).total).toBe(14);
		// Endurance: 12 points + 8 item + 10 mod = 30.
		expect(view.computedFor(EAttribute.Endurance).total).toBe(30);
		// MaxHealth: 50 base + 20×END(30) + 5×STR(14) = 720.
		expect(view.computedFor(EAttribute.MaxHealth).total).toBe(720);
		// Toughness: 2×END(30) + 14 item = 74.
		expect(view.computedFor(EAttribute.Toughness).total).toBe(74);
	});

	it('groups the displayed attributes by display taxonomy (Primary / Secondary), sorted by display order', () => {
		mockPlayerManager.attributes = [
			{ attributeId: EAttribute.Strength, amount: 14 },
			{ attributeId: EAttribute.Endurance, amount: 12 }
		];
		const view = new AttributeBreakdownView();
		const primary = view.groups.find((g) => g.type === EAttributeType.Primary);
		const secondary = view.groups.find((g) => g.type === EAttributeType.Secondary);
		// Only the allocated core attributes self-select into Primary.
		expect(primary?.attrs.map((a) => a.meta.id)).toEqual([EAttribute.Strength, EAttribute.Endurance]);
		// The engine base/derived aggregates always contribute, so they stay in Secondary. Unlike the other
		// chance attributes, CriticalChanceMultiplier carries a base of 1 (crit rework #1425, per-skill base
		// #1453 — the enabler moved to the skill's own CriticalChance, so this attribute is always present).
		// CriticalDamage (base 1.5) and DodgeChance (Agility-derived) remain too.
		expect(secondary?.attrs.map((a) => a.meta.id)).toEqual([
			EAttribute.MaxHealth,
			EAttribute.Toughness,
			EAttribute.CooldownRecovery,
			EAttribute.CriticalChanceMultiplier,
			EAttribute.CriticalDamage,
			EAttribute.DodgeChance
		]);
		// CooldownRecovery carries its reference-data precision (0 decimals) and percentage flag.
		const cdrMeta = secondary?.attrs.find((a) => a.meta.id === EAttribute.CooldownRecovery)?.meta;
		expect(cdrMeta?.dec).toBe(0);
		expect(cdrMeta?.pct).toBe(true);
	});

	it('self-selects only attributes with a real (non-combat) contributor', () => {
		// No allocations or gear: only the engine base/derived formulas contribute. The Secondary aggregates
		// surface — MaxHealth/Toughness/CooldownRecovery, plus CriticalChanceMultiplier (base 1, #1453),
		// CriticalDamage (base 1.5) and DodgeChance (Agility-derived) — alongside the obsolete attribute and
		// the combat-only Status channels staying excluded. No Primary group is shown.
		const view = new AttributeBreakdownView();
		const ids = view.groups.flatMap((g) => g.attrs.map((a) => a.meta.id));
		expect(ids).toEqual([
			EAttribute.MaxHealth,
			EAttribute.Toughness,
			EAttribute.CooldownRecovery,
			EAttribute.CriticalChanceMultiplier,
			EAttribute.CriticalDamage,
			EAttribute.DodgeChance
		]);
		expect(view.groups.some((g) => g.type === EAttributeType.Primary)).toBe(false);
		expect(ids).not.toContain(EAttribute.DropBonus);
		expect(ids).not.toContain(EAttribute.BleedDamagePerSecond);
	});

	it('defaults the selection to MaxHealth and can change it', () => {
		const view = new AttributeBreakdownView();
		expect(view.selected).toBe(EAttribute.MaxHealth);
		expect(view.selectedGrouped.groups.length).toBeGreaterThan(0);
		view.select(EAttribute.Strength);
		expect(view.selected).toBe(EAttribute.Strength);
	});
});

describe('battle-assembly parity', () => {
	// A scenario exercising all three battle-assembly sources at once.
	const distributions = [{ attributeId: EAttribute.Strength, baseAmount: 5, amountPerLevel: 1 }]; // STR +17 at lvl 12
	const proficiency = [
		{ level: 1, attributeId: EAttribute.Endurance, amount: 6, modifierTypeId: EModifierType.Additive }
	]; // END +6
	const scaledPassive: ISignaturePassive = {
		attributeId: EAttribute.Toughness,
		amount: 0,
		scalingAmount: 0.5,
		scalingAttributeId: EAttribute.Strength,
		modifierType: EModifierType.Additive
	};

	function arrangeScenario() {
		mockPlayerManager.level = 12;
		mockPlayerManager.attributes = [
			{ attributeId: EAttribute.Strength, amount: 14 },
			{ attributeId: EAttribute.Endurance, amount: 12 }
		];
		mockInventoryManager.equippedSlots = [
			mockItem(
				'Aegis Greathelm',
				[{ attributeId: EAttribute.Toughness, amount: 14 }],
				[
					{
						name: 'Tempered',
						itemModTypeId: EItemModType.Prefix,
						attributes: [{ attributeId: EAttribute.Endurance, amount: 10 }]
					}
				]
			)
		];
		mockPlayerManager.battleLockedBaseModifiers = classLockedBaseModifiers(distributions, mockPlayerManager.level);
		playerProficiencies.battleModifiers = proficiencyModifiers(proficiency, 3);
		mockPlayerManager.battleSignaturePassiveModifier = (resolve) =>
			classSignaturePassiveModifier(scaledPassive, resolve);
	}

	it('aggregates to the same totals the live battler (BattleAttributes) resolves', () => {
		arrangeScenario();
		const view = new AttributeBreakdownView();

		// Oracle: assemble the identical inputs the way BattleEngine.resetPlayer builds the player battler —
		// free pool + gear as the base data, locked base + proficiency as additional modifiers (before the
		// statics setData appends), then the signature passive added last, resolved against the assembled set.
		const atts: IBattlerAttribute[] = [
			...mockPlayerManager.attributes,
			{ attributeId: EAttribute.Toughness, amount: 14 },
			{ attributeId: EAttribute.Endurance, amount: 10 }
		];
		const battler = new BattleAttributes();
		battler.setData(atts, true, [
			...classLockedBaseModifiers(distributions, 12),
			...proficiencyModifiers(proficiency, 3)
		]);
		battler.addModifier(classSignaturePassiveModifier(scaledPassive, (a) => battler.getValue(a)));

		for (const attr of [EAttribute.Strength, EAttribute.Endurance, EAttribute.Toughness, EAttribute.MaxHealth]) {
			expect(view.computedFor(attr).total).toBeCloseTo(battler.getValue(attr), 10);
		}
		// Sanity-check the load-bearing numbers: STR = 14 + 17 locked base = 31; the passive then adds
		// 0.5 × 31 = 15.5 to Toughness (2×END + 14 gear + 15.5 = 85.5, END = 12 + 10 + 6 = 28).
		expect(view.computedFor(EAttribute.Strength).total).toBe(31);
		expect(view.computedFor(EAttribute.Toughness).total).toBeCloseTo(85.5);
	});

	it('surfaces the locked base, proficiency, and signature passive in the by-source grouping', () => {
		arrangeScenario();
		const view = new AttributeBreakdownView();
		const sourcesFor = (attr: EAttribute) => {
			view.select(attr);
			return view.selectedGrouped.groups.map((g) => g.source);
		};
		expect(sourcesFor(EAttribute.Strength)).toContain(EAttributeModifierSource.AttributeDistribution);
		expect(sourcesFor(EAttribute.Endurance)).toContain(EAttributeModifierSource.Proficiency);
		expect(sourcesFor(EAttribute.Toughness)).toContain(EAttributeModifierSource.Class);
	});
});

describe('self-selecting membership', () => {
	it('treats a combat-only (SkillEffect) contributor as not a real, non-combat modifier', () => {
		const combatOnly = computedWithSources(EAttribute.BleedDamagePerSecond, [EAttributeModifierSource.SkillEffect]);
		expect(hasNonCombatModifier(combatOnly)).toBe(false);
	});

	it('treats any non-SkillEffect contributor (e.g. gear) as a real, non-combat modifier', () => {
		const fromGear = computedWithSources(EAttribute.Strength, [EAttributeModifierSource.Item]);
		expect(hasNonCombatModifier(fromGear)).toBe(true);
	});

	it('omits an attribute with only a combat modifier from the groups, but lists one with a non-combat modifier', () => {
		const computed = new Map<EAttribute, ComputedAttribute<LabeledModifier>>([
			[
				EAttribute.BleedDamagePerSecond,
				computedWithSources(EAttribute.BleedDamagePerSecond, [EAttributeModifierSource.SkillEffect])
			],
			[EAttribute.Strength, computedWithSources(EAttribute.Strength, [EAttributeModifierSource.PlayerStatPoints])]
		]);
		const ids = buildGroups(computed, refAttributes).flatMap((g) => g.attrs.map((a) => a.meta.id));
		expect(ids).toContain(EAttribute.Strength);
		expect(ids).not.toContain(EAttribute.BleedDamagePerSecond);
	});
});

describe('formatting + labels', () => {
	it('formats numbers and signed contributions', () => {
		expect(fmtNum(1234.5, 0)).toBe('1,235');
		expect(fmtNum(4.16, 2)).toBe('4.16');
		expect(fmtSigned(12)).toBe('+12');
		expect(fmtSigned(-3)).toBe('−3');
		expect(fmtSigned(0.5)).toBe('+0.5'); // small fractional shown to 1 dp
	});

	it('renders a percentage attribute scaled ×100 with a % suffix', () => {
		// CooldownRecovery stores a decimal fraction (1.09) and renders as a percentage (109%).
		expect(fmtNum(1.09, 0, true)).toBe('109%');
		expect(fmtSigned(1.0, undefined, true)).toBe('+100%'); // the base contribution
		expect(fmtSigned(0.08, undefined, true)).toBe('+8%'); // Agility's +0.004·20 share
	});

	it('falls back to a normalised enum name when reference data is absent', () => {
		expect(attributeName(EAttribute.MaxHealth)).toBe('Max Health');
		expect(attributeName(EAttribute.CooldownRecovery)).toBe('Cooldown Recovery');
	});

	it('labels contributions by their source', () => {
		expect(modifierLabel({ source: EAttributeModifierSource.PlayerStatPoints } as never)).toBe('Allocated stat points');
		expect(modifierLabel({ source: EAttributeModifierSource.BaseValue } as never)).toBe('Engine base value');
		expect(
			modifierLabel({ source: EAttributeModifierSource.Derived, derivedSource: EAttribute.Endurance } as never)
		).toBe('Endurance');
		expect(modifierLabel({ source: EAttributeModifierSource.AttributeDistribution } as never)).toBe('Class base');
		expect(modifierLabel({ source: EAttributeModifierSource.Proficiency } as never)).toBe('Proficiency');
		expect(modifierLabel({ source: EAttributeModifierSource.Class } as never)).toBe('Signature passive');
	});
});

describe('ATTRIBUTE_TYPE_GROUPS taxonomy coverage', () => {
	// KindTag resolves its label from these groups; an unmapped EAttributeType would render a blank
	// pill, so every enum value must have a group. This fails loud if a future type is added without one.
	it('maps every EAttributeType value to a display group', () => {
		const mapped = new Set(ATTRIBUTE_TYPE_GROUPS.map((g) => g.type));
		const values = Object.values(EAttributeType).filter((v): v is EAttributeType => typeof v === 'number');
		for (const value of values) {
			expect(mapped.has(value)).toBe(true);
		}
	});
});

describe('buildGroups — damage-type affinity sub-groups (#1320, Area F)', () => {
	// The amplification/resistance family is typed `Affinity`; the breakdown splits it into one
	// sub-group per damage-type key so the large set stays readable grouped under its type.
	const affinityRefs: IAttribute[] = [
		makeAttribute(EAttribute.FireAmplification, 'Fire Amplification', {
			attributeType: EAttributeType.Affinity,
			displayOrder: 20,
			isPercentage: true
		}),
		makeAttribute(EAttribute.FireResistance, 'Fire Resistance', {
			attributeType: EAttributeType.Affinity,
			displayOrder: 21,
			isPercentage: true
		}),
		makeAttribute(EAttribute.BleedResistance, 'Bleed Resistance', {
			attributeType: EAttributeType.Affinity,
			displayOrder: 30,
			isPercentage: true
		}),
		makeAttribute(EAttribute.ElementalResistance, 'Elemental Resistance', {
			attributeType: EAttributeType.Affinity,
			displayOrder: 40,
			isPercentage: true
		})
	];

	const computedAffinity = (ids: EAttribute[]) =>
		new Map(ids.map((id) => [id, computedWithSources(id, [EAttributeModifierSource.Item])] as const));

	it('splits the affinity family into one sub-group per damage type, leaf types before categories', () => {
		const groups = buildGroups(
			computedAffinity([
				EAttribute.FireAmplification,
				EAttribute.FireResistance,
				EAttribute.BleedResistance,
				EAttribute.ElementalResistance
			]),
			affinityRefs
		);

		// Ordered by damage-type key: Fire (1), Bleed (5), Elemental (8) — leaf types ahead of the category.
		expect(groups.map((g) => g.label)).toEqual(['Fire', 'Bleed', 'Elemental']);
		expect(groups.map((g) => g.damageTypeKey)).toEqual([
			EDamageTypeKey.Fire,
			EDamageTypeKey.Bleed,
			EDamageTypeKey.Elemental
		]);
		// Each sub-group keeps the Affinity taxonomy and a stable unique render key.
		expect(groups.every((g) => g.type === EAttributeType.Affinity)).toBe(true);
		expect(groups.map((g) => g.key)).toEqual(['affinity-1', 'affinity-5', 'affinity-8']);
		// Fire groups both its amplification and resistance, sorted by display order (amp before resist).
		expect(groups[0].attrs.map((a) => a.meta.id)).toEqual([EAttribute.FireAmplification, EAttribute.FireResistance]);
		expect(groups[1].attrs.map((a) => a.meta.id)).toEqual([EAttribute.BleedResistance]);
		expect(groups[2].attrs.map((a) => a.meta.id)).toEqual([EAttribute.ElementalResistance]);
	});

	it('leaves the non-affinity taxonomies as single groups alongside the damage-type sub-groups', () => {
		const groups = buildGroups(
			computedAffinity([EAttribute.FireResistance]).set(
				EAttribute.Strength,
				computedWithSources(EAttribute.Strength, [EAttributeModifierSource.PlayerStatPoints])
			),
			[
				makeAttribute(EAttribute.Strength, 'Strength', { attributeType: EAttributeType.Primary, displayOrder: 0 }),
				...affinityRefs
			]
		);

		const primary = groups.find((g) => g.type === EAttributeType.Primary);
		expect(primary?.key).toBe(String(EAttributeType.Primary));
		expect(primary?.damageTypeKey).toBeUndefined();
		expect(groups.find((g) => g.damageTypeKey === EDamageTypeKey.Fire)?.label).toBe('Fire');
	});
});
