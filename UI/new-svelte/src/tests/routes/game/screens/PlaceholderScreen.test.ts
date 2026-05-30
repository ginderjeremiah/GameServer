import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import PlaceholderScreen from '../../../../routes/game/screens/PlaceholderScreen.svelte';

afterEach(cleanup);

describe('PlaceholderScreen', () => {
	it('renders the placeholder container', () => {
		render(PlaceholderScreen, { props: { label: 'Attributes' } });
		expect(screen.getByTestId('placeholder-screen')).toBeTruthy();
	});

	it('displays the given label', () => {
		render(PlaceholderScreen, { props: { label: 'Attributes' } });
		const el = screen.getByTestId('placeholder-screen');
		expect(el.textContent).toContain('Attributes');
	});

	it('displays "Not yet implemented" text', () => {
		render(PlaceholderScreen, { props: { label: 'Stats' } });
		const el = screen.getByTestId('placeholder-screen');
		expect(el.textContent).toContain('Not yet implemented');
	});
});
