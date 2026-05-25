// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest';
import { render, fireEvent, cleanup } from '@testing-library/svelte';
import TextInput from './TextInput.svelte';

afterEach(cleanup);

describe('TextInput', () => {
	it('renders a text input', () => {
		const { container } = render(TextInput, { props: { type: 'text', value: '' } });
		expect(container.querySelector('input[type="text"]')).toBeTruthy();
	});

	it('renders with a label', () => {
		const { getByText } = render(TextInput, { props: { type: 'text', value: '', label: 'Username' } });
		expect(getByText('Username')).toBeTruthy();
	});

	it('renders password type', () => {
		const { container } = render(TextInput, { props: { type: 'password', value: '' } });
		const input = container.querySelector('input');
		expect(input?.type).toBe('password');
	});

	it('renders number type', () => {
		const { container } = render(TextInput, { props: { type: 'number', value: 0 } });
		const input = container.querySelector('input');
		expect(input?.type).toBe('number');
	});

	it('accepts user input', async () => {
		const { container } = render(TextInput, { props: { type: 'text', value: '' } });
		const input = container.querySelector('input') as HTMLInputElement;

		await fireEvent.input(input, { target: { value: 'hello' } });
		expect(input.value).toBe('hello');
	});

	it('applies name attribute', () => {
		const { container } = render(TextInput, { props: { type: 'text', value: '', name: 'email' } });
		const input = container.querySelector('input');
		expect(input?.name).toBe('email');
	});

	it('applies disabled state', () => {
		const { container } = render(TextInput, { props: { type: 'text', value: '', disabled: true } });
		const input = container.querySelector('input') as HTMLInputElement;
		expect(input.disabled).toBe(true);
	});
});
