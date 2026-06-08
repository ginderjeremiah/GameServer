import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';

import Toggle from '$routes/game/screens/options/Toggle.svelte';

afterEach(cleanup);

describe('Toggle', () => {
	it('sets aria-checked="true" and the "on" class when on=true', () => {
		const { container } = render(Toggle, { props: { on: true, onToggle: vi.fn() } });
		const btn = container.querySelector('button') as HTMLButtonElement;
		expect(btn.getAttribute('aria-checked')).toBe('true');
		expect(btn.classList.contains('on')).toBe(true);
	});

	it('sets aria-checked="false" and no "on" class when on=false', () => {
		const { container } = render(Toggle, { props: { on: false, onToggle: vi.fn() } });
		const btn = container.querySelector('button') as HTMLButtonElement;
		expect(btn.getAttribute('aria-checked')).toBe('false');
		expect(btn.classList.contains('on')).toBe(false);
	});

	it('calls onToggle(true) when clicked while off', async () => {
		const onToggle = vi.fn();
		const { container } = render(Toggle, { props: { on: false, onToggle } });
		await fireEvent.click(container.querySelector('button')!);
		expect(onToggle).toHaveBeenCalledWith(true);
	});

	it('calls onToggle(false) when clicked while on', async () => {
		const onToggle = vi.fn();
		const { container } = render(Toggle, { props: { on: true, onToggle } });
		await fireEvent.click(container.querySelector('button')!);
		expect(onToggle).toHaveBeenCalledWith(false);
	});

	it('sets the aria-label when label is provided', () => {
		const { container } = render(Toggle, { props: { on: false, onToggle: vi.fn(), label: 'Enable notifications' } });
		expect(container.querySelector('button')!.getAttribute('aria-label')).toBe('Enable notifications');
	});

	it('sets data-testid when testId is provided', () => {
		const { container } = render(Toggle, { props: { on: false, onToggle: vi.fn(), testId: 'my-toggle' } });
		expect(container.querySelector('[data-testid="my-toggle"]')).toBeTruthy();
	});
});
