import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { DEFAULT_MAX_BATTLE_MS } from '$lib/api/types/game-constants';
import BattleTimer from '$routes/game/screens/fight/BattleTimer.svelte';

afterEach(cleanup);

describe('BattleTimer', () => {
	it('renders the elapsed clock and the formatted limit', () => {
		const { getByTestId } = render(BattleTimer, { props: { elapsedMs: 48000, maxMs: DEFAULT_MAX_BATTLE_MS } });
		const timer = getByTestId('battle-timer');
		expect(timer.querySelector('.readout')?.textContent).toBe('0:48');
		expect(timer.querySelector('.caption')?.textContent).toContain('/ 2:00 limit');
	});

	it('zero-pads the seconds', () => {
		const { getByTestId } = render(BattleTimer, { props: { elapsedMs: 65000, maxMs: DEFAULT_MAX_BATTLE_MS } });
		expect(getByTestId('battle-timer').querySelector('.readout')?.textContent).toBe('1:05');
	});

	it('sizes the progress track to the elapsed fraction', () => {
		const { container } = render(BattleTimer, { props: { elapsedMs: 60000, maxMs: DEFAULT_MAX_BATTLE_MS } });
		// 60s / 120s = 50%.
		expect((container.querySelector('.bar-fill') as HTMLElement).getAttribute('style')).toContain('width: 50%');
	});

	it('uses the accent fill before the final seconds and the warning hue within them', () => {
		const early = render(BattleTimer, { props: { elapsedMs: 60000, maxMs: DEFAULT_MAX_BATTLE_MS } });
		// The fill colour rides the Bar's --bar-fill token: accent before the warning window.
		expect(early.container.innerHTML).toContain('--bar-fill: var(--accent)');
		expect(early.container.innerHTML).not.toContain('--bar-fill: var(--warning)');
		cleanup();

		// Final 20 seconds (>= 100000ms of the 120000ms cap) flips to the warning hue.
		const late = render(BattleTimer, { props: { elapsedMs: 105000, maxMs: DEFAULT_MAX_BATTLE_MS } });
		expect(late.container.innerHTML).toContain('--bar-fill: var(--warning)');
	});

	it('caps the readout at the limit instead of overshooting', () => {
		const { getByTestId } = render(BattleTimer, { props: { elapsedMs: 119950, maxMs: DEFAULT_MAX_BATTLE_MS } });
		// Just shy of the cap still reads under the limit, never 2:00+ before timeout.
		expect(getByTestId('battle-timer').querySelector('.readout')?.textContent).toBe('1:59');
	});

	it('shows the timeout/draw state once the cap is reached', () => {
		const { getByTestId, queryByTestId } = render(BattleTimer, {
			props: { elapsedMs: DEFAULT_MAX_BATTLE_MS, maxMs: DEFAULT_MAX_BATTLE_MS }
		});
		expect(getByTestId('battle-timer-timeout').textContent).toBe('TIMEOUT');
		expect(getByTestId('battle-timer').textContent).toContain('draw');
		// The running readout is gone in the timeout state.
		expect(queryByTestId('battle-timer')?.querySelector('.readout')?.classList.contains('timeout')).toBe(true);
	});
});
