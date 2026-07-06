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

	describe('reflected damage (#1330)', () => {
		it('floats a player reflection over the enemy with the reflect glyph, label, and amount', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({ target: 'enemy', kind: 'reflect', amount: 33 });

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.textContent).toContain('33');
			expect(floater.querySelector('.floater-label')?.textContent).toBe('REFLECT');
			// The shared combat-log reflect glyph is an inline SVG, not a PNG outcome icon.
			expect(floater.querySelector('.floater-glyph svg')).not.toBeNull();
			expect(floater.querySelector('img.floater-icon')).toBeNull();
			// A player-side reflect lands on the enemy in the brand accent (mirrors the log line's hue).
			expect(floater.getAttribute('style')).toContain('var(--accent)');
		});

		it('floats an enemy reflection over the player in the enemy hue', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'player', testId: 'player-floaters' } });
			emit({ target: 'player', kind: 'reflect', amount: 12 });

			const floater = getByTestId('player-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.textContent).toContain('12');
			expect(floater.querySelector('.floater-glyph svg')).not.toBeNull();
			expect(floater.getAttribute('style')).toContain('var(--enemy-accent)');
		});
	});

	describe('typed damage (#1320)', () => {
		it('tints a typed plain hit by its damage type and shows the type icon', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({ target: 'enemy', kind: 'hit', amount: 45, damageType: EDamageType.Fire });

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.textContent).toContain('45');
			expect(floater.getAttribute('style')).toContain('var(--dmg-fire)');
			// The damage-type PNG tags the type (no outcome icon for a plain hit).
			expect(floater.querySelector('img.floater-icon')?.getAttribute('src')).toContain('Fire.png');
		});

		it('keeps a physical hit icon-free (the untyped baseline) but still tints it neutral', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({ target: 'enemy', kind: 'hit', amount: 12, damageType: EDamageType.Physical });

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.getAttribute('style')).toContain('var(--dmg-physical)');
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

		it('shows an absorbed hit as a positive heal in the regen hue with no damage-type icon', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({ target: 'enemy', kind: 'hit', amount: -20, damageType: EDamageType.Fire });

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.textContent).toContain('+20');
			expect(floater.getAttribute('style')).toContain('var(--health-remaining-color)');
			expect(floater.querySelector('img.floater-icon')).toBeNull();
		});

		it('omits the typed ratio bar for a multi-typed absorbed hit', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({
				target: 'enemy',
				kind: 'hit',
				amount: -30,
				damageType: EDamageType.Fire,
				portions: [
					{ type: EDamageType.Fire, weight: 60 },
					{ type: EDamageType.Water, weight: 40 }
				]
			});

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.textContent).toContain('+30');
			expect(floater.querySelector('img.floater-icon')).toBeNull();
			expect(floater.querySelector('.floater-ratio')).toBeNull();
		});
	});

	describe('multi-typed split bar (#1343)', () => {
		it('draws a segmented ratio bar beneath a multi-typed hit, tinted per portion', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({
				target: 'enemy',
				kind: 'hit',
				amount: 50,
				// The number reads as its PrimaryDamageType; the bar carries the exact split.
				damageType: EDamageType.Physical,
				portions: [
					{ type: EDamageType.Physical, weight: 60 },
					{ type: EDamageType.Fire, weight: 40 }
				]
			});

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.textContent).toContain('50');
			// The number stays coloured by the primary type, like a single-typed hit.
			expect(floater.getAttribute('style')).toContain('var(--dmg-physical)');
			const segments = floater.querySelectorAll('.floater-ratio .seg');
			expect(segments).toHaveLength(2);
			expect(segments[0].getAttribute('style')).toContain('var(--dmg-physical)');
			expect(segments[1].getAttribute('style')).toContain('var(--dmg-fire)');
		});

		it('keeps a single-typed hit clean — no ratio bar', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({
				target: 'enemy',
				kind: 'hit',
				amount: 12,
				damageType: EDamageType.Fire,
				portions: [{ type: EDamageType.Fire, weight: 1 }]
			});

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.querySelector('.floater-ratio')).toBeNull();
		});

		it('omits the bar entirely when an event carries no portions (a reflect/dodge)', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({ target: 'enemy', kind: 'hit', amount: 30, damageType: EDamageType.Water });

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.querySelector('.floater-ratio')).toBeNull();
		});

		it('scales the bar to a three-portion split', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({
				target: 'enemy',
				kind: 'hit',
				amount: 70,
				damageType: EDamageType.Physical,
				portions: [
					{ type: EDamageType.Physical, weight: 1 },
					{ type: EDamageType.Fire, weight: 1 },
					{ type: EDamageType.Wind, weight: 1 }
				]
			});

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.querySelectorAll('.floater-ratio .seg')).toHaveLength(3);
		});

		it('still draws the split (and crit styling) on a multi-typed crit', () => {
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({
				target: 'enemy',
				kind: 'crit',
				amount: 120,
				damageType: EDamageType.Fire,
				portions: [
					{ type: EDamageType.Fire, weight: 70 },
					{ type: EDamageType.Water, weight: 30 }
				]
			});

			const floater = getByTestId('enemy-floaters').querySelector('.floater') as HTMLElement;
			expect(floater.classList.contains('crit')).toBe(true);
			expect(floater.querySelector('.floater-label')?.textContent).toBe('CRIT');
			expect(floater.querySelectorAll('.floater-ratio .seg')).toHaveLength(2);
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

	describe('hidden tab (#1598)', () => {
		afterEach(() => {
			delete (document as unknown as Record<string, unknown>).hidden;
		});

		it('skips spawning a floater while the tab is hidden', () => {
			Object.defineProperty(document, 'hidden', { configurable: true, get: () => true });
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({ target: 'enemy', kind: 'hit', amount: 247 });

			expect(getByTestId('enemy-floaters').querySelectorAll('.floater')).toHaveLength(0);
		});

		it('resumes spawning once the tab is visible again', () => {
			let hidden = true;
			Object.defineProperty(document, 'hidden', { configurable: true, get: () => hidden });
			const { getByTestId } = render(CombatFloaters, { props: { side: 'enemy', testId: 'enemy-floaters' } });
			emit({ target: 'enemy', kind: 'hit', amount: 247 });
			expect(getByTestId('enemy-floaters').querySelectorAll('.floater')).toHaveLength(0);

			hidden = false;
			emit({ target: 'enemy', kind: 'hit', amount: 99 });
			expect(getByTestId('enemy-floaters').querySelectorAll('.floater')).toHaveLength(1);
		});
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
