import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute, EAttributeType, EItemModType, type IAttribute, type IBattlerAttribute } from '$lib/api';
import { EAttributeModifierSource, EModifierType, type AppliedModifier, type ComputedAttribute } from '$lib/battle';

// Stand-ins for the player + inventory managers and the static reference data.
// `buildPlayerModifiers` reads the player's allocations and equipped loadout
// from these; the view aggregates them through the real attribute pipeline and
// reads the display taxonomy / precision off `staticData.attributes`.
const { mockPlayerManager, mockInventoryManager, staticData } = vi.hoisted(() => ({
	mockPlayerManager: { attributes: [] as IBattlerAttribute[], name: 'Aelara', level: 12 },
	mockInventoryManager: { equippedSlots: [] as unknown[] },
	staticData: { attributes: undefined as IAttribute[] | undefined }
}));

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager, inventoryManager: mockInventoryManager }));
vi.mock('$stores', () => ({ staticData }));

import { attributeName } from '$lib/common';
import {
	AttributeBreakdownView,
	buildGroups,
	buildPlayerModifiers,
	fmtNum,
	fmtSigned,
	hasNonCombatModifier,
	modifierLabel,
	slotLabel,
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
	makeAttribute(EAttribute.Defense, 'Defense', { attributeType: EAttributeType.Secondary, displayOrder: 7 }),
	makeAttribute(EAttribute.CooldownRecovery, 'Cooldown Recovery', {
		attributeType: EAttributeType.Secondary,
		displayOrder: 8,
		decimals: 0,
		isPercentage: true
	}),
	makeAttribute(EAttribute.DamageTakenPerSecond, 'Damage Taken Per Second', {
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

beforeEach(() => {
	mockPlayerManager.attributes = [];
	mockInventoryManager.equippedSlots = [];
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
				[{ attributeId: EAttribute.Defense, amount: 14 }],
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
		expect(item).toMatchObject({ attribute: EAttribute.Defense, amount: 14, label: 'Aegis Greathelm', slot: 0 });
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
					{ attributeId: EAttribute.Defense, amount: 14 }
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
		// Defense: 2 base + 1×END(30) + 0.5×AGI(0) + 14 item = 46.
		expect(view.computedFor(EAttribute.Defense).total).toBe(46);
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
		// The engine base/derived aggregates always contribute, so they stay in Secondary.
		expect(secondary?.attrs.map((a) => a.meta.id)).toEqual([
			EAttribute.MaxHealth,
			EAttribute.Defense,
			EAttribute.CooldownRecovery
		]);
		// CooldownRecovery carries its reference-data precision (0 decimals) and percentage flag.
		const cdrMeta = secondary?.attrs.find((a) => a.meta.id === EAttribute.CooldownRecovery)?.meta;
		expect(cdrMeta?.dec).toBe(0);
		expect(cdrMeta?.pct).toBe(true);
	});

	it('self-selects only attributes with a real (non-combat) contributor', () => {
		// No allocations or gear: only the engine base/derived formulas contribute, so just the
		// Secondary aggregates surface — the obsolete/unimplemented attributes and the combat-only
		// Status channels have no contributors and drop out, and no Primary group is shown.
		const view = new AttributeBreakdownView();
		const ids = view.groups.flatMap((g) => g.attrs.map((a) => a.meta.id));
		expect(ids).toEqual([EAttribute.MaxHealth, EAttribute.Defense, EAttribute.CooldownRecovery]);
		expect(view.groups.some((g) => g.type === EAttributeType.Primary)).toBe(false);
		expect(ids).not.toContain(EAttribute.DropBonus);
		expect(ids).not.toContain(EAttribute.CriticalChance);
		expect(ids).not.toContain(EAttribute.DamageTakenPerSecond);
	});

	it('defaults the selection to MaxHealth and can change it', () => {
		const view = new AttributeBreakdownView();
		expect(view.selected).toBe(EAttribute.MaxHealth);
		expect(view.selectedGrouped.groups.length).toBeGreaterThan(0);
		view.select(EAttribute.Strength);
		expect(view.selected).toBe(EAttribute.Strength);
	});
});

describe('self-selecting membership', () => {
	it('treats a combat-only (SkillEffect) contributor as not a real, non-combat modifier', () => {
		const combatOnly = computedWithSources(EAttribute.DamageTakenPerSecond, [EAttributeModifierSource.SkillEffect]);
		expect(hasNonCombatModifier(combatOnly)).toBe(false);
	});

	it('treats any non-SkillEffect contributor (e.g. gear) as a real, non-combat modifier', () => {
		const fromGear = computedWithSources(EAttribute.Strength, [EAttributeModifierSource.Item]);
		expect(hasNonCombatModifier(fromGear)).toBe(true);
	});

	it('omits an attribute with only a combat modifier from the groups, but lists one with a non-combat modifier', () => {
		const computed = new Map<EAttribute, ComputedAttribute<LabeledModifier>>([
			[
				EAttribute.DamageTakenPerSecond,
				computedWithSources(EAttribute.DamageTakenPerSecond, [EAttributeModifierSource.SkillEffect])
			],
			[EAttribute.Strength, computedWithSources(EAttribute.Strength, [EAttributeModifierSource.PlayerStatPoints])]
		]);
		const ids = buildGroups(computed, refAttributes).flatMap((g) => g.attrs.map((a) => a.meta.id));
		expect(ids).toContain(EAttribute.Strength);
		expect(ids).not.toContain(EAttribute.DamageTakenPerSecond);
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
		expect(slotLabel(4)).toBe('Weapon');
	});
});
