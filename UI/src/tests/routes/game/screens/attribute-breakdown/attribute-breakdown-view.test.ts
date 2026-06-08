import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute, EItemModType, type IBattlerAttribute } from '$lib/api';
import { EAttributeModifierSource, EModifierType } from '$lib/battle';

// Stand-ins for the player + inventory managers and the static reference data.
// `buildPlayerModifiers` reads the player's allocations and equipped loadout
// from these; the view aggregates them through the real attribute pipeline.
const { mockPlayerManager, mockInventoryManager, staticData } = vi.hoisted(() => ({
	mockPlayerManager: { attributes: [] as IBattlerAttribute[], name: 'Aelara', level: 12 },
	mockInventoryManager: { equippedSlots: [] as unknown[] },
	// Absent reference data so attributeName falls back to the normalised enum.
	staticData: { attributes: undefined as unknown }
}));

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager, inventoryManager: mockInventoryManager }));
vi.mock('$stores', () => ({ staticData }));

import {
	AttributeBreakdownView,
	BREAKDOWN_ATTRS,
	attributeName,
	buildPlayerModifiers,
	fmtNum,
	fmtSigned,
	modifierLabel,
	slotLabel
} from '$routes/game/screens/attribute-breakdown/attribute-breakdown-view.svelte';

const mockItem = (
	name: string,
	attributes: IBattlerAttribute[],
	appliedMods: { name: string; itemModTypeId: EItemModType; attributes: IBattlerAttribute[] }[] = []
) => ({ name, attributes, appliedMods });

beforeEach(() => {
	mockPlayerManager.attributes = [];
	mockInventoryManager.equippedSlots = [];
	staticData.attributes = undefined;
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

	it('groups the displayed attributes into Core (6) and Derived (3)', () => {
		const view = new AttributeBreakdownView();
		const core = view.groups.find((g) => g.key === 'core')!;
		const derived = view.groups.find((g) => g.key === 'derived')!;
		expect(core.attrs.map((a) => a.meta.id)).toEqual([
			EAttribute.Strength,
			EAttribute.Endurance,
			EAttribute.Intellect,
			EAttribute.Agility,
			EAttribute.Dexterity,
			EAttribute.Luck
		]);
		expect(derived.attrs.map((a) => a.meta.id)).toEqual([
			EAttribute.MaxHealth,
			EAttribute.Defense,
			EAttribute.CooldownRecovery
		]);
	});

	it('defaults the selection to MaxHealth and can change it', () => {
		const view = new AttributeBreakdownView();
		expect(view.selected).toBe(EAttribute.MaxHealth);
		expect(view.selectedGrouped.groups.length).toBeGreaterThan(0);
		view.select(EAttribute.Strength);
		expect(view.selected).toBe(EAttribute.Strength);
	});

	it('only surfaces the implemented attributes (no obsolete / unused)', () => {
		const ids = BREAKDOWN_ATTRS.map((a) => a.id);
		expect(ids).not.toContain(EAttribute.DropBonus);
		expect(ids).not.toContain(EAttribute.CriticalChance);
		expect(ids).toHaveLength(9);
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
