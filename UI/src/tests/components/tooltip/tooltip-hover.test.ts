// @vitest-environment jsdom
import { describe, it, expect, vi } from 'vitest';
import { tooltipHover } from '$components/tooltip/tooltip-hover';

const makeController = () => ({ describedById: 'tooltip-1', show: vi.fn(), move: vi.fn(), hide: vi.fn() });

describe('tooltipHover action', () => {
	it('shows anchored at the cursor on hover, repositions on move, hides on leave', () => {
		const node = document.createElement('div');
		const controller = makeController();
		const action = tooltipHover(node, { controller, payload: 'strength' });

		const enter = new MouseEvent('mouseenter');
		node.dispatchEvent(enter);
		expect(controller.show).toHaveBeenCalledWith('strength', enter);

		node.dispatchEvent(new MouseEvent('mousemove'));
		expect(controller.move).toHaveBeenCalledTimes(1);

		node.dispatchEvent(new MouseEvent('mouseleave'));
		expect(controller.hide).toHaveBeenCalledTimes(1);

		action.destroy();
	});

	it('shows anchored off the element on keyboard focus and hides on blur', () => {
		const node = document.createElement('button');
		const controller = makeController();
		const action = tooltipHover(node, { controller, payload: 'agility' });

		// A preceding keydown marks the focus as keyboard-driven, so it anchors off the element's box.
		document.dispatchEvent(new Event('keydown'));
		node.dispatchEvent(new FocusEvent('focus'));
		expect(controller.show).toHaveBeenCalledWith('agility', node);

		node.dispatchEvent(new FocusEvent('blur'));
		expect(controller.hide).toHaveBeenCalledTimes(1);

		action.destroy();
	});

	it('does not re-anchor on a mouse-click focus, leaving the tooltip tracking the cursor (#880)', () => {
		const node = document.createElement('button');
		const controller = makeController();
		const action = tooltipHover(node, { controller, payload: 'agility' });

		// A mouse click hovers (tooltip shown at the cursor) and then focuses the element; the focus
		// must not re-anchor the tooltip off the box, or it would jump away from the pointer.
		const enter = new MouseEvent('mouseenter');
		node.dispatchEvent(enter);
		expect(controller.show).toHaveBeenCalledTimes(1);
		expect(controller.show).toHaveBeenLastCalledWith('agility', enter);

		document.dispatchEvent(new Event('pointerdown'));
		node.dispatchEvent(new FocusEvent('focus'));
		// No additional show: the focus was a no-op, so the hover's cursor anchor still stands.
		expect(controller.show).toHaveBeenCalledTimes(1);

		action.destroy();
	});

	it('tracks the latest controller/payload after update and stops after destroy', () => {
		const node = document.createElement('div');
		const first = makeController();
		const action = tooltipHover(node, { controller: first, payload: 'strength' });

		const second = makeController();
		action.update({ controller: second, payload: 'luck' });

		node.dispatchEvent(new MouseEvent('mouseenter'));
		expect(first.show).not.toHaveBeenCalled();
		expect(second.show).toHaveBeenCalledWith('luck', expect.anything());

		action.destroy();
		node.dispatchEvent(new MouseEvent('mouseenter'));
		expect(second.show).toHaveBeenCalledTimes(1);
	});

	it('no-ops safely when no controller is provided', () => {
		const node = document.createElement('div');
		const action = tooltipHover(node, { controller: undefined, payload: 'strength' });
		expect(() => node.dispatchEvent(new MouseEvent('mouseenter'))).not.toThrow();
		action.destroy();
	});
});
