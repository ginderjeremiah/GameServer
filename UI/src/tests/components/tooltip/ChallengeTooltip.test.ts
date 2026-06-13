import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EChallengeType, EItemCategory, ERarity, type IChallenge, type IItem, type IZone } from '$lib/api';

// The tooltip resolves the challenge, its gating-zone relationship and reward from the reference
// store, and reads completion from the player-challenge store; both are mocked with small fixtures.
const { staticData, playerChallenges } = vi.hoisted(() => ({
	staticData: {
		challenges: [] as IChallenge[],
		challengeTypes: [] as { id: number; name: string }[],
		zones: [] as IZone[],
		items: [] as (IItem | undefined)[],
		itemMods: [] as unknown[],
		skills: [] as unknown[]
	},
	playerChallenges: { isChallengeCompleted: vi.fn(() => false) }
}));

vi.mock('$stores', () => ({ staticData, playerChallenges }));

import ChallengeTooltip from '$components/tooltip/ChallengeTooltip.svelte';

const zone = (id: number, order: number, name: string, unlockChallengeId?: number): IZone =>
	({ id, name, description: '', order, levelMin: 1, levelMax: 10, bossLevel: 1, unlockChallengeId }) as IZone;

const setup = (over: Partial<IChallenge> = {}) => {
	staticData.challenges = [];
	staticData.challenges[5] = {
		id: 5,
		name: 'Cull the Swarm',
		description: 'Defeat 10 enemies',
		challengeTypeId: EChallengeType.EnemiesKilled,
		progressGoal: 10,
		...over
	} as IChallenge;
	staticData.challengeTypes = [{ id: EChallengeType.EnemiesKilled, name: 'Enemies Killed' }];
	staticData.zones = [zone(20, 2, 'Beta', 5)];
};

afterEach(() => {
	cleanup();
	playerChallenges.isChallengeCompleted.mockReset();
	playerChallenges.isChallengeCompleted.mockReturnValue(false);
});

describe('ChallengeTooltip', () => {
	it('renders nothing for an undefined challenge (the empty/hidden panel)', () => {
		setup();
		const { container } = render(ChallengeTooltip, { props: { challengeId: undefined } });
		expect(container.querySelector('.tt-title-name')).toBeNull();
	});

	it('shows the challenge type, name and requirement description', () => {
		setup();
		const { container } = render(ChallengeTooltip, { props: { challengeId: 5 } });
		expect((container.querySelector('.tt-category-label') as HTMLElement).textContent).toBe('Enemies Killed');
		expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('Cull the Swarm');
		expect((container.querySelector('.ct-desc') as HTMLElement).textContent).toBe('Defeat 10 enemies');
	});

	it('accents the panel border by the challenge type', () => {
		setup();
		const { container } = render(ChallengeTooltip, { props: { challengeId: 5 } });
		expect((container.querySelector('.tt-shell') as HTMLElement).getAttribute('style')).toContain(
			'var(--challenge-enemies-killed)'
		);
	});

	it('lists the zone the challenge unlocks', () => {
		setup();
		const { container } = render(ChallengeTooltip, { props: { challengeId: 5 } });
		const names = Array.from(container.querySelectorAll('.ct-name')).map((n) => n.textContent?.trim());
		expect(names).toContain('Beta');
	});

	it('seals a reward name while the challenge is incomplete, then reveals it once complete', () => {
		setup({ rewardItemId: 3 });
		staticData.items = [];
		staticData.items[3] = {
			id: 3,
			name: 'Iron Helm',
			itemCategoryId: EItemCategory.Helm,
			rarityId: ERarity.Rare
		} as IItem;

		const { container, unmount } = render(ChallengeTooltip, { props: { challengeId: 5 } });
		const sealed = container.querySelector('.ct-name.sealed') as HTMLElement;
		expect(sealed.textContent?.trim()).toBe('???');
		// The teaser sub-label is still shown even while sealed.
		expect(container.textContent).toContain('Rare · Helm');
		unmount();

		playerChallenges.isChallengeCompleted.mockReturnValue(true);
		const { container: done } = render(ChallengeTooltip, { props: { challengeId: 5 } });
		expect(done.querySelector('.ct-name.sealed')).toBeNull();
		expect(done.textContent).toContain('Iron Helm');
	});
});
