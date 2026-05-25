// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, fireEvent, cleanup } from '@testing-library/svelte';
import Button from './Button.svelte';

afterEach(cleanup);

describe('Button', () => {
	it('renders with text', () => {
		const { getByText } = render(Button, { props: { text: 'Click Me', onClick: vi.fn() } });
		expect(getByText('Click Me')).toBeTruthy();
	});

	it('fires onClick when clicked', async () => {
		const onClick = vi.fn();
		const { getByRole } = render(Button, { props: { text: 'Submit', onClick } });

		await fireEvent.click(getByRole('button'));
		expect(onClick).toHaveBeenCalledTimes(1);
	});

	it('is disabled when loading', () => {
		const { getByRole } = render(Button, { props: { text: 'Save', onClick: vi.fn(), loading: true } });
		const button = getByRole('button');
		expect(button.hasAttribute('disabled')).toBe(true);
	});

	it('is enabled when not loading', () => {
		const { getByRole } = render(Button, { props: { text: 'Save', onClick: vi.fn(), loading: false } });
		const button = getByRole('button');
		expect(button.hasAttribute('disabled')).toBe(false);
	});

	it('defaults to type="button"', () => {
		const { getByRole } = render(Button, { props: { text: 'Btn', onClick: vi.fn() } });
		const button = getByRole('button');
		expect(button.getAttribute('type')).toBe('button');
	});

	it('accepts type="submit"', () => {
		const { getByRole } = render(Button, { props: { text: 'Go', onClick: vi.fn(), type: 'submit' } });
		const button = getByRole('button');
		expect(button.getAttribute('type')).toBe('submit');
	});
});
