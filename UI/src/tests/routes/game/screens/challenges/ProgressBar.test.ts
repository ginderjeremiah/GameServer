import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';

import ProgressBar from '$routes/game/screens/challenges/ProgressBar.svelte';

afterEach(cleanup);

describe('ProgressBar — a11y', () => {
	it('exposes progressbar semantics with the percent as aria-valuenow', () => {
		const { container } = render(ProgressBar, { props: { percent: 42, accent: 'var(--accent)' } });
		const track = container.querySelector('.bar-track') as HTMLElement;
		expect(track.getAttribute('role')).toBe('progressbar');
		expect(track.getAttribute('aria-valuenow')).toBe('42');
		expect(track.getAttribute('aria-valuemin')).toBe('0');
		expect(track.getAttribute('aria-valuemax')).toBe('100');
	});

	it('clamps aria-valuenow to the 0–100 range', () => {
		const over = render(ProgressBar, { props: { percent: 150, accent: 'var(--accent)' } });
		expect((over.container.querySelector('.bar-track') as HTMLElement).getAttribute('aria-valuenow')).toBe('100');
		cleanup();

		const under = render(ProgressBar, { props: { percent: -20, accent: 'var(--accent)' } });
		expect((under.container.querySelector('.bar-track') as HTMLElement).getAttribute('aria-valuenow')).toBe('0');
	});
});
