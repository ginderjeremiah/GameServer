import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import HpBar from '$components/HpBar.svelte';

afterEach(cleanup);

describe('HpBar', () => {
	it('exposes the bar as a progressbar with rounded current/max health and value text', () => {
		const { container } = render(HpBar, {
			props: { currentHealth: 80.6, maxHealth: 100, ariaLabel: 'Aelara health' }
		});

		const bar = container.querySelector('.hp-bar') as HTMLElement;
		expect(bar.getAttribute('role')).toBe('progressbar');
		expect(bar.getAttribute('aria-label')).toBe('Aelara health');
		expect(bar.getAttribute('aria-valuenow')).toBe('81');
		expect(bar.getAttribute('aria-valuemin')).toBe('0');
		expect(bar.getAttribute('aria-valuemax')).toBe('100');
		expect(bar.getAttribute('aria-valuetext')).toBe('80.6 / 100');
	});

	it('formats both sides of the health text, trimming floating-point noise', () => {
		const { container } = render(HpBar, {
			props: { currentHealth: 412.55, maxHealth: 523.4999999999, ariaLabel: 'Boss health' }
		});
		expect(container.querySelector('.hp-text')?.textContent).toContain('412.55 / 523.5');
	});

	it('sizes the remaining bar to the health percentage', () => {
		const { container } = render(HpBar, {
			props: { currentHealth: 50, maxHealth: 200, ariaLabel: 'health' }
		});
		// 50 / 200 = 25%.
		expect((container.querySelector('.hp-remaining') as HTMLElement).getAttribute('style')).toContain('width: 25%');
	});

	it('clamps to full when there is no max health', () => {
		const { container } = render(HpBar, {
			props: { currentHealth: 0, maxHealth: 0, ariaLabel: 'health' }
		});
		expect((container.querySelector('.hp-remaining') as HTMLElement).getAttribute('style')).toContain('width: 100%');
	});

	it('clamps the bar to 100% when current health transiently exceeds max', () => {
		const { container } = render(HpBar, {
			props: { currentHealth: 150, maxHealth: 100, ariaLabel: 'health' }
		});
		expect((container.querySelector('.hp-remaining') as HTMLElement).getAttribute('style')).toContain('width: 100%');
	});

	it('clamps the bar to 0% for negative current health', () => {
		const { container } = render(HpBar, {
			props: { currentHealth: -50, maxHealth: 100, ariaLabel: 'health' }
		});
		expect((container.querySelector('.hp-remaining') as HTMLElement).getAttribute('style')).toContain('width: 0%');
	});

	it('renders NaN health without throwing (geometry is numeric, not a throwing formatter)', () => {
		// The old `formatNum`-based geometry threw on NaN; the numeric clamp must render instead.
		expect(() =>
			render(HpBar, {
				props: { currentHealth: Number.NaN, maxHealth: 100, ariaLabel: 'health' }
			})
		).not.toThrow();
	});

	it('overlays a phase pip for each provided percentage', () => {
		const { container } = render(HpBar, {
			props: { currentHealth: 50, maxHealth: 100, ariaLabel: 'Boss health', phasePips: [25, 50, 75] }
		});
		const pips = container.querySelectorAll('.phase-pip');
		expect(pips).toHaveLength(3);
		expect((pips[1] as HTMLElement).getAttribute('style')).toContain('left: 50%');
	});

	it('applies the tall variant and a test id when requested', () => {
		const { getByTestId } = render(HpBar, {
			props: { currentHealth: 10, maxHealth: 20, ariaLabel: 'Boss health', tall: true, testId: 'boss-hp-bar' }
		});
		expect(getByTestId('boss-hp-bar').classList.contains('tall')).toBe(true);
	});
});
