import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import BossHpBar from '$routes/game/screens/fight/boss/BossHpBar.svelte';

afterEach(cleanup);

describe('BossHpBar', () => {
	it('exposes the bar as a progressbar with rounded current/max health and value text', () => {
		const { getByTestId } = render(BossHpBar, { props: { currentHealth: 80.6, maxHealth: 100 } });

		const bar = getByTestId('boss-hp-bar');
		expect(bar.getAttribute('role')).toBe('progressbar');
		expect(bar.getAttribute('aria-label')).toBe('Boss health');
		expect(bar.getAttribute('aria-valuenow')).toBe('81');
		expect(bar.getAttribute('aria-valuemin')).toBe('0');
		expect(bar.getAttribute('aria-valuemax')).toBe('100');
		expect(bar.getAttribute('aria-valuetext')).toBe('80.6 / 100');
	});

	it('renders the current/max health text', () => {
		const { getByTestId } = render(BossHpBar, { props: { currentHealth: 50, maxHealth: 200 } });
		expect(getByTestId('boss-hp-bar').querySelector('.hp-text')?.textContent).toContain('50 / 200');
	});
});
