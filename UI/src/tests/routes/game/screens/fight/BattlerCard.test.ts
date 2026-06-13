import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EAttribute, type IAttribute } from '$lib/api';

// The card renders a Skills -> SkillTooltip subtree, which imports the battle engine and the
// reference-data store at module load; both are mocked even though no hover happens here.
const { mockBattleEngine, staticData } = vi.hoisted(() => ({
	mockBattleEngine: { getOpponent: vi.fn() },
	staticData: { attributes: [] as IAttribute[] }
}));

vi.mock('$lib/engine', () => ({ battleEngine: mockBattleEngine }));
vi.mock('$stores', () => ({ staticData }));

import BattlerCard from '$routes/game/screens/fight/BattlerCard.svelte';
import { makeBattler, makeSkill } from './fight-fixtures';

afterEach(cleanup);

describe('BattlerCard', () => {
	it('renders the battler name, level and current/max health', () => {
		const battler = makeBattler({
			name: 'Aelara',
			level: 12,
			attributes: [{ attributeId: EAttribute.MaxHealth, amount: 100 }],
			currentHealth: 80
		});
		const { getByTestId } = render(BattlerCard, { props: { battler, side: 'player' } });

		const card = getByTestId('player-card');
		expect(card.querySelector('.battler-name')?.textContent).toBe('Aelara');
		expect(card.querySelector('.battler-level')?.textContent).toContain('12');
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
});
