import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';

// SortControl only needs the SORT_OPTIONS const from the view module; stub the
// store so importing the view-model doesn't pull the real game stores.
vi.mock('$stores', () => ({ staticData: {} }));

import SortControl from '$routes/game/screens/challenges/SortControl.svelte';

afterEach(cleanup);

describe('SortControl', () => {
	it('renders a button for every sort option', () => {
		render(SortControl, { props: { value: 'progress', onChange: vi.fn() } });
		expect(screen.getByText('Progress')).toBeTruthy();
		expect(screen.getByText('Rarity')).toBeTruthy();
		expect(screen.getByText('Name')).toBeTruthy();
	});

	it('marks the active option', () => {
		const { container } = render(SortControl, { props: { value: 'rarity', onChange: vi.fn() } });
		const active = container.querySelector('.sort-option.active') as HTMLElement;
		expect(active.textContent?.trim()).toBe('Rarity');
	});

	it('divides every option after the first', () => {
		const { container } = render(SortControl, { props: { value: 'progress', onChange: vi.fn() } });
		// Three options → two dividers (all but the first).
		expect(container.querySelectorAll('.sort-option.divided')).toHaveLength(2);
	});

	it('calls onChange with the clicked option key', async () => {
		const onChange = vi.fn();
		render(SortControl, { props: { value: 'progress', onChange } });
		await fireEvent.click(screen.getByText('Name'));
		expect(onChange).toHaveBeenCalledWith('name');
	});
});
