import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import Select from '$components/Select.svelte';

afterEach(cleanup);

const options = [
	{ id: 1, name: 'Alpha' },
	{ id: 2, name: 'Beta' },
	{ id: 3, name: 'Gamma' }
];

describe('Select', () => {
	it('renders a label element when label is provided', () => {
		const { getByText } = render(Select, { props: { value: 1, options, label: 'My Field' } });
		expect(getByText('My Field')).toBeTruthy();
	});

	it('does not render a label element when label is omitted', () => {
		const { container } = render(Select, { props: { value: 1, options } });
		expect(container.querySelector('label')).toBeNull();
	});

	it('prepends a blank option by default', () => {
		const { container } = render(Select, { props: { value: 1, options } });
		const optionEls = container.querySelectorAll('option');
		// blank + 3 real options
		expect(optionEls.length).toBe(4);
		expect((optionEls[0] as HTMLOptionElement).value).toBe('-1');
		expect(optionEls[0].textContent).toBe('');
	});

	it('omits the blank option when disableBlanks=true', () => {
		const { container } = render(Select, { props: { value: 1, options, disableBlanks: true } });
		const optionEls = container.querySelectorAll('option');
		expect(optionEls.length).toBe(3);
	});

	it('renders all provided options with their names', () => {
		const { container } = render(Select, { props: { value: 1, options, disableBlanks: true } });
		const optionEls = container.querySelectorAll('option');
		expect(optionEls[0].textContent?.trim()).toBe('Alpha');
		expect(optionEls[1].textContent?.trim()).toBe('Beta');
		expect(optionEls[2].textContent?.trim()).toBe('Gamma');
	});
});
