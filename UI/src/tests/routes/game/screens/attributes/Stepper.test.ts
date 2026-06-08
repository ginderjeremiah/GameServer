import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';

import Stepper from '$routes/game/screens/attributes/Stepper.svelte';

afterEach(cleanup);

describe('Stepper', () => {
	it('renders a decrement and an increment button', () => {
		render(Stepper, { props: { canDec: true, canInc: true, onDec: vi.fn(), onInc: vi.fn() } });
		expect(screen.getByLabelText('Remove a point')).toBeTruthy();
		expect(screen.getByLabelText('Add a point')).toBeTruthy();
	});

	it('disables the decrement button when canDec is false', () => {
		render(Stepper, { props: { canDec: false, canInc: true, onDec: vi.fn(), onInc: vi.fn() } });
		expect((screen.getByLabelText('Remove a point') as HTMLButtonElement).disabled).toBe(true);
	});

	it('disables the increment button when canInc is false', () => {
		render(Stepper, { props: { canDec: true, canInc: false, onDec: vi.fn(), onInc: vi.fn() } });
		expect((screen.getByLabelText('Add a point') as HTMLButtonElement).disabled).toBe(true);
	});

	it('enables both buttons when both can* flags are true', () => {
		render(Stepper, { props: { canDec: true, canInc: true, onDec: vi.fn(), onInc: vi.fn() } });
		expect((screen.getByLabelText('Remove a point') as HTMLButtonElement).disabled).toBe(false);
		expect((screen.getByLabelText('Add a point') as HTMLButtonElement).disabled).toBe(false);
	});

	it('calls onInc when the + button is clicked', async () => {
		const onInc = vi.fn();
		render(Stepper, { props: { canDec: true, canInc: true, onDec: vi.fn(), onInc } });
		await fireEvent.click(screen.getByLabelText('Add a point'));
		expect(onInc).toHaveBeenCalledTimes(1);
	});

	it('calls onDec when the - button is clicked', async () => {
		const onDec = vi.fn();
		render(Stepper, { props: { canDec: true, canInc: true, onDec, onInc: vi.fn() } });
		await fireEvent.click(screen.getByLabelText('Remove a point'));
		expect(onDec).toHaveBeenCalledTimes(1);
	});

	it('does not call onInc when the + button is disabled', async () => {
		const onInc = vi.fn();
		render(Stepper, { props: { canDec: true, canInc: false, onDec: vi.fn(), onInc } });
		await fireEvent.click(screen.getByLabelText('Add a point'));
		expect(onInc).not.toHaveBeenCalled();
	});

	it('does not call onDec when the - button is disabled', async () => {
		const onDec = vi.fn();
		render(Stepper, { props: { canDec: false, canInc: true, onDec, onInc: vi.fn() } });
		await fireEvent.click(screen.getByLabelText('Remove a point'));
		expect(onDec).not.toHaveBeenCalled();
	});
});
