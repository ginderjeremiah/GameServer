import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import TooltipDescription from '$components/tooltip/TooltipDescription.svelte';

afterEach(cleanup);

describe('TooltipDescription', () => {
	it('renders the real description text by default', () => {
		const { container } = render(TooltipDescription, { props: { text: 'Wreathed in flame.' } });
		expect((container.querySelector('.tt-description') as HTMLElement).textContent).toBe('Wreathed in flame.');
		expect(container.querySelector('.mask-bar')).toBeNull();
	});

	describe('masked', () => {
		it('renders one mask bar per supplied line width instead of the text', () => {
			const { container } = render(TooltipDescription, {
				props: { masked: true, accent: 'var(--rarity-rare)', lineWidths: [236, 188], text: 'Hidden flavour.' }
			});
			const widths = [...container.querySelectorAll<HTMLElement>('.mask-bar')].map((b) => b.style.width);
			expect(widths).toEqual(['236px', '188px']);
			// The real text is never rendered in the teaser.
			expect(container.querySelector('.tt-description')).toBeNull();
			expect(container.textContent).not.toContain('Hidden flavour.');
		});

		it('falls back to a default two-line layout when no widths are given', () => {
			const { container } = render(TooltipDescription, { props: { masked: true, accent: 'var(--rarity-rare)' } });
			expect(container.querySelectorAll('.mask-bar')).toHaveLength(2);
		});
	});
});
