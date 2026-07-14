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

	it('recomputes the callout position for a step sharing the same anchor as the prior step', async () => {
		const anchorEl = document.createElement('button');
		document.body.appendChild(anchorEl);
		const action = tutorialAnchor(anchorEl, 'skill-bar');
		vi.spyOn(anchorEl, 'getBoundingClientRect').mockReturnValue({
			top: 480,
			left: 100,
			width: 40,
			height: 20,
			bottom: 500,
			right: 140,
			x: 100,
			y: 480,
			toJSON: () => ({})
		} as DOMRect);

		const steps: TourStep[] = [
			{ text: 'Short', anchorKey: 'skill-bar' },
			{ text: 'A much longer line of tour text that renders a visibly taller callout box', anchorKey: 'skill-bar' }
		];
		const { container, getByRole } = setup(steps);
		await tick();

		const shell = container.querySelector('.tour-callout') as HTMLElement;
		// jsdom never lays out real box sizes; stand in a height that tracks the current step's own text
		// (not the surrounding chrome, which is present on every step) so a real difference is observable
		// (short fits below the anchor, tall overflows the viewport and flips above it), then force one
		// recompute so the effect picks up the getter.
		Object.defineProperty(shell, 'offsetHeight', {
			configurable: true,
			get: () => {
				const stepText = shell.querySelector('.tour-callout__text')?.textContent ?? '';
				return stepText.length > 20 ? 300 : 60;
			}
		});
		Object.defineProperty(shell, 'offsetWidth', { configurable: true, value: 300 });
		await fireEvent(window, new Event('resize'));
		await tick();
		const firstTop = shell.style.top;

		await fireEvent.click(getByRole('button', { name: 'Next' }));
		await tick();
		const secondTop = shell.style.top;

		expect(secondTop).not.toBe(firstTop);

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

	it('focuses the primary action (Next) on open, not Skip', async () => {
		const { getByRole } = setup(threeSteps);
		await tick();
		expect(document.activeElement).toBe(getByRole('button', { name: 'Next' }));
	});

	it('does not steal focus back to Skip when advancing steps', async () => {
		const { getByRole } = setup(threeSteps);
		await tick();

		const next = getByRole('button', { name: 'Next' }) as HTMLButtonElement;
		next.focus();
		await fireEvent.click(next);
		await tick();

		expect(document.activeElement).toBe(next);
	});

	it('moves focus onto Done when the last step swaps Next for Done', async () => {
		const { getByRole } = setup(threeSteps);

		await fireEvent.click(getByRole('button', { name: 'Next' }));
		await fireEvent.click(getByRole('button', { name: 'Next' }));
		await tick();

		expect(document.activeElement).toBe(getByRole('button', { name: 'Done' }));
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
