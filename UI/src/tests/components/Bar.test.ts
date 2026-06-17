import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import Bar from '$components/Bar.svelte';

afterEach(cleanup);

describe('Bar', () => {
	it('exposes progressbar semantics with a rounded aria-valuenow', () => {
		const { container } = render(Bar, { props: { value: 80.6, max: 100, ariaLabel: 'Charge' } });
		const bar = container.querySelector('.bar-track') as HTMLElement;
		expect(bar.getAttribute('role')).toBe('progressbar');
		expect(bar.getAttribute('aria-label')).toBe('Charge');
		expect(bar.getAttribute('aria-valuenow')).toBe('81');
		expect(bar.getAttribute('aria-valuemin')).toBe('0');
		expect(bar.getAttribute('aria-valuemax')).toBe('100');
	});

	it('omits aria-label and aria-valuetext when not provided', () => {
		const { container } = render(Bar, { props: { value: 50 } });
		const bar = container.querySelector('.bar-track') as HTMLElement;
		expect(bar.hasAttribute('aria-label')).toBe(false);
		expect(bar.hasAttribute('aria-valuetext')).toBe(false);
	});

	it('sets aria-label and aria-valuetext when provided', () => {
		const { container } = render(Bar, { props: { value: 50, ariaLabel: 'HP', valueText: '50 / 100' } });
		const bar = container.querySelector('.bar-track') as HTMLElement;
		expect(bar.getAttribute('aria-label')).toBe('HP');
		expect(bar.getAttribute('aria-valuetext')).toBe('50 / 100');
	});

	it('sizes the fill to the value within an explicit min/max range', () => {
		const { container } = render(Bar, { props: { value: 6, min: 2, max: 10 } });
		// (6 - 2) / (10 - 2) = 50%.
		expect((container.querySelector('.bar-fill') as HTMLElement).getAttribute('style')).toContain('width: 50%');
	});

	it('clamps the fill width and aria-valuenow above the max', () => {
		const { container } = render(Bar, { props: { value: 150, max: 100 } });
		const bar = container.querySelector('.bar-track') as HTMLElement;
		expect(bar.getAttribute('aria-valuenow')).toBe('100');
		expect((container.querySelector('.bar-fill') as HTMLElement).getAttribute('style')).toContain('width: 100%');
	});

	it('clamps the fill width and aria-valuenow below the min', () => {
		const { container } = render(Bar, { props: { value: -20, max: 100 } });
		const bar = container.querySelector('.bar-track') as HTMLElement;
		expect(bar.getAttribute('aria-valuenow')).toBe('0');
		expect((container.querySelector('.bar-fill') as HTMLElement).getAttribute('style')).toContain('width: 0%');
	});

	it('renders an empty fill when the value range has zero width', () => {
		const { container } = render(Bar, { props: { value: 5, min: 0, max: 0 } });
		expect((container.querySelector('.bar-fill') as HTMLElement).getAttribute('style')).toContain('width: 0%');
	});

	it('renders role="presentation" and omits progressbar ARIA when presentational', () => {
		const { container } = render(Bar, {
			props: { value: 50, max: 100, ariaLabel: 'Share', valueText: '50%', presentational: true }
		});
		const bar = container.querySelector('.bar-track') as HTMLElement;
		expect(bar.getAttribute('role')).toBe('presentation');
		expect(bar.hasAttribute('aria-valuenow')).toBe(false);
		expect(bar.hasAttribute('aria-valuemin')).toBe(false);
		expect(bar.hasAttribute('aria-valuemax')).toBe(false);
		expect(bar.hasAttribute('aria-label')).toBe(false);
		expect(bar.hasAttribute('aria-valuetext')).toBe(false);
	});

	it('still sizes the fill to the value when presentational', () => {
		const { container } = render(Bar, { props: { value: 3, max: 4, presentational: true } });
		expect((container.querySelector('.bar-fill') as HTMLElement).getAttribute('style')).toContain('width: 75%');
	});

	it('applies a test id and renders overlay children inside the track', () => {
		const overlay = createRawSnippet(() => ({ render: () => '<div data-testid="cursor"></div>' }));
		const { getByTestId } = render(Bar, { props: { value: 40, testId: 'meter', children: overlay } });
		const bar = getByTestId('meter');
		expect(bar.classList.contains('bar-track')).toBe(true);
		expect(bar.querySelector('[data-testid="cursor"]')).toBeTruthy();
	});
});
