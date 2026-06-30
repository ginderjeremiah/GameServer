import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EDamageType } from '$lib/api';
import DamageRatioBar from '$components/DamageRatioBar.svelte';

afterEach(cleanup);

describe('DamageRatioBar', () => {
	it('renders one segment per portion, tinted by its leaf-type hue', () => {
		const { container } = render(DamageRatioBar, {
			props: {
				portions: [
					{ type: EDamageType.Physical, weight: 60 },
					{ type: EDamageType.Fire, weight: 40 }
				]
			}
		});

		const segments = container.querySelectorAll('.seg');
		expect(segments).toHaveLength(2);
		expect(segments[0].getAttribute('style')).toContain('var(--dmg-physical)');
		expect(segments[1].getAttribute('style')).toContain('var(--dmg-fire)');
	});

	it('sizes each segment in proportion to its weight via flex-grow', () => {
		const { container } = render(DamageRatioBar, {
			props: {
				portions: [
					{ type: EDamageType.Physical, weight: 3 },
					{ type: EDamageType.Water, weight: 1 }
				]
			}
		});

		const segments = container.querySelectorAll('.seg');
		expect((segments[0] as HTMLElement).style.flexGrow).toBe('3');
		expect((segments[1] as HTMLElement).style.flexGrow).toBe('1');
	});

	it('scales to a three-portion split', () => {
		const { container } = render(DamageRatioBar, {
			props: {
				portions: [
					{ type: EDamageType.Physical, weight: 1 },
					{ type: EDamageType.Fire, weight: 1 },
					{ type: EDamageType.Wind, weight: 1 }
				]
			}
		});

		expect(container.querySelectorAll('.seg')).toHaveLength(3);
	});

	it('drives the bar thickness from the height prop', () => {
		const { container } = render(DamageRatioBar, {
			props: { portions: [{ type: EDamageType.Physical, weight: 1 }], height: 3 }
		});

		const bar = container.querySelector('.ratio-bar') as HTMLElement;
		expect(bar.style.getPropertyValue('--ratio-bar-height')).toBe('3px');
	});
});
