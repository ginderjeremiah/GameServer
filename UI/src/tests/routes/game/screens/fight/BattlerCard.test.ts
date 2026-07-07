import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EAttribute, type IAttribute } from '$lib/api';
import { getTutorialAnchor } from '$components';

// The card renders a Skills -> SkillTooltip subtree, which imports the battle engine and the
// reference-data store at module load; both are mocked even though no hover happens here. The player
// card also reads the player manager (level/XP for the header bar) and the combat-float hook.
const { mockBattleEngine, mockPlayerManager, mockEnemyManager, onCombatFloat, staticData } = vi.hoisted(() => ({
	mockBattleEngine: { getOpponent: vi.fn() },
	mockPlayerManager: { level: 7, exp: 280, nextLevelThreshold: 700, playerRating: 120 },
	mockEnemyManager: { currentEnemy: undefined as { enemyRating: number } | undefined },
	onCombatFloat: vi.fn(() => () => {}),
	staticData: { attributes: [] as IAttribute[] }
}));

vi.mock('$lib/engine', () => ({
	battleEngine: mockBattleEngine,
	playerManager: mockPlayerManager,
	enemyManager: mockEnemyManager,
	onCombatFloat
}));
vi.mock('$stores', () => ({ staticData }));

import BattlerCard from '$routes/game/screens/fight/BattlerCard.svelte';
import { makeBattler, makeSkill } from './fight-fixtures';

afterEach(cleanup);

describe('BattlerCard', () => {
	it('renders the player name, account level and current/max health', () => {
		const battler = makeBattler({
			name: 'Aelara',
			attributes: [{ attributeId: EAttribute.MaxHealth, amount: 100 }],
			currentHealth: 80
		});
		// The player card shows the player's account level (from the player manager), not the battler's.
		mockPlayerManager.level = 5;
		const { getByTestId } = render(BattlerCard, { props: { battler, side: 'player' } });

		const card = getByTestId('player-card');
		expect(card.querySelector('.battler-name')?.textContent).toBe('Aelara');
		expect(card.querySelector('.player-level')?.textContent).toContain('5');
		expect(card.querySelector('.hp-text')?.textContent).toContain('80 / 100');
	});

	it('formats a fractional max health instead of rendering floating-point noise', () => {
		const battler = makeBattler({
			attributes: [{ attributeId: EAttribute.MaxHealth, amount: 412.50000000001 }],
			currentHealth: 73.4
		});
		const { getByTestId } = render(BattlerCard, { props: { battler, side: 'player' } });

		expect(getByTestId('player-card').querySelector('.hp-text')?.textContent).toContain('73.4 / 412.5');
	});

	it('exposes the HP bar as a progressbar with rounded current/max health', () => {
		const battler = makeBattler({
			name: 'Aelara',
			attributes: [{ attributeId: EAttribute.MaxHealth, amount: 100 }],
			currentHealth: 80.6
		});
		const { container } = render(BattlerCard, { props: { battler, side: 'player' } });

		const bar = container.querySelector('.hp-bar') as HTMLElement;
		expect(bar.getAttribute('role')).toBe('progressbar');
		expect(bar.getAttribute('aria-label')).toBe('Aelara health');
		expect(bar.getAttribute('aria-valuenow')).toBe('81');
		expect(bar.getAttribute('aria-valuemin')).toBe('0');
		expect(bar.getAttribute('aria-valuemax')).toBe('100');
	});

	it('sizes the health bar to the remaining health percentage', () => {
		const battler = makeBattler({
			attributes: [{ attributeId: EAttribute.MaxHealth, amount: 200 }],
			currentHealth: 50
		});
		const { container } = render(BattlerCard, { props: { battler, side: 'player' } });
		// 50 / 200 = 25%.
		expect((container.querySelector('.hp-remaining') as HTMLElement).getAttribute('style')).toContain('width: 25%');
	});

	it('clamps the health bar to full when the battler has no max health', () => {
		const battler = makeBattler({
			attributes: [{ attributeId: EAttribute.MaxHealth, amount: 0 }],
			currentHealth: 0
		});
		const { container } = render(BattlerCard, { props: { battler, side: 'player' } });
		expect((container.querySelector('.hp-remaining') as HTMLElement).getAttribute('style')).toContain('width: 100%');
	});

	it('accents the card by side', () => {
		const player = render(BattlerCard, { props: { battler: makeBattler(), side: 'player' } });
		expect(player.getByTestId('player-card')).toBeTruthy();
		expect((player.container.querySelector('.accent-bar') as HTMLElement).getAttribute('style')).toContain(
			'var(--accent)'
		);
		cleanup();

		const enemy = render(BattlerCard, { props: { battler: makeBattler({ name: 'Dire Wolf' }), side: 'enemy' } });
		expect(enemy.getByTestId('enemy-card')).toBeTruthy();
		expect((enemy.container.querySelector('.accent-bar') as HTMLElement).getAttribute('style')).toContain(
			'var(--enemy-accent)'
		);
	});

	it("renders the battler's skills", () => {
		const battler = makeBattler();
		battler.skills = [makeSkill(battler, { name: 'Slash', iconPath: '/slash.png' })];
		const { getByAltText, getByText } = render(BattlerCard, { props: { battler, side: 'player' } });
		expect(getByAltText('Slash')).toBeTruthy();
		expect(getByText('Slash')).toBeTruthy();
	});

	it("shows the player's level and XP progress bar in the header", () => {
		mockPlayerManager.level = 7;
		mockPlayerManager.exp = 280;
		mockPlayerManager.nextLevelThreshold = 700;
		const { getByTestId } = render(BattlerCard, { props: { battler: makeBattler(), side: 'player' } });

		const card = getByTestId('player-card');
		expect(card.querySelector('.player-level')?.textContent).toContain('7');

		const xpBar = getByTestId('player-xp-bar');
		expect(xpBar.getAttribute('role')).toBe('progressbar');
		expect(xpBar.getAttribute('aria-valuenow')).toBe('280');
		expect(xpBar.getAttribute('aria-valuemax')).toBe('700');
		// 280 / 700 = 40%.
		expect((xpBar.querySelector('.xp-fill') as HTMLElement).getAttribute('style')).toContain('width: 40%');
	});

	it('shows a plain level on the enemy card with no XP bar', () => {
		const { getByTestId, queryByTestId } = render(BattlerCard, {
			props: { battler: makeBattler({ name: 'Dire Wolf', level: 9 }), side: 'enemy' }
		});
		expect(getByTestId('enemy-card').querySelector('.battler-level')?.textContent).toContain('9');
		expect(queryByTestId('player-xp-bar')).toBeNull();
	});

	it("shows the player's combat-power readout in the header", () => {
		mockPlayerManager.playerRating = 128;
		const { getByTestId } = render(BattlerCard, { props: { battler: makeBattler(), side: 'player' } });
		expect(getByTestId('player-power').textContent).toContain('128');
	});

	it("shows the enemy's combat-power readout once the current enemy has loaded", () => {
		mockPlayerManager.playerRating = 100;
		mockEnemyManager.currentEnemy = { enemyRating: 250 };
		const { getByTestId } = render(BattlerCard, {
			props: { battler: makeBattler({ name: 'Dire Wolf' }), side: 'enemy' }
		});
		// enemyRating (250) far exceeds playerRating (100), so the cue reads as the top "Dangerous" band.
		expect(getByTestId('enemy-power').textContent).toContain('250');
		expect(getByTestId('enemy-power').textContent).toContain('Dangerous');
		mockEnemyManager.currentEnemy = undefined;
	});

	it('renders no enemy power readout before the current enemy has loaded', () => {
		mockEnemyManager.currentEnemy = undefined;
		const { queryByTestId } = render(BattlerCard, {
			props: { battler: makeBattler({ name: 'Dire Wolf' }), side: 'enemy' }
		});
		expect(queryByTestId('enemy-power')).toBeNull();
	});

	it('registers its HP bar as a tutorial-tour anchor keyed by side', () => {
		const player = render(BattlerCard, { props: { battler: makeBattler(), side: 'player' } });
		expect(getTutorialAnchor('fight-hp-bar-player')).toBe(
			player.getByTestId('player-card').querySelector('.hp-bar-slot')
		);
		player.unmount();

		const enemy = render(BattlerCard, { props: { battler: makeBattler({ name: 'Dire Wolf' }), side: 'enemy' } });
		expect(getTutorialAnchor('fight-hp-bar-enemy')).toBe(enemy.getByTestId('enemy-card').querySelector('.hp-bar-slot'));
	});
});
