import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';

import NumInput from '$routes/admin/workbench/components/NumInput.svelte';

afterEach(cleanup);

describe('NumInput — rendering', () => {
	it('renders an input with type="text" and inputmode="decimal"', () => {
		const { container } = render(NumInput, { props: { value: 10, onChange: vi.fn() } });
		const input = container.querySelector('input') as HTMLInputElement;
		expect(input.type).toBe('text');
		expect(input.getAttribute('inputmode')).toBe('decimal');
	});

	it('displays the initial value as a string', () => {
		const { container } = render(NumInput, { props: { value: 42, onChange: vi.fn() } });
		expect((container.querySelector('input') as HTMLInputElement).value).toBe('42');
	});

	it('displays "0" for value 0', () => {
		const { container } = render(NumInput, { props: { value: 0, onChange: vi.fn() } });
		expect((container.querySelector('input') as HTMLInputElement).value).toBe('0');
	});
});

describe('NumInput — valid input', () => {
	it('calls onChange with the parsed integer on a valid integer input', async () => {
		const onChange = vi.fn();
		const { container } = render(NumInput, { props: { value: 0, onChange } });
		const input = container.querySelector('input') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: '15' } });
		expect(onChange).toHaveBeenCalledWith(15);
	});

	it('calls onChange with the parsed decimal on a valid decimal input', async () => {
		const onChange = vi.fn();
		const { container } = render(NumInput, { props: { value: 0, onChange } });
		const input = container.querySelector('input') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: '3.14' } });
		expect(onChange).toHaveBeenCalledWith(3.14);
	});

	it('does not call onChange when the input is cleared (no coercion to 0)', async () => {
		const onChange = vi.fn();
		const { container } = render(NumInput, { props: { value: 5, onChange } });
		const input = container.querySelector('input') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: '' } });
		expect(onChange).not.toHaveBeenCalled();
	});
});

describe('NumInput — input rejection', () => {
	it('does not call onChange when input contains alphabetic characters', async () => {
		const onChange = vi.fn();
		const { container } = render(NumInput, { props: { value: 5, onChange } });
		const input = container.querySelector('input') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: '5abc' } });
		expect(onChange).not.toHaveBeenCalled();
	});

	it('does not call onChange for a lone minus sign when allowNegative is false', async () => {
		const onChange = vi.fn();
		const { container } = render(NumInput, { props: { value: 0, onChange, allowNegative: false } });
		const input = container.querySelector('input') as HTMLInputElement;
		// A lone "-" matches the no-negative pattern only when allowNegative is true.
		// With allowNegative: false the pattern is /^\d*\.?\d*$/ so "-" fails.
		await fireEvent.input(input, { target: { value: '-' } });
		expect(onChange).not.toHaveBeenCalled();
	});

	it('accepts negative values when allowNegative is true', async () => {
		const onChange = vi.fn();
		const { container } = render(NumInput, { props: { value: 0, onChange, allowNegative: true } });
		const input = container.querySelector('input') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: '-5' } });
		expect(onChange).toHaveBeenCalledWith(-5);
	});

	it('rejects a negative value when allowNegative is false', async () => {
		const onChange = vi.fn();
		const { container } = render(NumInput, { props: { value: 0, onChange, allowNegative: false } });
		const input = container.querySelector('input') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: '-5' } });
		expect(onChange).not.toHaveBeenCalled();
	});
});

describe('NumInput — in-progress text', () => {
	it('does not call onChange for a lone "." (in-progress decimal)', async () => {
		const onChange = vi.fn();
		const { container } = render(NumInput, { props: { value: 0, onChange } });
		const input = container.querySelector('input') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: '.' } });
		expect(onChange).not.toHaveBeenCalled();
	});

	it('preserves the in-progress text without committing a value', async () => {
		const onChange = vi.fn();
		const { container } = render(NumInput, { props: { value: 5, onChange } });
		const input = container.querySelector('input') as HTMLInputElement;
		// While focused the cleared text is kept (not re-coerced), and the stored value is untouched.
		await fireEvent.focus(input);
		await fireEvent.input(input, { target: { value: '' } });
		expect(input.value).toBe('');
		expect(onChange).not.toHaveBeenCalled();
	});
});
