// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import OutcomeBanner from '$routes/game/screens/card-game/loom/OutcomeBanner.svelte';

afterEach(cleanup);

describe('OutcomeBanner', () => {
	it('shows the victory headline and flavour line on a win', () => {
		const { container } = render(OutcomeBanner, { props: { outcome: 'win', sub: 'The Warden unravels.' } });
		const banner = container.querySelector('.banner') as HTMLElement;
		expect(banner.classList.contains('win')).toBe(true);
		expect(banner.querySelector('h2')?.textContent).toBe('VICTORY');
		expect(banner.querySelector('.bsub')?.textContent).toBe('The Warden unravels.');
	});

	it('shows the downed headline on a loss', () => {
		const { container } = render(OutcomeBanner, { props: { outcome: 'lose', sub: 'The present caught you.' } });
		const banner = container.querySelector('.banner') as HTMLElement;
		expect(banner.classList.contains('lose')).toBe(true);
		expect(banner.querySelector('h2')?.textContent).toBe('DOWNED');
	});

	it('announces the outcome as a polite live region', () => {
		const { container } = render(OutcomeBanner, { props: { outcome: 'win', sub: 'The Warden unravels.' } });
		const banner = container.querySelector('.banner') as HTMLElement;
		expect(banner.getAttribute('role')).toBe('status');
		expect(banner.getAttribute('aria-live')).toBe('polite');
	});
});
