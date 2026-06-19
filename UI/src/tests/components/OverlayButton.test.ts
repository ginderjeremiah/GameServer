// @vitest-environment jsdom
import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';

import OverlayButton from '$components/OverlayButton.svelte';

afterEach(cleanup);

describe('OverlayButton', () => {
	it('renders a real <button> labelled for assistive tech', () => {
		const { container } = render(OverlayButton, { props: { label: 'Iron Sword' } });
		const btn = container.querySelector('.overlay-button');
		expect(btn?.tagName).toBe('BUTTON');
		expect(btn?.getAttribute('aria-label')).toBe('Iron Sword');
	});

	it('wires the tooltip container id onto aria-describedby when given one', () => {
		const { container } = render(OverlayButton, { props: { label: 'Iron Sword', describedById: 'tooltip-5' } });
		expect(container.querySelector('.overlay-button')!.getAttribute('aria-describedby')).toBe('tooltip-5');
	});

	it('omits aria-describedby when no tooltip id is provided', () => {
		const { container } = render(OverlayButton, { props: { label: 'Iron Sword' } });
		expect(container.querySelector('.overlay-button')!.hasAttribute('aria-describedby')).toBe(false);
	});

	it('forwards focus and blur so a card can surface its tooltip on keyboard focus', async () => {
		const onFocus = vi.fn();
		const onBlur = vi.fn();
		const { container } = render(OverlayButton, { props: { label: 'Iron Sword', onFocus, onBlur } });
		const btn = container.querySelector('.overlay-button')!;
		await fireEvent.focus(btn);
		expect(onFocus).toHaveBeenCalledOnce();
		await fireEvent.blur(btn);
		expect(onBlur).toHaveBeenCalledOnce();
	});
});
