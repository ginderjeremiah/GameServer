import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EAttribute, ERarity, EItemModType, type IItemMod, type ISkill } from '$lib/api';
import RewardAffordance from '$routes/game/screens/challenges/RewardAffordance.svelte';
import type { ResolvedReward } from '$routes/game/screens/challenges/challenges-view.svelte';

const sampleMod: IItemMod = {
	id: 213,
	name: 'Honed',
	description: 'Honed',
	itemModTypeId: EItemModType.Prefix,
	rarityId: ERarity.Rare,
	attributes: [],
	tags: []
};

const modReward = (revealed: boolean): ResolvedReward => ({
	kind: 'mod',
	revealed,
	rarity: ERarity.Rare,
	accent: 'var(--rarity-rare)',
	glow: 'var(--rarity-rare-glow)',
	name: 'Honed',
	sub: 'Rare · Prefix',
	mod: sampleMod
});

const sampleSkill: ISkill = {
	id: 5,
	name: 'Firebolt',
	baseDamage: 12,
	description: 'Hurls a bolt of fire.',
	damageMultipliers: [{ attributeId: EAttribute.Intellect, multiplier: 1.5 }],
	effects: [],
	cooldownMs: 3000,
	iconPath: ''
};

const skillReward = (revealed: boolean): ResolvedReward => ({
	kind: 'skill',
	revealed,
	rarity: ERarity.Common,
	accent: 'var(--accent-light)',
	glow: '0',
	name: 'Firebolt',
	sub: 'Skill',
	skill: sampleSkill
});

afterEach(cleanup);

describe('RewardAffordance', () => {
	it('hides the name behind ??? while sealed and shows the teaser sub-line', () => {
		const { getByText, queryByText } = render(RewardAffordance, {
			props: { reward: modReward(false), variant: 'tile' }
		});
		expect(getByText('???')).toBeTruthy();
		expect(queryByText('Honed')).toBeNull();
		expect(getByText('Rare · Prefix')).toBeTruthy();
	});

	it('reveals the reward name once unlocked', () => {
		const { getByText, queryByText } = render(RewardAffordance, {
			props: { reward: modReward(true), variant: 'tile' }
		});
		expect(getByText('Honed')).toBeTruthy();
		expect(queryByText('???')).toBeNull();
	});

	it('renders the compact chip variant with the same seal/reveal rule', () => {
		const sealed = render(RewardAffordance, { props: { reward: modReward(false), variant: 'chip' } });
		expect(sealed.getByText('???')).toBeTruthy();
		cleanup();

		const revealed = render(RewardAffordance, { props: { reward: modReward(true), variant: 'chip' } });
		expect(revealed.getByText('Honed')).toBeTruthy();
	});

	it('seals then reveals a skill reward name with the "Skill" teaser sub-line', () => {
		const sealed = render(RewardAffordance, { props: { reward: skillReward(false), variant: 'tile' } });
		expect(sealed.getByText('???')).toBeTruthy();
		expect(sealed.queryByText('Firebolt')).toBeNull();
		expect(sealed.getByText('Skill')).toBeTruthy();
		cleanup();

		const revealed = render(RewardAffordance, { props: { reward: skillReward(true), variant: 'tile' } });
		expect(revealed.getByText('Firebolt')).toBeTruthy();
	});

	it('shows a fallback when there is no reward', () => {
		const { getByText } = render(RewardAffordance, { props: { reward: null } });
		expect(getByText('No reward')).toBeTruthy();
	});
});
