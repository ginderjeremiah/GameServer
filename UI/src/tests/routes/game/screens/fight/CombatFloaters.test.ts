import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { flushSync } from 'svelte';
import type { CombatFloatEvent } from '$lib/engine';

// CombatFloaters subscribes to the engine's combat-float hook at init. Capture the registered
// callback so the test can drive events directly; the real `$lib/common` formatNum is used.
const { onCombatFloat, captured } = vi.hoisted(() => {
	const captured = { emit: undefined as ((event: CombatFloatEvent) => void) | undefined };
	return {
		captured,
		onCombatFloat: vi.fn((callback: (event: CombatFloatEvent) => void) => {
			captured.emit = callback;
			return () => {};
		})
	};
});

vi.mock('$lib/engine', () => ({ onCombatFloat }));

import CombatFloaters from '$routes/game/screens/fight/CombatFloaters.svelte';

const emit = (event: CombatFloatEvent) => {
	captured.emit?.(event);
	flushSync();
};

afterEach(() => {
	cleanup();
	captured.emit = undefined;
	vi.useRealTimers();
});

describe('CombatFloaters', () => {
	it('spawns a number for an event targeting its side', () => {
		const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
		emit({ target: 'enemy', kind: 'hit', amount: 247 });

		const floaters = getByTestId('enemy-floaters').querySelectorAll('.floater');
		expect(floaters).toHaveLength(1);
		expect(floaters[0].textContent).toContain('247');
		// A player hit lands on the enemy in the brand accent.
		expect((floaters[0] as HTMLElement).getAttribute('style')).toContain('var(--accent)');
	});

	it('ignores events targeting the other side', () => {
		const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
		emit({ target: 'player', kind: 'hit', amount: 182 });
		expect(getByTestId('enemy-floaters').querySelectorAll('.floater')).toHaveLength(0);
	});

	it('labels and gold-colours a crit', () => {
		const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
		emit({ target: 'enemy', kind: 'crit', amount: 438 });

		const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
		expect(floater.textContent).toContain('438');
		expect(floater.querySelector('.floater-label')?.textContent).toBe('CRIT');
		expect(floater.classList.contains('crit')).toBe(true);
		expect(floater.getAttribute('style')).toContain('var(--gold)');
	});

	it('shows a dodge as a label with no number', () => {
		const { getByTestId } = render(CombatFloaters, { props: { side: 'player', testId: 'player-floaters' } });
		emit({ target: 'player', kind: 'dodge' });

		const floater = getByTestId('player-floaters').querySelector('.floater') as HTMLElement;
		expect(floater.querySelector('.floater-label')?.textContent).toBe('DODGE');
		// No amount span beyond the icon + label.
		expect(floater.textContent?.trim()).toBe('DODGE');
	});

	it('colours an incoming hit on the player with the enemy hue', () => {
		const { getByTestId } = render(CombatFloaters, { props: { side: 'player', testId: 'player-floaters' } });
		emit({ target: 'player', kind: 'hit', amount: 182 });

		const floater = getByTestId('player-floaters').querySelector('.floater') as HTMLElement;
		expect(floater.getAttribute('style')).toContain('var(--enemy-accent)');
	});

	it('removes a floater after its animation completes', () => {
		vi.useFakeTimers();
		const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
		emit({ target: 'enemy', kind: 'hit', amount: 99 });
		expect(getByTestId('enemy-floaters').querySelectorAll('.floater')).toHaveLength(1);

		vi.advanceTimersByTime(1600);
		flushSync();
		expect(getByTestId('enemy-floaters').querySelectorAll('.floater')).toHaveLength(0);
	});
});
