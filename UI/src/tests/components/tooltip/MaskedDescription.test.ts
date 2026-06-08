import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import MaskedDescription from '$components/tooltip/MaskedDescription.svelte';

afterEach(cleanup);

describe('MaskedDescription', () => {
	it('renders one mask bar per supplied line width', () => {
		const { container } = render(MaskedDescription, {
			props: { accent: 'var(--rarity-rare)', lineWidths: [236, 188] }
		});
		const widths = [...container.querySelectorAll<HTMLElement>('.mask-bar')].map((b) => b.style.width);
		expect(widths).toEqual(['236px', '188px']);
	});

	it('falls back to a default two-line layout when no widths are given', () => {
		const { container } = render(MaskedDescription, { props: { accent: 'var(--rarity-rare)' } });
		expect(container.querySelectorAll('.mask-bar')).toHaveLength(2);
	});
});
