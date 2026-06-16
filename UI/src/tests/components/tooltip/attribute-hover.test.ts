// @vitest-environment jsdom
import { describe, it, expect, vi } from 'vitest';
import { EAttribute } from '$lib/api';
import { attributeHover } from '$components/tooltip/attribute-hover';

const makeController = () => ({ show: vi.fn(), move: vi.fn(), hide: vi.fn() });

describe('attributeHover action', () => {
	it('shows anchored at the cursor on hover and hides on leave', () => {
		const node = document.createElement('div');
		const controller = makeController();
		const action = attributeHover(node, { controller, id: EAttribute.Strength });

		const enter = new MouseEvent('mouseenter');
		node.dispatchEvent(enter);
		expect(controller.show).toHaveBeenCalledWith(EAttribute.Strength, enter);

		node.dispatchEvent(new MouseEvent('mousemove'));
		expect(controller.move).toHaveBeenCalledTimes(1);

		node.dispatchEvent(new MouseEvent('mouseleave'));
		expect(controller.hide).toHaveBeenCalledTimes(1);

		action.destroy();
	});

	it('shows anchored off the element on focus and hides on blur', () => {
		const node = document.createElement('button');
		const controller = makeController();
		const action = attributeHover(node, { controller, id: EAttribute.Agility });

		node.dispatchEvent(new FocusEvent('focus'));
		expect(controller.show).toHaveBeenCalledWith(EAttribute.Agility, node);

		node.dispatchEvent(new FocusEvent('blur'));
		expect(controller.hide).toHaveBeenCalledTimes(1);

		action.destroy();
	});

	it('tracks the latest controller/id after update and stops after destroy', () => {
		const node = document.createElement('div');
		const first = makeController();
		const action = attributeHover(node, { controller: first, id: EAttribute.Strength });

		const second = makeController();
		action.update({ controller: second, id: EAttribute.Luck });

		node.dispatchEvent(new MouseEvent('mouseenter'));
		expect(first.show).not.toHaveBeenCalled();
		expect(second.show).toHaveBeenCalledWith(EAttribute.Luck, expect.anything());

		action.destroy();
		node.dispatchEvent(new MouseEvent('mouseenter'));
		expect(second.show).toHaveBeenCalledTimes(1);
	});

	it('no-ops safely when no controller is provided', () => {
		const node = document.createElement('div');
		const action = attributeHover(node, { controller: undefined, id: EAttribute.Strength });
		expect(() => node.dispatchEvent(new MouseEvent('mouseenter'))).not.toThrow();
		action.destroy();
	});
});
