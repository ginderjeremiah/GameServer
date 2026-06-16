import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { CardGameView } from '$routes/game/screens/card-game/card-game-view.svelte';
import Controls from '$routes/game/screens/card-game/loom/Controls.svelte';

afterEach(cleanup);

describe('Controls', () => {
	it('begins a held Reflex on a pointerdown of the reflex button (works for touch and mouse)', async () => {
		const view = new CardGameView();
		const setReflex = vi.spyOn(view, 'setReflex');
		const { container } = render(Controls, { props: { view } });

		await fireEvent.pointerDown(container.querySelector('.btn.reflex') as HTMLElement);
		expect(setReflex).toHaveBeenCalledWith(true);
	});

	it('exposes the reflex meter as a 0-100 progressbar of the agility reserve', () => {
		const view = new CardGameView();
		view.game.reflex = 60.7;
		const { container } = render(Controls, { props: { view } });

		const meter = container.querySelector('[aria-label="Agility reserve"]') as HTMLElement;
		expect(meter.getAttribute('role')).toBe('progressbar');
		expect(meter.getAttribute('aria-valuenow')).toBe('61');
		expect(meter.getAttribute('aria-valuemin')).toBe('0');
		expect(meter.getAttribute('aria-valuemax')).toBe('100');
	});
});
