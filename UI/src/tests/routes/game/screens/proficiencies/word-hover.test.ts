// @vitest-environment jsdom
import { describe, it, expect, vi } from 'vitest';
import { wordHover } from '$routes/game/screens/proficiencies/word-hover';
import type { TierView } from '$routes/game/screens/proficiencies/proficiencies-lexicon';

const makeController = () => ({ describedById: 'tooltip-1', show: vi.fn(), move: vi.fn(), hide: vi.fn() });

const tierView = (id: number): TierView => ({
	id,
	name: `Tier ${id}`,
	pathOrdinal: 0,
	level: 0,
	maxLevel: 10,
	xp: 0,
	xpForNext: 100,
	state: 'unlocked',
	frontier: false,
	milestoneLevels: [],
	decipher: 'undeciphered',
	word: `word${id}`,
	pronunciation: `pron${id}`,
	translation: `means${id}`,
	iconPath: ''
});

describe('wordHover action', () => {
	it('shows anchored at the cursor on hover, repositions on move, hides on leave', () => {
		const node = document.createElement('div');
		const controller = makeController();
		const tier = tierView(1);
		const action = wordHover(node, { controller, tier });

		const enter = new MouseEvent('mouseenter');
		node.dispatchEvent(enter);
		expect(controller.show).toHaveBeenCalledWith(tier, enter);

		node.dispatchEvent(new MouseEvent('mousemove'));
		expect(controller.move).toHaveBeenCalledTimes(1);

		node.dispatchEvent(new MouseEvent('mouseleave'));
		expect(controller.hide).toHaveBeenCalledTimes(1);

		action.destroy();
	});

	it('shows anchored off the element on keyboard focus and hides on blur', () => {
		const node = document.createElement('button');
		const controller = makeController();
		const tier = tierView(2);
		const action = wordHover(node, { controller, tier });

		// A preceding keydown marks the focus as keyboard-driven, so it anchors off the element's box.
		document.dispatchEvent(new Event('keydown'));
		node.dispatchEvent(new FocusEvent('focus'));
		expect(controller.show).toHaveBeenCalledWith(tier, node);

		node.dispatchEvent(new FocusEvent('blur'));
		expect(controller.hide).toHaveBeenCalledTimes(1);

		action.destroy();
	});

	it('does not re-anchor on a mouse-click focus, leaving the tooltip tracking the cursor (#880)', () => {
		const node = document.createElement('button');
		const controller = makeController();
		const tier = tierView(3);
		const action = wordHover(node, { controller, tier });

		const enter = new MouseEvent('mouseenter');
		node.dispatchEvent(enter);
		expect(controller.show).toHaveBeenCalledTimes(1);

		document.dispatchEvent(new Event('pointerdown'));
		node.dispatchEvent(new FocusEvent('focus'));
		// No additional show: the focus was a no-op, so the hover's cursor anchor still stands.
		expect(controller.show).toHaveBeenCalledTimes(1);

		action.destroy();
	});

	it('tracks the latest controller/tier after update and stops after destroy', () => {
		const node = document.createElement('div');
		const first = makeController();
		const action = wordHover(node, { controller: first, tier: tierView(1) });

		const second = makeController();
		const next = tierView(2);
		action.update({ controller: second, tier: next });

		node.dispatchEvent(new MouseEvent('mouseenter'));
		expect(first.show).not.toHaveBeenCalled();
		expect(second.show).toHaveBeenCalledWith(next, expect.anything());

		action.destroy();
		node.dispatchEvent(new MouseEvent('mouseenter'));
		expect(second.show).toHaveBeenCalledTimes(1);
	});
});
