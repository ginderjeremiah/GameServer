import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { EChallengeType, ERarity, ESkillAcquisition, type ISkill } from '$lib/api';
import OverviewPane from '$routes/game/screens/challenges/OverviewPane.svelte';
import type {
	ChallengeVM,
	OverallSummary,
	ResolvedReward,
	TypeGroup
} from '$routes/game/screens/challenges/challenges-view.svelte';

afterEach(cleanup);

const sampleSkill: ISkill = {
	id: 5,
	name: 'Firebolt',
	baseDamage: 12,
	description: 'Hurls a bolt of fire.',
	damageMultipliers: [],
	effects: [],
	cooldownMs: 3000,
	iconPath: '',
	rarityId: ERarity.Common,
	acquisition: ESkillAcquisition.Player
};

const reward: ResolvedReward = {
	kind: 'skill',
	revealed: true,
	rarity: ERarity.Common,
	accent: 'var(--accent-light)',
	glow: 'var(--rarity-common-glow)',
	name: 'Firebolt',
	sub: 'Skill',
	skill: sampleSkill
};

const challenge = (over: Partial<ChallengeVM> & { id: number }): ChallengeVM =>
	({
		name: `Challenge ${over.id}`,
		description: '',
		typeId: EChallengeType.EnemiesKilled,
		goal: 10,
		progress: 1,
		completed: false,
		state: 'active',
		prog: { percent: 10 },
		unit: '',
		typeAccent: 'var(--challenge-enemies-killed)',
		reward,
		...over
	}) as unknown as ChallengeVM;

const group: TypeGroup = {
	typeId: EChallengeType.EnemiesKilled,
	label: 'Enemies Killed',
	accent: 'var(--challenge-enemies-killed)',
	items: [challenge({ id: 0 })]
};

const summary: OverallSummary = { total: 1, done: 0, active: 1, pct: 0 };

const renderPane = (onPick = vi.fn()) =>
	render(OverviewPane, { props: { summary, nextUp: null, groups: [group], onPick } });

describe('OverviewPane — accessible type-card (no nested buttons)', () => {
	it('renders the type-card as a presentational container with a real <button> overlay', () => {
		const { container } = renderPane();
		const card = container.querySelector('.type-card')!;
		expect(card.tagName).toBe('DIV');
		expect(card.querySelector('.overlay-button')?.tagName).toBe('BUTTON');
	});

	it('keeps the reward chip a sibling of the overlay, never nested inside another button', () => {
		const { container } = renderPane();
		const card = container.querySelector('.type-card')!;
		const chip = card.querySelector('.chip');
		expect(chip?.tagName).toBe('BUTTON');
		// A <button> inside the overlay <button> would be invalid HTML — the bug this fixes.
		expect(card.querySelector('.overlay-button button')).toBeNull();
		expect(card.querySelector('button button')).toBeNull();
	});

	it('picks the challenge type when the card overlay is activated', async () => {
		const onPick = vi.fn();
		const { container } = renderPane(onPick);
		await fireEvent.click(container.querySelector('.type-card .overlay-button')!);
		expect(onPick).toHaveBeenCalledWith(EChallengeType.EnemiesKilled);
	});
});
