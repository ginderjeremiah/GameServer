import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { LoomGame } from '$lib/card-game';
import Combatants from '$routes/game/screens/card-game/loom/Combatants.svelte';

afterEach(cleanup);

describe('Combatants', () => {
	it('exposes the enemy and player HP bars as progressbars with value text', () => {
		const game = new LoomGame();
		game.enemyHP = 90.4;
		game.enemyMax = 120;
		game.playerHP = 40.6;
		game.playerMax = 80;
		const { container } = render(Combatants, { props: { game } });

		const enemy = container.querySelector('.bar.enemy') as HTMLElement;
		expect(enemy.getAttribute('role')).toBe('progressbar');
		expect(enemy.getAttribute('aria-label')).toBe('The Warden health');
		expect(enemy.getAttribute('aria-valuenow')).toBe('90');
		expect(enemy.getAttribute('aria-valuemin')).toBe('0');
		expect(enemy.getAttribute('aria-valuemax')).toBe('120');
		expect(enemy.getAttribute('aria-valuetext')).toBe('90.4 / 120');

		const player = container.querySelector('.bar.player') as HTMLElement;
		expect(player.getAttribute('role')).toBe('progressbar');
		expect(player.getAttribute('aria-label')).toBe('Your health');
		expect(player.getAttribute('aria-valuenow')).toBe('41');
		expect(player.getAttribute('aria-valuemax')).toBe('80');
		expect(player.getAttribute('aria-valuetext')).toBe('40.6 / 80');
	});

	it('exposes the next-draw bar as a 0-100 progressbar of accumulated draw time', () => {
		const game = new LoomGame();
		// Half-way to the next draw → 50%.
		game.drawAcc = game.drawIntervalSec / 2;
		const { container } = render(Combatants, { props: { game } });

		const draw = container.querySelector('.drawbar') as HTMLElement;
		expect(draw.getAttribute('role')).toBe('progressbar');
		expect(draw.getAttribute('aria-label')).toBe('Next draw progress');
		expect(draw.getAttribute('aria-valuenow')).toBe('50');
		expect(draw.getAttribute('aria-valuemin')).toBe('0');
		expect(draw.getAttribute('aria-valuemax')).toBe('100');
	});

	it('clamps the next-draw bar to 100 when the accumulator overshoots the interval', () => {
		const game = new LoomGame();
		game.drawAcc = game.drawIntervalSec * 2;
		const { container } = render(Combatants, { props: { game } });

		const draw = container.querySelector('.drawbar') as HTMLElement;
		expect(draw.getAttribute('aria-valuenow')).toBe('100');
		expect((draw.querySelector('i') as HTMLElement).getAttribute('style')).toContain('width: 100%');
	});
});
