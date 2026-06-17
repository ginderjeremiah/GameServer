import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { EAttribute, ERarity, EItemModType, type IItemMod, type ISkill } from '$lib/api';
import RewardAffordance from '$routes/game/screens/challenges/RewardAffordance.svelte';
import RewardAffordanceFixture from './RewardAffordanceFixture.svelte';
import type { RewardTooltipController } from '$routes/game/screens/challenges/reward-tooltip-context';
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
	glow: null,
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

	describe('keyboard / screen-reader accessibility', () => {
		const stubController = (): RewardTooltipController => ({
			describedById: 'tooltip-reward',
			show: vi.fn(),
			move: vi.fn(),
			hide: vi.fn()
		});

		it('renders each variant as a focusable button so keyboard users can reach it', () => {
			for (const variant of ['tile', 'chip'] as const) {
				const { getByRole } = render(RewardAffordance, { props: { reward: modReward(true), variant } });
				// A real <button> is in the tab order without any explicit tabindex.
				expect(getByRole('button').tabIndex).toBe(0);
				cleanup();
			}
		});

		it('labels a revealed reward with its name and teaser for assistive tech', () => {
			const { getByRole } = render(RewardAffordance, { props: { reward: modReward(true), variant: 'tile' } });
			expect(getByRole('button').getAttribute('aria-label')).toBe('Reward: Honed, Rare · Prefix');
		});

		it('labels a sealed reward as sealed rather than leaking its name', () => {
			const { getByRole } = render(RewardAffordance, { props: { reward: modReward(false), variant: 'tile' } });
			expect(getByRole('button').getAttribute('aria-label')).toBe('Sealed reward: Rare · Prefix');
		});

		it('hides the decorative tile info icon from assistive tech', () => {
			const { container } = render(RewardAffordance, { props: { reward: modReward(true), variant: 'tile' } });
			expect(container.querySelector('.tile-info')?.getAttribute('aria-hidden')).toBe('true');
		});

		it('opens the shared tooltip on focus, anchored to the focused element', async () => {
			const controller = stubController();
			const reward = modReward(true);
			const { getByRole } = render(RewardAffordanceFixture, { props: { reward, variant: 'tile', controller } });

			const button = getByRole('button');
			await fireEvent.focus(button);

			// Focus drives the same controller hover does, anchored to the element (not a cursor).
			expect(controller.show).toHaveBeenCalledWith(reward, button);
		});

		it('closes the shared tooltip on blur', async () => {
			const controller = stubController();
			const { getByRole } = render(RewardAffordanceFixture, {
				props: { reward: modReward(true), variant: 'chip', controller }
			});

			const button = getByRole('button');
			await fireEvent.focus(button);
			await fireEvent.blur(button);

			expect(controller.hide).toHaveBeenCalledTimes(1);
		});

		it('associates the trigger with the shared tooltip via aria-describedby', () => {
			const controller = stubController();
			const { getByRole } = render(RewardAffordanceFixture, {
				props: { reward: modReward(true), variant: 'tile', controller }
			});

			// So a screen reader announces the reward explanation (not just the name) on focus.
			expect(getByRole('button').getAttribute('aria-describedby')).toBe('tooltip-reward');
		});
	});
});
