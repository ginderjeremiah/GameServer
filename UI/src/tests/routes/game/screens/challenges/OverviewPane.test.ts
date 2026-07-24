import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { EChallengeType, ERarity, EItemModType, type IItemMod } from '$lib/api';
import OverviewPane from '$routes/game/screens/challenges/OverviewPane.svelte';
import type {
	ChallengeVM,
	OverallSummary,
	ResolvedReward,
	TypeGroup
} from '$routes/game/screens/challenges/challenges-view.svelte';

afterEach(cleanup);

const sampleMod: IItemMod = {
	id: 5,
	name: 'of Fury',
	itemModTypeId: EItemModType.Suffix,
	rarityId: ERarity.Common
} as IItemMod;

const reward: ResolvedReward = {
	kind: 'mod',
	revealed: true,
	rarity: ERarity.Common,
	accent: 'var(--accent-light)',
	glow: 'var(--rarity-common-glow)',
	name: 'of Fury',
	sub: 'Common · Suffix',
	mod: sampleMod
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

	it('raises the reward strip only while it holds an interactive chip', () => {
		const { container } = renderPane();
		expect(container.querySelector('.type-card-reward')?.classList.contains('raised')).toBe(true);
	});

	it('leaves the reward strip flat when the next challenge has no reward, so the card stays clickable', () => {
		// The strip renders a non-interactive "No reward" span here; raising it over the full-bleed overlay
		// would swallow every click landing on it and leave a dead zone on the card.
		const noReward: TypeGroup = { ...group, items: [challenge({ id: 0, reward: null })] };
		const { container } = render(OverviewPane, {
			props: { summary, nextUp: null, groups: [noReward], onPick: vi.fn() }
		});
		const strip = container.querySelector('.type-card-reward')!;
		expect(strip.querySelector('.no-reward')).not.toBeNull();
		expect(strip.classList.contains('raised')).toBe(false);
	});

	it('leaves the reward strip flat when every challenge of the type is unlocked', () => {
		const allDone: TypeGroup = { ...group, items: [challenge({ id: 0, state: 'done', completed: true })] };
		const { container } = render(OverviewPane, {
			props: { summary, nextUp: null, groups: [allDone], onPick: vi.fn() }
		});
		expect(container.querySelector('.type-card-reward')?.classList.contains('raised')).toBe(false);
	});
});
