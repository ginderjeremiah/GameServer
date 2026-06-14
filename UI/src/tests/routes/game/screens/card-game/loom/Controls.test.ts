import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { CardGameView } from '$routes/game/screens/card-game/card-game-view.svelte';
import Controls from '$routes/game/screens/card-game/loom/Controls.svelte';

afterEach(cleanup);

describe('Controls', () => {
	it('exposes the reflex meter as a 0-100 progressbar of the agility reserve', () => {
		const view = new CardGameView();
		view.game.reflex = 60.7;
		const { container } = render(Controls, { props: { view } });

		const meter = container.querySelector('.reflexmeter') as HTMLElement;
		expect(meter.getAttribute('role')).toBe('progressbar');
		expect(meter.getAttribute('aria-label')).toBe('Agility reserve');
		expect(meter.getAttribute('aria-valuenow')).toBe('61');
		expect(meter.getAttribute('aria-valuemin')).toBe('0');
		expect(meter.getAttribute('aria-valuemax')).toBe('100');
	});
});
