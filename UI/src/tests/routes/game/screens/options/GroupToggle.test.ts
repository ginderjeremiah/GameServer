import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';

import GroupToggle from '$routes/game/screens/options/GroupToggle.svelte';

afterEach(cleanup);

describe('GroupToggle', () => {
	it('renders without "on" or "mixed" class when both are false', () => {
		const { container } = render(GroupToggle, { props: { on: false, mixed: false, onClick: vi.fn() } });
		const btn = container.querySelector('button') as HTMLButtonElement;
		expect(btn.classList.contains('on')).toBe(false);
		expect(btn.classList.contains('mixed')).toBe(false);
	});

	it('has the "on" class when on=true', () => {
		const { container } = render(GroupToggle, { props: { on: true, mixed: false, onClick: vi.fn() } });
		expect(container.querySelector('button')!.classList.contains('on')).toBe(true);
	});

	it('has the "mixed" class when mixed=true', () => {
		const { container } = render(GroupToggle, { props: { on: false, mixed: true, onClick: vi.fn() } });
		expect(container.querySelector('button')!.classList.contains('mixed')).toBe(true);
	});

	it('calls onClick when clicked', async () => {
		const onClick = vi.fn();
		const { container } = render(GroupToggle, { props: { on: false, mixed: false, onClick } });
		await fireEvent.click(container.querySelector('button')!);
		expect(onClick).toHaveBeenCalledTimes(1);
	});

	it('sets data-testid when testId is provided', () => {
		const { container } = render(GroupToggle, { props: { on: false, mixed: false, onClick: vi.fn(), testId: 'gt-combat' } });
		expect(container.querySelector('[data-testid="gt-combat"]')).toBeTruthy();
	});

	it('uses the "Disable all in group" title when on', () => {
		const { container } = render(GroupToggle, { props: { on: true, mixed: false, onClick: vi.fn() } });
		expect(container.querySelector('button')!.getAttribute('title')).toBe('Disable all in group');
	});

	it('uses the "Enable all in group" title when off', () => {
		const { container } = render(GroupToggle, { props: { on: false, mixed: false, onClick: vi.fn() } });
		expect(container.querySelector('button')!.getAttribute('title')).toBe('Enable all in group');
	});
});
