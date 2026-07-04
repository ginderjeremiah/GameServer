import { describe, it, expect } from 'vitest';
import { EAttribute, EAttributeType, type IAttribute } from '$lib/api';
import {
	attributeCode,
	attributeEnumName,
	attributeIcon,
	attributeIsHarmful,
	attributeName,
	attributeTypeName
} from '../../lib/common/attribute-display';
import { makeAttribute } from '../fixtures/attributes';

describe('attributeEnumName', () => {
	it('returns the normalized enum key for a known attribute id', () => {
		expect(attributeEnumName(EAttribute.MaxHealth)).toBe('Max Health');
	});

	it('returns the single-word name for a single-word attribute', () => {
		expect(attributeEnumName(EAttribute.Strength)).toBe('Strength');
	});

	it('degrades to "Unknown" for an out-of-range id rather than throwing', () => {
		// `EAttribute[id]` is `undefined` for an unknown id; the fallback must not crash a
		// hot display path (#493) and should render a readable label, not a blank string.
		const unknownId = 999 as EAttribute;
		expect(() => attributeEnumName(unknownId)).not.toThrow();
		expect(attributeEnumName(unknownId)).toBe('Unknown');
	});
});

describe('attributeName', () => {
	const mockAttributes: IAttribute[] = [
		makeAttribute(EAttribute.Strength, 'Physical Power'),
		makeAttribute(EAttribute.MaxHealth, 'Maximum Life')
	];

	it('returns the reference-data name when attributes are provided', () => {
		expect(attributeName(EAttribute.Strength, mockAttributes)).toBe('Physical Power');
		expect(attributeName(EAttribute.MaxHealth, mockAttributes)).toBe('Maximum Life');
	});

	it('falls back to the normalised enum name when attributes are absent', () => {
		expect(attributeName(EAttribute.Strength)).toBe('Strength');
		expect(attributeName(EAttribute.MaxHealth)).toBe('Max Health');
		expect(attributeName(EAttribute.CooldownRecovery)).toBe('Cooldown Recovery');
	});

	it('falls back to the normalised enum name when the id is absent from the provided list', () => {
		expect(attributeName(EAttribute.Luck, mockAttributes)).toBe('Luck');
	});

	it('degrades to "Unknown" for an out-of-range id rather than throwing', () => {
		const unknownId = 999 as EAttribute;
		expect(() => attributeName(unknownId)).not.toThrow();
		expect(attributeName(unknownId)).toBe('Unknown');
	});
});

describe('attributeCode', () => {
	const mockAttributes: IAttribute[] = [
		makeAttribute(EAttribute.Strength, 'Strength', { code: 'STR' }),
		makeAttribute(EAttribute.MaxHealth, 'Max Health', { code: '' })
	];

	it('returns the reference-data code when attributes are provided', () => {
		expect(attributeCode(EAttribute.Strength, mockAttributes)).toBe('STR');
	});

	it('returns the empty code verbatim for an attribute the backend assigns none', () => {
		expect(attributeCode(EAttribute.MaxHealth, mockAttributes)).toBe('');
	});

	it('falls back to the normalised enum name when attributes are absent', () => {
		expect(attributeCode(EAttribute.Strength)).toBe('Strength');
		expect(attributeCode(EAttribute.MaxHealth)).toBe('Max Health');
	});

	it('falls back to the normalised enum name when the id is absent from the provided list', () => {
		expect(attributeCode(EAttribute.Luck, mockAttributes)).toBe('Luck');
	});
});

describe('attributeIsHarmful', () => {
	const mockAttributes: IAttribute[] = [
		makeAttribute(EAttribute.BleedDamagePerSecond, 'Bleed Damage Per Second', { isHarmful: true }),
		makeAttribute(EAttribute.Strength, 'Strength', { isHarmful: false })
	];

	it('reads the harmful flag from the reference set', () => {
		expect(attributeIsHarmful(EAttribute.BleedDamagePerSecond, mockAttributes)).toBe(true);
		expect(attributeIsHarmful(EAttribute.Strength, mockAttributes)).toBe(false);
	});

	it('defaults to false when attributes are absent or the id is unknown', () => {
		expect(attributeIsHarmful(EAttribute.BleedDamagePerSecond)).toBe(false);
		expect(attributeIsHarmful(EAttribute.Luck, mockAttributes)).toBe(false);
	});
});

describe('attributeTypeName', () => {
	it('maps each EAttributeType to its display label', () => {
		expect(attributeTypeName(EAttributeType.Primary)).toBe('Primary');
		expect(attributeTypeName(EAttributeType.Secondary)).toBe('Secondary');
		expect(attributeTypeName(EAttributeType.Status)).toBe('Status');
	});

	it('degrades to an empty label for an undefined or out-of-range type', () => {
		// Reference data not yet loaded (undefined) or a value with no enum key must not throw.
		expect(attributeTypeName(undefined)).toBe('');
		expect(attributeTypeName(99 as EAttributeType)).toBe('');
	});
});

describe('attributeIcon', () => {
	it('resolves to the static-img path under /img, named after the display name', () => {
		expect(attributeIcon(EAttribute.Strength)).toBe('/img/Strength.png');
		expect(attributeIcon(EAttribute.MaxHealth)).toBe('/img/Max Health.png');
		expect(attributeIcon(EAttribute.CooldownRecovery)).toBe('/img/Cooldown Recovery.png');
		expect(attributeIcon(EAttribute.HealthRegenPerSecond)).toBe('/img/Health Regen Per Second.png');
		expect(attributeIcon(EAttribute.CriticalChanceMultiplier)).toBe('/img/Critical Chance Multiplier.png');
	});

	it('maps every attribute with art to a non-empty path', () => {
		const withArt = [
			EAttribute.Strength,
			EAttribute.Endurance,
			EAttribute.Intellect,
			EAttribute.Agility,
			EAttribute.Dexterity,
			EAttribute.Luck,
			EAttribute.MaxHealth,
			EAttribute.Toughness,
			EAttribute.CooldownRecovery,
			EAttribute.CriticalChanceMultiplier,
			EAttribute.CriticalDamage,
			EAttribute.DodgeChance,
			EAttribute.HealthRegenPerSecond,
			EAttribute.DamageReflection
		];
		for (const id of withArt) {
			expect(attributeIcon(id)).toMatch(/^\/img\/.+\.png$/);
		}
	});

	it('maps the damage-type amp/resist + weapon-amp family to its badge-composited art (#1320/#1340)', () => {
		// Amplification = the type's base icon + amp badge; Resistance = base + resist badge (composited).
		expect(attributeIcon(EAttribute.FireAmplification)).toBe('/img/Fire Amplification.png');
		expect(attributeIcon(EAttribute.PhysicalResistance)).toBe('/img/Physical Resistance.png');
		expect(attributeIcon(EAttribute.ElementalResistance)).toBe('/img/Elemental Resistance.png');
		// The DoT category's files use its full display name; weapon types are amp-only.
		expect(attributeIcon(EAttribute.DotAmplification)).toBe('/img/Damage Over Time Amplification.png');
		expect(attributeIcon(EAttribute.SwordAmplification)).toBe('/img/Sword Amplification.png');
		// Every amp/resist/weapon-amp attribute resolves to a path (none left blank).
		const family = [
			EAttribute.PhysicalAmplification,
			EAttribute.PhysicalResistance,
			EAttribute.FireAmplification,
			EAttribute.FireResistance,
			EAttribute.WaterAmplification,
			EAttribute.WaterResistance,
			EAttribute.EarthAmplification,
			EAttribute.EarthResistance,
			EAttribute.WindAmplification,
			EAttribute.WindResistance,
			EAttribute.BleedAmplification,
			EAttribute.BleedResistance,
			EAttribute.PoisonAmplification,
			EAttribute.PoisonResistance,
			EAttribute.BurnAmplification,
			EAttribute.BurnResistance,
			EAttribute.ElementalAmplification,
			EAttribute.ElementalResistance,
			EAttribute.DotAmplification,
			EAttribute.DotResistance,
			EAttribute.SwordAmplification,
			EAttribute.AxeAmplification,
			EAttribute.BowAmplification,
			EAttribute.ClubAmplification,
			EAttribute.DaggerAmplification,
			EAttribute.UnarmedAmplification
		];
		for (const id of family) {
			expect(attributeIcon(id)).toMatch(/^\/img\/.+\.png$/);
		}
	});

	it('returns "" for attributes with no art and for unknown ids', () => {
		// DropBonus is obsolete, the typed DoT accumulators have no art yet (#1320 Area F), and the parry/dodge
		// (#1457/#1523) and cadence (#1426) multiplier pairs are art-pending; an out-of-range id resolves the
		// same way so the AttributeIcon component renders nothing.
		expect(attributeIcon(EAttribute.DropBonus)).toBe('');
		expect(attributeIcon(EAttribute.BleedDamagePerSecond)).toBe('');
		expect(attributeIcon(EAttribute.PoisonDamagePerSecond)).toBe('');
		expect(attributeIcon(EAttribute.BurnDamagePerSecond)).toBe('');
		expect(attributeIcon(EAttribute.CooldownBonus)).toBe('');
		expect(attributeIcon(EAttribute.CooldownBonusMultiplier)).toBe('');
		expect(attributeIcon(999 as EAttribute)).toBe('');
	});
});
