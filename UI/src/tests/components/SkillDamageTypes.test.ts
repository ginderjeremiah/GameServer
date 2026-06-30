import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EDamageType } from '$lib/api';
import SkillDamageTypes from '$components/SkillDamageTypes.svelte';

afterEach(cleanup);

describe('SkillDamageTypes', () => {
	it('shows a single-typed skill as one clean row — no percentage, no ratio bar', () => {
		const { container } = render(SkillDamageTypes, {
			props: { portions: [{ type: EDamageType.Fire, weight: 1 }] }
		});

		const rows = container.querySelectorAll('.dt-row');
		expect(rows).toHaveLength(1);
		expect(rows[0].querySelector('.dt-name')?.textContent).toBe('Fire');
		expect(rows[0].querySelector('.dt-ico')?.getAttribute('src')).toContain('Fire.png');
		// A lone type has no meaningful split, so neither the percentage nor the bar is shown.
		expect(container.querySelector('.dt-pct')).toBeNull();
		expect(container.querySelector('.ratio-bar')).toBeNull();
	});

	it('shows a two-typed mix as the ratio bar plus a labelled, percentage-tagged row per type', () => {
		const { container } = render(SkillDamageTypes, {
			props: {
				portions: [
					{ type: EDamageType.Physical, weight: 60 },
					{ type: EDamageType.Fire, weight: 40 }
				]
			}
		});

		expect(container.querySelectorAll('.ratio-bar .seg')).toHaveLength(2);

		const rows = container.querySelectorAll('.dt-row');
		expect(rows).toHaveLength(2);
		expect(rows[0].querySelector('.dt-name')?.textContent).toBe('Physical');
		expect(rows[0].querySelector('.dt-pct')?.textContent).toBe('60%');
		expect(rows[1].querySelector('.dt-name')?.textContent).toBe('Fire');
		expect(rows[1].querySelector('.dt-pct')?.textContent).toBe('40%');
	});

	it('normalizes raw weights to percentages', () => {
		const { container } = render(SkillDamageTypes, {
			props: {
				portions: [
					{ type: EDamageType.Physical, weight: 3 },
					{ type: EDamageType.Water, weight: 1 }
				]
			}
		});

		const pcts = [...container.querySelectorAll('.dt-pct')].map((el) => el.textContent);
		expect(pcts).toEqual(['75%', '25%']);
	});

	it('scales to a three-portion mix', () => {
		const { container } = render(SkillDamageTypes, {
			props: {
				portions: [
					{ type: EDamageType.Physical, weight: 1 },
					{ type: EDamageType.Fire, weight: 1 },
					{ type: EDamageType.Wind, weight: 1 }
				]
			}
		});

		expect(container.querySelectorAll('.ratio-bar .seg')).toHaveLength(3);
		expect(container.querySelectorAll('.dt-row')).toHaveLength(3);
		// Even thirds round to 33% each (independent rounding; players read the bar for the exact split).
		expect([...container.querySelectorAll('.dt-pct')].map((el) => el.textContent)).toEqual(['33%', '33%', '33%']);
	});
});
