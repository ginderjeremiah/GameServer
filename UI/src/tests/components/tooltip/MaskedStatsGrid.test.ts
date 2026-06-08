import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import MaskedStatsGrid from '$components/tooltip/MaskedStatsGrid.svelte';

afterEach(cleanup);

describe('MaskedStatsGrid', () => {
	it('renders one mask bar and one ??? cell per row, so the count reads true', () => {
		const { container } = render(MaskedStatsGrid, {
			props: { rows: 3, barWidths: [70, 88, 58], accent: 'var(--rarity-rare)' }
		});
		expect(container.querySelectorAll('.mask-bar')).toHaveLength(3);
		const qmarks = container.querySelectorAll('.tt-qmark');
		expect(qmarks).toHaveLength(3);
		qmarks.forEach((q) => expect(q.textContent).toBe('???'));
	});

	it('cycles the bar widths across the rows', () => {
		const { container } = render(MaskedStatsGrid, {
			props: { rows: 4, barWidths: [10, 20], accent: 'var(--rarity-rare)' }
		});
		const widths = [...container.querySelectorAll<HTMLElement>('.mask-bar')].map((b) => b.style.width);
		expect(widths).toEqual(['10px', '20px', '10px', '20px']);
	});

	it('renders nothing when there are no rows', () => {
		const { container } = render(MaskedStatsGrid, {
			props: { rows: 0, barWidths: [70], accent: 'var(--rarity-rare)' }
		});
		expect(container.querySelectorAll('.mask-bar')).toHaveLength(0);
		expect(container.querySelectorAll('.tt-qmark')).toHaveLength(0);
	});
});
