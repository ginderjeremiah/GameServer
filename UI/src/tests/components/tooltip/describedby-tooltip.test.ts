// @vitest-environment jsdom
import { describe, it, expect } from 'vitest';
import { describedByTooltip } from '$components/tooltip/describedby-tooltip';

describe('describedByTooltip action', () => {
	it('sets aria-describedby to the provided tooltip id', () => {
		const node = document.createElement('button');
		describedByTooltip(node, 'tooltip-3');
		expect(node.getAttribute('aria-describedby')).toBe('tooltip-3');
	});

	it('omits the attribute when no id is provided (e.g. a trigger that surfaces no tooltip)', () => {
		const node = document.createElement('button');
		describedByTooltip(node, undefined);
		expect(node.hasAttribute('aria-describedby')).toBe(false);
	});

	it('adds and removes the attribute as the id appears and clears on update', () => {
		const node = document.createElement('button');
		const action = describedByTooltip(node, undefined);
		expect(node.hasAttribute('aria-describedby')).toBe(false);

		action.update('tooltip-7');
		expect(node.getAttribute('aria-describedby')).toBe('tooltip-7');

		action.update(undefined);
		expect(node.hasAttribute('aria-describedby')).toBe(false);
	});
});
