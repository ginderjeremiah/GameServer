import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { flushSync } from 'svelte';
import { EDamageType } from '$lib/api';
import type { CombatFloatEvent } from '$lib/engine';

// CombatFloaters subscribes to the engine's combat-float hook at init. Capture the registered
// callback so the test can drive events directly; the real `$lib/common` formatNum is used.
const { onCombatFloat, captured } = vi.hoisted(() => {
	const captured = {
		emit: undefined as ((event: CombatFloatEvent) => void) | undefined,
		unsubscribe: vi.fn()
	};
	return {
		captured,
		onCombatFloat: vi.fn((callback: (event: CombatFloatEvent) => void) => {
			captured.emit = callback;
			return captured.unsubscribe;
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
	captured.unsubscribe.mockClear();
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
		// A plain hit carries no outcome icon.
		expect(floaters[0].querySelector('img.floater-icon')).toBeNull();
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
		expect(floater.querySelector('img.floater-icon')?.getAttribute('src')).toContain('Critical Damage.png');
	});

	it('shows a dodge as a label with no number', () => {
		const { getByTestId } = render(CombatFloaters, { props: { side: 'player', testId: 'player-floaters' } });
		emit({ target: 'player', kind: 'dodge' });

		const floater = getByTestId('player-floaters').querySelector('.floater') as HTMLElement;
		expect(floater.querySelector('.floater-label')?.textContent).toBe('DODGE');
		expect(floater.querySelector('img.floater-icon')?.getAttribute('src')).toContain('Dodge.png');
		// No amount span beyond the icon + label.
		expect(floater.textContent?.trim()).toBe('DODGE');
	});

	it('colours an incoming hit on the player with the enemy hue', () => {
		const { getByTestId } = render(CombatFloaters, { props: { side: 'player', testId: 'player-floaters' } });
		emit({ target: 'player', kind: 'hit', amount: 182 });

		const floater = getByTestId('player-floaters').querySelector('.floater') as HTMLElement;
		expect(floater.getAttribute('style')).toContain('var(--enemy-accent)');
	});

	describe('typed damage (#1320)', () => {
		it('tints a typed plain hit by its damage type and shows the type glyph', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({ target: 'enemy', kind: 'hit', amount: 45, damageType: EDamageType.Fire });

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.textContent).toContain('45');
			expect(floater.getAttribute('style')).toContain('var(--dmg-fire)');
			// An inline type glyph (not a PNG outcome icon) tags the type.
			expect(floater.querySelector('.floater-glyph svg')).not.toBeNull();
			expect(floater.querySelector('img.floater-icon')).toBeNull();
		});

		it('keeps a physical hit glyph-free (the untyped baseline) but still tints it neutral', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({ target: 'enemy', kind: 'hit', amount: 12, damageType: EDamageType.Physical });

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.getAttribute('style')).toContain('var(--dmg-physical)');
			expect(floater.querySelector('.floater-glyph')).toBeNull();
			expect(floater.querySelector('img.floater-icon')).toBeNull();
		});

		it('tints a typed crit by its type (not gold) while keeping the crit icon and label', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({ target: 'enemy', kind: 'crit', amount: 90, damageType: EDamageType.Water });

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.getAttribute('style')).toContain('var(--dmg-water)');
			expect(floater.querySelector('.floater-label')?.textContent).toBe('CRIT');
			expect(floater.querySelector('img.floater-icon')?.getAttribute('src')).toContain('Critical Damage.png');
		});

		it('shows an absorbed hit as a positive heal in the regen hue', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({ target: 'enemy', kind: 'hit', amount: -20, damageType: EDamageType.Fire });

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.textContent).toContain('+20');
			expect(floater.getAttribute('style')).toContain('var(--health-remaining-color)');
		});
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

	it('drives the CSS animation duration from the JS constant via a custom property', () => {
		const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
		// The removal timer (DURATION_MS) and the dmg-rise animation read one source so they can't drift.
		expect(getByTestId('enemy-floaters').style.getPropertyValue('--float-duration')).toBe('1500ms');
	});

	it('clears pending removal timers and unsubscribes on unmount (no write-after-destroy)', () => {
		vi.useFakeTimers();
		const clearSpy = vi.spyOn(globalThis, 'clearTimeout');
		const { unmount } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
		// Spawn a few floaters so there are pending removal timers, then tear the component down.
		emit({ target: 'enemy', kind: 'hit', amount: 1 });
		emit({ target: 'enemy', kind: 'crit', amount: 2 });

		unmount();

		expect(captured.unsubscribe).toHaveBeenCalledTimes(1);
		// Both pending removal timers are cleared, so neither callback fires after the component is gone.
		expect(clearSpy).toHaveBeenCalledTimes(2);
		expect(() => {
			vi.advanceTimersByTime(1600);
			flushSync();
		}).not.toThrow();
		clearSpy.mockRestore();
	});
});
