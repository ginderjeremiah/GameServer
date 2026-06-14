import { describe, it, expect } from 'vitest';
import { anchorPosition } from '$routes/game/screens/challenges/reward-tooltip-context';

describe('anchorPosition', () => {
	it('positions a pointer anchor at the cursor coordinates', () => {
		const ev = { clientX: 120, clientY: 45 } as MouseEvent;
		expect(anchorPosition(ev)).toEqual({ x: 120, y: 45 });
	});

	it('positions a focus anchor under the centre of the element box', () => {
		// Focus has no cursor, so the tooltip anchors off the element's rect instead.
		const el = document.createElement('button');
		el.getBoundingClientRect = () =>
			({
				left: 100,
				width: 40,
				bottom: 200,
				top: 180,
				right: 140,
				height: 20,
				x: 100,
				y: 180,
				toJSON: () => ({})
			}) as DOMRect;
		expect(anchorPosition(el)).toEqual({ x: 120, y: 200 });
	});
});
