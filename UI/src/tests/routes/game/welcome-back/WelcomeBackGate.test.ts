import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import type { IOfflineProgressModel } from '$lib/api';

// staticData is read for the zone name and the challenge/reward catalogues. Hoisted so the vi.mock
// factory (lifted to the top of the file) can reference it without a temporal-dead-zone error.
const { staticData } = vi.hoisted(() => ({
	staticData: {
		zones: [
			{ id: 0, name: 'Verdant Hollow' },
			{ id: 1, name: 'Ashen Wastes' }
		],
		challenges: [{ id: 0, name: 'First Blood', rewardSkillId: 0 }],
		items: [],
		itemMods: [],
		skills: [{ id: 0, name: 'Firebolt' }]
	}
}));
vi.mock('$stores', () => ({ staticData }));

import WelcomeBackGate from '$routes/game/welcome-back/WelcomeBackGate.svelte';

afterEach(cleanup);

const summary = (overrides: Partial<IOfflineProgressModel> = {}): IOfflineProgressModel => ({
	awayMs: (3 * 60 + 12) * 60_000,
	autoChallengeBoss: false,
	zoneId: 1,
	battlesWon: 42,
	battlesLost: 3,
	battlesDrawn: 1,
	totalExp: 12500,
	levelsGained: 2,
	statPointsGained: 10,
	hasProgress: true,
	completedChallenges: [],
	...overrides
});

describe('WelcomeBackGate', () => {
	it('renders the away duration, mode, zone, and battle/reward figures', () => {
		render(WelcomeBackGate, { props: { summary: summary(), onEnter: vi.fn() } });

		expect(screen.getByText('3h 12m')).toBeTruthy();
		expect(screen.getByText(/Idle farming/)).toBeTruthy();
		expect(screen.getByText(/Ashen Wastes/)).toBeTruthy();
		expect(screen.getByText('42')).toBeTruthy(); // battles won
		expect(screen.getByText('12500')).toBeTruthy(); // exp earned
		expect(screen.getByText('2')).toBeTruthy(); // levels gained
	});

	it('labels boss farming when the persisted mode is auto-challenge-boss', () => {
		render(WelcomeBackGate, { props: { summary: summary({ autoChallengeBoss: true }), onEnter: vi.fn() } });
		expect(screen.getByText(/Boss farming/)).toBeTruthy();
	});

	it('lists completed challenges with the reward they unlocked', () => {
		render(WelcomeBackGate, {
			props: { summary: summary({ completedChallenges: [{ challengeId: 0, rewardSkillId: 0 }] }), onEnter: vi.fn() }
		});

		expect(screen.getByText('Challenges completed')).toBeTruthy();
		expect(screen.getByText('First Blood')).toBeTruthy();
		expect(screen.getByText('Firebolt')).toBeTruthy();
	});

	it('omits the challenges section when none completed', () => {
		render(WelcomeBackGate, { props: { summary: summary(), onEnter: vi.fn() } });
		expect(screen.queryByText('Challenges completed')).toBeNull();
	});

	it('calls onEnter when the Enter button is clicked', async () => {
		const onEnter = vi.fn();
		render(WelcomeBackGate, { props: { summary: summary(), onEnter } });

		await fireEvent.click(screen.getByTestId('welcome-back-enter'));

		expect(onEnter).toHaveBeenCalledTimes(1);
	});
});
