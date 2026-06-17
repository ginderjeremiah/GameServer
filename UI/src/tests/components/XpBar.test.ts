import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { flushSync } from 'svelte';
import XpBar from '$components/XpBar.svelte';

afterEach(cleanup);

describe('XpBar', () => {
	it('exposes the bar as a progressbar with rounded exp/threshold and value text', () => {
		const { container } = render(XpBar, { props: { level: 7, exp: 280.4, nextLevelThreshold: 700 } });

		const bar = container.querySelector('.xp-bar') as HTMLElement;
		expect(bar.getAttribute('role')).toBe('progressbar');
		expect(bar.getAttribute('aria-label')).toBe('Experience');
		expect(bar.getAttribute('aria-valuenow')).toBe('280');
		expect(bar.getAttribute('aria-valuemin')).toBe('0');
		expect(bar.getAttribute('aria-valuemax')).toBe('700');
		expect(bar.getAttribute('aria-valuetext')).toBe('280.4 / 700 XP');
	});

	it('sizes the fill to the experience percentage', () => {
		const { container } = render(XpBar, { props: { level: 7, exp: 280, nextLevelThreshold: 700 } });
		// 280 / 700 = 40%.
		expect((container.querySelector('.xp-fill') as HTMLElement).getAttribute('style')).toContain('width: 40%');
	});

	it('clamps the fill to full when exp meets or exceeds the threshold', () => {
		const { container } = render(XpBar, { props: { level: 7, exp: 900, nextLevelThreshold: 700 } });
		expect((container.querySelector('.xp-fill') as HTMLElement).getAttribute('style')).toContain('width: 100%');
	});

	it('keeps the fill empty when the threshold is zero', () => {
		const { container } = render(XpBar, { props: { level: 0, exp: 0, nextLevelThreshold: 0 } });
		expect((container.querySelector('.xp-fill') as HTMLElement).getAttribute('style')).toContain('width: 0%');
	});

	it('accepts a custom aria label and test id', () => {
		const { getByTestId } = render(XpBar, {
			props: { level: 3, exp: 50, nextLevelThreshold: 300, ariaLabel: 'Aelara experience', testId: 'player-xp-bar' }
		});
		const bar = getByTestId('player-xp-bar');
		expect(bar.getAttribute('aria-label')).toBe('Aelara experience');
	});

	it('flashes the fill only when the level rises, not on first mount', async () => {
		const { container, rerender } = render(XpBar, { props: { level: 7, exp: 50, nextLevelThreshold: 700 } });
		const fill = container.querySelector('.xp-fill') as HTMLElement;
		// The mount guard records the baseline level without flashing.
		expect(fill.style.animation).not.toContain('levelup-flash');

		await rerender({ level: 8, exp: 10, nextLevelThreshold: 800 });
		flushSync();
		expect(fill.style.animation).toContain('levelup-flash');
	});

	it('does not flash when the level is unchanged', async () => {
		const { container, rerender } = render(XpBar, { props: { level: 7, exp: 50, nextLevelThreshold: 700 } });
		const fill = container.querySelector('.xp-fill') as HTMLElement;

		// Only exp moved within the same level — no level-up, so no flash.
		await rerender({ level: 7, exp: 120, nextLevelThreshold: 700 });
		flushSync();
		expect(fill.style.animation).not.toContain('levelup-flash');
	});
});
