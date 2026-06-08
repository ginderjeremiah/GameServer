import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import type { AttributeMode } from '$routes/game/screens/attributes/attributes-view.svelte';

import ModeToggle from '$routes/game/screens/attributes/ModeToggle.svelte';

afterEach(cleanup);

describe('ModeToggle', () => {
	it('renders the Guided and Theorycraft options', () => {
		render(ModeToggle, { props: { mode: 'guided' as AttributeMode, onPick: vi.fn() } });
		expect(screen.getByText('Guided')).toBeTruthy();
		expect(screen.getByText('Theorycraft')).toBeTruthy();
	});

	it('marks the active mode with the "on" class', () => {
		render(ModeToggle, { props: { mode: 'theory' as AttributeMode, onPick: vi.fn() } });
		expect(screen.getByText('Guided').classList.contains('on')).toBe(false);
		expect(screen.getByText('Theorycraft').classList.contains('on')).toBe(true);
	});

	it('marks "Guided" active when mode is "guided"', () => {
		render(ModeToggle, { props: { mode: 'guided' as AttributeMode, onPick: vi.fn() } });
		expect(screen.getByText('Guided').classList.contains('on')).toBe(true);
		expect(screen.getByText('Theorycraft').classList.contains('on')).toBe(false);
	});

	it('calls onPick("theory") when Theorycraft is clicked', async () => {
		const onPick = vi.fn();
		render(ModeToggle, { props: { mode: 'guided' as AttributeMode, onPick } });
		await fireEvent.click(screen.getByText('Theorycraft'));
		expect(onPick).toHaveBeenCalledWith('theory');
	});

	it('calls onPick("guided") when Guided is clicked', async () => {
		const onPick = vi.fn();
		render(ModeToggle, { props: { mode: 'theory' as AttributeMode, onPick } });
		await fireEvent.click(screen.getByText('Guided'));
		expect(onPick).toHaveBeenCalledWith('guided');
	});
});
