import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import TooltipStatsGrid from '$components/tooltip/TooltipStatsGrid.svelte';

afterEach(cleanup);

const valuesOf = (container: HTMLElement) =>
	[...container.querySelectorAll<HTMLElement>('.tt-stat-value')].map((v) => v.textContent?.trim());

describe('TooltipStatsGrid', () => {
	it('renders signed, verbatim values by default and accents by sign', () => {
		const { container } = render(TooltipStatsGrid, {
			props: {
				entries: [
					{ name: 'Strength', value: 5 },
					{ name: 'Defense', value: -2 }
				]
			}
		});
		const cells = [...container.querySelectorAll<HTMLElement>('.tt-stat-value')];
		expect(valuesOf(container)).toEqual(['+5', '-2']);
		expect(cells[0].classList.contains('positive')).toBe(true);
		expect(cells[1].classList.contains('negative')).toBe(true);
	});

	it('renders decimals verbatim when format is off', () => {
		const { container } = render(TooltipStatsGrid, {
			props: { entries: [{ name: 'Crit', value: 1.25 }] }
		});
		expect(valuesOf(container)).toEqual(['+1.25']);
	});

	it('renders an empty grid (no empty text) when entries is empty and no emptyText is given', () => {
		const { container } = render(TooltipStatsGrid, { props: { entries: [] } });
		expect(container.querySelector('.tt-stats-grid')).not.toBeNull();
		expect(container.querySelector('.tt-stat-empty')).toBeNull();
	});

	it('renders the empty state instead of the grid when emptyText is set and entries is empty', () => {
		const { container } = render(TooltipStatsGrid, {
			props: { entries: [], emptyText: 'No gear equipped yet.' }
		});
		expect(container.querySelector('.tt-stats-grid')).toBeNull();
		expect((container.querySelector('.tt-stat-empty') as HTMLElement).textContent).toBe('No gear equipped yet.');
	});

	it('renders the grid (not the empty state) when emptyText is set but entries are present', () => {
		const { container } = render(TooltipStatsGrid, {
			props: { entries: [{ name: 'Strength', value: 3 }], emptyText: 'No gear equipped yet.' }
		});
		expect(container.querySelector('.tt-stat-empty')).toBeNull();
		expect(valuesOf(container)).toEqual(['+3']);
	});

	it('formats non-integers to one decimal place and leaves integers whole when format is on', () => {
		const { container } = render(TooltipStatsGrid, {
			props: {
				entries: [
					{ name: 'Crit', value: 1.25 },
					{ name: 'Defense', value: -0.5 },
					{ name: 'Strength', value: 4 }
				],
				format: true
			}
		});
		expect(valuesOf(container)).toEqual(['+1.3', '-0.5', '+4']);
	});
});
