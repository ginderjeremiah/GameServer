import { describe, it, expect } from 'vitest';
import {
	EAttribute,
	EAttributeType,
	EModifierType,
	type IAttribute,
	type IProficiencyLevelModifier,
	type IProficiencyLevelReward,
	type ISkillPathContribution
} from '$lib/api';
import {
	buildLadder,
	decipherReveal,
	formatModifier,
	statePill,
	trainedBy,
	xpProgressText,
	type SkillNameResolver
} from '$routes/game/screens/proficiencies/word-detail';
import type { TierView } from '$routes/game/screens/proficiencies/proficiencies-lexicon';

/* ── fixtures ──────────────────────────────────────────────────────────────── */

const attr = (id: EAttribute, name: string, isPercentage = false): IAttribute => ({
	id,
	name,
	description: '',
	attributeType: EAttributeType.Secondary,
	isPercentage,
	isHarmful: false,
	code: '',
	displayOrder: 0,
	decimals: 1
});

// A percentage attribute (renders a `+2%`-style delta) and a flat one (renders a plain `+5`).
const ATTRIBUTES: IAttribute[] = [
	attr(EAttribute.CriticalChance, 'Fire Damage', true),
	attr(EAttribute.Toughness, 'Toughness', false)
];

const modifier = (o: Partial<IProficiencyLevelModifier> & { level: number }): IProficiencyLevelModifier => ({
	attributeId: EAttribute.CriticalChance,
	modifierTypeId: EModifierType.Additive,
	amount: 0.02,
	...o
});

const reward = (level: number, rewardSkillId: number): IProficiencyLevelReward => ({ level, rewardSkillId });
const contribution = (skillId: number, homeTier = 0, weight = 1): ISkillPathContribution => ({
	skillId,
	homeTier,
	weight
});

const tierView = (o: Partial<TierView> & { id: number }): TierView => ({
	name: `Tier ${o.id}`,
	pathOrdinal: 0,
	level: 0,
	maxLevel: 10,
	xp: 0,
	xpForNext: 100,
	state: 'unlocked',
	frontier: false,
	milestoneLevels: [],
	levelModifiers: [],
	levelRewards: [],
	decipher: 'undeciphered',
	word: `word${o.id}`,
	pronunciation: `pron${o.id}`,
	translation: `means${o.id}`,
	iconPath: '',
	...o
});

// Resolves a handful of skill ids; everything else is unknown (undefined), exercising the drop path.
const SKILL_NAMES: Record<number, string> = { 100: 'Fireball', 200: 'Ember Strike', 300: 'Flame Burst' };
const resolveSkill: SkillNameResolver = (id) => SKILL_NAMES[id];

/* ── statePill ─────────────────────────────────────────────────────────────── */

describe('statePill', () => {
	it('maps each tier state to its label and theme key', () => {
		expect(statePill('maxed')).toEqual({ label: 'MASTERED', key: 'mastered' });
		expect(statePill('training')).toEqual({ label: 'TRAINING', key: 'training' });
		expect(statePill('unlocked')).toEqual({ label: 'LEARNING', key: 'learning' });
	});
});

/* ── xpProgressText ────────────────────────────────────────────────────────── */

describe('xpProgressText', () => {
	it('summarises residual XP toward the next level', () => {
		const tier = tierView({ id: 1, level: 6, maxLevel: 10, xp: 40, xpForNext: 150, state: 'training' });
		expect(xpProgressText(tier)).toBe('40 / 150 XP → level 7');
	});

	it('reports a maxed tier as fully translated', () => {
		const tier = tierView({ id: 2, level: 10, maxLevel: 10, xp: 0, xpForNext: 0, state: 'maxed' });
		expect(xpProgressText(tier)).toBe('maxed out — fully translated');
	});

	it('treats a tier at its (lower) cap as maxed regardless of state', () => {
		const tier = tierView({ id: 3, level: 5, maxLevel: 5 });
		expect(xpProgressText(tier)).toBe('maxed out — fully translated');
	});
});

/* ── decipherReveal ────────────────────────────────────────────────────────── */

describe('decipherReveal', () => {
	it('shows the undeciphered placeholder before the pronunciation is learned', () => {
		expect(decipherReveal(tierView({ id: 1, decipher: 'undeciphered' }))).toBe('⟨ undeciphered ⟩');
	});

	it('quotes the pronunciation at the pronunciation stage', () => {
		expect(decipherReveal(tierView({ id: 1, decipher: 'pronunciation', pronunciation: 'AYN-kor' }))).toBe('“AYN-kor”');
	});

	it('shows the translation once translated', () => {
		expect(decipherReveal(tierView({ id: 1, decipher: 'translated', translation: 'The First Flame' }))).toBe(
			'The First Flame'
		);
	});
});

/* ── formatModifier ────────────────────────────────────────────────────────── */

describe('formatModifier', () => {
	it('formats an additive percentage modifier as a signed delta plus the attribute name', () => {
		const mod = modifier({ level: 1, attributeId: EAttribute.CriticalChance, amount: 0.02 });
		expect(formatModifier(mod, ATTRIBUTES)).toBe('+2% Fire Damage');
	});

	it('formats an additive flat modifier without a percent sign', () => {
		const mod = modifier({
			level: 1,
			attributeId: EAttribute.Toughness,
			modifierTypeId: EModifierType.Additive,
			amount: 5
		});
		expect(formatModifier(mod, ATTRIBUTES)).toBe('+5 Toughness');
	});

	it('formats a multiplicative modifier as a ×factor', () => {
		const mod = modifier({
			level: 1,
			attributeId: EAttribute.Toughness,
			modifierTypeId: EModifierType.Multiplicative,
			amount: 1.5
		});
		expect(formatModifier(mod, ATTRIBUTES)).toBe('×1.5 Toughness');
	});

	it('falls back to the humanised enum name when the attribute is not in the reference set', () => {
		const mod = modifier({ level: 1, attributeId: EAttribute.Toughness, amount: 3 });
		// No attributes passed → name degrades to the enum key, magnitude still renders.
		expect(formatModifier(mod, undefined)).toBe('+3 Toughness');
	});
});

/* ── buildLadder ───────────────────────────────────────────────────────────── */

describe('buildLadder', () => {
	it('produces one row per level from 1 to maxLevel', () => {
		const tier = tierView({ id: 1, maxLevel: 10 });
		const rows = buildLadder(tier, ATTRIBUTES, resolveSkill);
		expect(rows.map((r) => r.level)).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
	});

	it('marks rows at or below the current level as reached', () => {
		const tier = tierView({ id: 1, level: 3, maxLevel: 5 });
		const rows = buildLadder(tier, ATTRIBUTES, resolveSkill);
		expect(rows.map((r) => r.reached)).toEqual([true, true, true, false, false]);
	});

	it('formats the per-level bonus from the modifiers authored at that exact level', () => {
		const tier = tierView({
			id: 1,
			maxLevel: 3,
			levelModifiers: [
				modifier({ level: 1, attributeId: EAttribute.CriticalChance, amount: 0.02 }),
				modifier({ level: 3, attributeId: EAttribute.Toughness, modifierTypeId: EModifierType.Additive, amount: 4 })
			]
		});
		const rows = buildLadder(tier, ATTRIBUTES, resolveSkill);
		expect(rows[0].bonus).toBe('+2% Fire Damage');
		expect(rows[1].bonus).toBe(''); // level 2 has no authored payout
		expect(rows[2].bonus).toBe('+4 Toughness');
	});

	it('joins multiple modifiers authored at the same level', () => {
		const tier = tierView({
			id: 1,
			maxLevel: 2,
			levelModifiers: [
				modifier({ level: 2, attributeId: EAttribute.CriticalChance, amount: 0.03 }),
				modifier({ level: 2, attributeId: EAttribute.Toughness, modifierTypeId: EModifierType.Additive, amount: 2 })
			]
		});
		const rows = buildLadder(tier, ATTRIBUTES, resolveSkill);
		expect(rows[1].bonus).toBe('+3% Fire Damage · +2 Toughness');
	});

	it('flags reward levels as milestones and resolves the granted skill name', () => {
		const tier = tierView({
			id: 1,
			maxLevel: 10,
			levelRewards: [reward(5, 300), reward(10, 100)]
		});
		const rows = buildLadder(tier, ATTRIBUTES, resolveSkill);
		expect(rows.filter((r) => r.isMilestone).map((r) => r.level)).toEqual([5, 10]);
		expect(rows[4].skillName).toBe('Flame Burst');
		expect(rows[9].skillName).toBe('Fireball');
		expect(rows[0].isMilestone).toBe(false);
		expect(rows[0].skillName).toBeUndefined();
	});

	it('carries a milestone whose reward skill cannot be resolved as a milestone with no name', () => {
		const tier = tierView({ id: 1, maxLevel: 5, levelRewards: [reward(5, 999)] });
		const rows = buildLadder(tier, ATTRIBUTES, resolveSkill);
		expect(rows[4].isMilestone).toBe(true);
		expect(rows[4].skillName).toBeUndefined();
	});
});

/* ── trainedBy ─────────────────────────────────────────────────────────────── */

describe('trainedBy', () => {
	it('resolves contribution skill ids to distinct names in order', () => {
		const contributions = [contribution(100), contribution(200)];
		expect(trainedBy(contributions, resolveSkill)).toEqual(['Fireball', 'Ember Strike']);
	});

	it('dedupes a skill that contributes at several home tiers', () => {
		const contributions = [contribution(100, 0), contribution(100, 1), contribution(200, 0)];
		expect(trainedBy(contributions, resolveSkill)).toEqual(['Fireball', 'Ember Strike']);
	});

	it('drops a contribution whose skill cannot be resolved rather than showing a blank chip', () => {
		const contributions = [contribution(100), contribution(999)];
		expect(trainedBy(contributions, resolveSkill)).toEqual(['Fireball']);
	});

	it('is empty when there are no contributions', () => {
		expect(trainedBy([], resolveSkill)).toEqual([]);
	});
});
