// @vitest-environment jsdom
import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { tick } from 'svelte';
import TourPlayer from '$components/tour/TourPlayer.svelte';
import { tutorialAnchor } from '$components/tour/tutorial-anchor';
import type { TourStep } from '$components/tour/tour-types';

afterEach(() => {
	cleanup();
	document.body.classList.remove('tour-open');
});

const threeSteps: TourStep[] = [{ text: 'Step one' }, { text: 'Step two' }, { text: 'Step three' }];

const setup = (steps: TourStep[], overrides: Partial<{ open: boolean }> = {}) => {
	const onDismiss = vi.fn();
	const onComplete = vi.fn();
	const result = render(TourPlayer, {
		props: { open: overrides.open ?? true, steps, label: 'Test tour', onDismiss, onComplete }
	});
	return { ...result, onDismiss, onComplete };
};

describe('TourPlayer', () => {
	it('renders nothing while closed', () => {
		const { container } = setup(threeSteps, { open: false });
		expect(container.querySelector('.tour-callout')).toBeNull();
	});

	it('shows the first step with a 1-based step counter', () => {
		const { getByText } = setup(threeSteps);
		expect(getByText('1 of 3')).toBeTruthy();
		expect(getByText('Step one')).toBeTruthy();
	});

	it('degrades to a centered callout when the step has no anchorKey', async () => {
		const { container } = setup(threeSteps);
		await tick();
		const callout = container.querySelector('.tour-callout') as HTMLElement;
		expect(callout.getAttribute('data-tour-position')).toBe('centered');
	});

	it('degrades to a centered callout when the anchorKey is not registered', async () => {
		const { container } = setup([{ text: 'Dangling anchor', anchorKey: 'never-registered' }]);
		await tick();
		const callout = container.querySelector('.tour-callout') as HTMLElement;
		expect(callout.getAttribute('data-tour-position')).toBe('centered');
	});

	it('positions as anchored when the step names a registered anchor', async () => {
		const anchorEl = document.createElement('button');
		document.body.appendChild(anchorEl);
		const action = tutorialAnchor(anchorEl, 'skill-bar');

		const { container } = setup([{ text: 'Look here', anchorKey: 'skill-bar' }]);
		await tick();
		const callout = container.querySelector('.tour-callout') as HTMLElement;
		expect(callout.getAttribute('data-tour-position')).toBe('anchored');
		expect(container.querySelector('.tour-spotlight')).not.toBeNull();

		action.destroy();
		anchorEl.remove();
	});

	it('navigates forward and back, disabling Back on the first step', async () => {
		const { getByText, getByRole } = setup(threeSteps);

		const back = getByRole('button', { name: 'Back' }) as HTMLButtonElement;
		expect(back.disabled).toBe(true);

		await fireEvent.click(getByRole('button', { name: 'Next' }));
		expect(getByText('2 of 3')).toBeTruthy();
		expect(back.disabled).toBe(false);

		await fireEvent.click(back);
		expect(getByText('1 of 3')).toBeTruthy();
	});

	it('shows Done instead of Next on the last step and calls onComplete', async () => {
		const { getByText, getByRole, onComplete } = setup(threeSteps);

		await fireEvent.click(getByRole('button', { name: 'Next' }));
		await fireEvent.click(getByRole('button', { name: 'Next' }));
		expect(getByText('3 of 3')).toBeTruthy();
		expect(() => getByRole('button', { name: 'Next' })).toThrow();

		await fireEvent.click(getByRole('button', { name: 'Done' }));
		expect(onComplete).toHaveBeenCalledTimes(1);
	});

	it('calls onDismiss on Escape, backdrop click, and Skip', async () => {
		const { container, getByRole, onDismiss } = setup(threeSteps);

		await fireEvent.keyDown(window, { key: 'Escape' });
		expect(onDismiss).toHaveBeenCalledTimes(1);

		await fireEvent.click(container.querySelector('.tour-backdrop')!);
		expect(onDismiss).toHaveBeenCalledTimes(2);

		await fireEvent.click(getByRole('button', { name: 'Skip' }));
		expect(onDismiss).toHaveBeenCalledTimes(3);
	});

	it('restores focus to the prior element once dismissed', async () => {
		const outside = document.createElement('button');
		document.body.appendChild(outside);
		outside.focus();
		expect(document.activeElement).toBe(outside);

		const { rerender } = setup(threeSteps, { open: false });
		await rerender({ open: true, steps: threeSteps, label: 'Test tour', onDismiss: vi.fn(), onComplete: vi.fn() });
		await tick();
		expect(document.activeElement).not.toBe(outside);

		await rerender({ open: false, steps: threeSteps, label: 'Test tour', onDismiss: vi.fn(), onComplete: vi.fn() });
		await tick();
		expect(document.activeElement).toBe(outside);
		outside.remove();
	});

	it('resets to the first step each time it reopens', async () => {
		const { getByText, getByRole, rerender } = setup(threeSteps);

		await fireEvent.click(getByRole('button', { name: 'Next' }));
		expect(getByText('2 of 3')).toBeTruthy();

		await rerender({ open: false, steps: threeSteps, label: 'Test tour', onDismiss: vi.fn(), onComplete: vi.fn() });
		await rerender({ open: true, steps: threeSteps, label: 'Test tour', onDismiss: vi.fn(), onComplete: vi.fn() });
		await tick();
		expect(getByText('1 of 3')).toBeTruthy();
	});
});
