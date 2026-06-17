import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import EnemyCooldown from '$routes/game/screens/fight/EnemyCooldown.svelte';

afterEach(cleanup);

describe('EnemyCooldown', () => {
	it('rounds the remaining time up to whole seconds', () => {
		const { getByTestId } = render(EnemyCooldown, { props: { remainingMs: 4200, totalMs: 5000 } });
		expect(getByTestId('enemy-cooldown').querySelector('.readout')?.textContent).toBe('5s');
	});

	it('labels the readout as the next enemy', () => {
		const { getByTestId } = render(EnemyCooldown, { props: { remainingMs: 3000, totalMs: 5000 } });
		expect(getByTestId('enemy-cooldown').querySelector('.caption')?.textContent).toBe('next enemy');
	});

	it('fills the track by the elapsed fraction of the cooldown', () => {
		const { container } = render(EnemyCooldown, { props: { remainingMs: 2000, totalMs: 5000 } });
		// 3000ms of 5000ms elapsed = 60% filled.
		expect((container.querySelector('.track-fill') as HTMLElement).getAttribute('style')).toContain('width: 60%');
	});

	it('clamps a tiny overshoot to 0s rather than a negative count', () => {
		const { getByTestId } = render(EnemyCooldown, { props: { remainingMs: -50, totalMs: 5000 } });
		expect(getByTestId('enemy-cooldown').querySelector('.readout')?.textContent).toBe('0s');
	});

	it('guards a zero total against a divide-by-zero, showing an empty track', () => {
		const { container } = render(EnemyCooldown, { props: { remainingMs: 0, totalMs: 0 } });
		expect((container.querySelector('.track-fill') as HTMLElement).getAttribute('style')).toContain('width: 0%');
	});
});
