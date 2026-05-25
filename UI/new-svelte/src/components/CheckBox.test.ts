// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest';
import { render, fireEvent, cleanup } from '@testing-library/svelte';
import CheckBox from './CheckBox.svelte';

afterEach(cleanup);

describe('CheckBox', () => {
	it('renders a checkbox input', () => {
		const { getByRole } = render(CheckBox, { props: { value: false } });
		expect(getByRole('checkbox')).toBeTruthy();
	});

	it('reflects the initial checked state', () => {
		const { getByRole } = render(CheckBox, { props: { value: true } });
		const input = getByRole('checkbox') as HTMLInputElement;
		expect(input.checked).toBe(true);
	});

	it('toggles when clicked', async () => {
		const { getByRole } = render(CheckBox, { props: { value: false } });
		const input = getByRole('checkbox') as HTMLInputElement;

		await fireEvent.click(input);
		expect(input.checked).toBe(true);
	});

	it('respects disabled state', () => {
		const { getByRole } = render(CheckBox, { props: { value: false, disabled: true } });
		const input = getByRole('checkbox') as HTMLInputElement;
		expect(input.disabled).toBe(true);
	});
});
