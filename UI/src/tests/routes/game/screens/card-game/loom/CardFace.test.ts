import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { CARDS } from '$lib/card-game';
import { CardGameView } from '$routes/game/screens/card-game/card-game-view.svelte';
import CardFace from '$routes/game/screens/card-game/loom/CardFace.svelte';

afterEach(cleanup);

const renderCard = (view = new CardGameView()) => {
	const { container } = render(CardFace, { props: { card: CARDS.slash, index: 0, slot: 1, view } });
	return { view, card: container.querySelector('.card') as HTMLElement };
};

describe('CardFace', () => {
	it('begins a drag from a pointerdown, plumbing the pointer coordinates through to the view', () => {
		const { view, card } = renderCard();
		const beginDrag = vi.spyOn(view, 'beginDrag');
		fireEvent.pointerDown(card, { clientX: 42, clientY: 99 });
		expect(beginDrag).toHaveBeenCalledWith(0, 'slash', 42, 99);
	});

	it('no longer responds to a legacy mousedown — the gesture is pointer-based so touch devices work', () => {
		const { view, card } = renderCard();
		const beginDrag = vi.spyOn(view, 'beginDrag');
		fireEvent.mouseDown(card, { clientX: 42, clientY: 99 });
		expect(beginDrag).not.toHaveBeenCalled();
	});

	it('does not falsely announce a keyboard button to assistive tech (casting is via the 1–7 hotkeys)', () => {
		const { card } = renderCard();
		expect(card.getAttribute('role')).toBeNull();
		expect(card.getAttribute('tabindex')).toBeNull();
	});
});
