import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import type { CardGameView } from '$routes/game/screens/card-game/card-game-view.svelte';

import SandboxStrip from '$routes/game/screens/card-game/loom/SandboxStrip.svelte';

afterEach(cleanup);

// The strip only reads `view.game` for display and calls `view.setStat`, so a minimal stub stands in
// for the full card-game engine.
const makeView = (setStat = vi.fn()): CardGameView =>
	({
		game: { agi: 100, dex: 100, luck: 30, drawIntervalSec: 1.5, handCap: 5, critGap: 3 },
		setStat
	}) as unknown as CardGameView;

const sliders = (container: HTMLElement) =>
	Array.from(container.querySelectorAll<HTMLInputElement>('input[type="range"]'));

describe('SandboxStrip — accessible slider names', () => {
	it('gives each range slider an accessible name', () => {
		const { container } = render(SandboxStrip, { props: { view: makeView() } });
		expect(sliders(container).map((s) => s.getAttribute('aria-label'))).toEqual(['Agility', 'Dexterity', 'Luck']);
	});

	it('routes each slider input to setStat with its stat key', async () => {
		const setStat = vi.fn();
		const { container } = render(SandboxStrip, { props: { view: makeView(setStat) } });
		const [agi, dex, luck] = sliders(container);
		await fireEvent.input(agi, { target: { value: '120' } });
		await fireEvent.input(dex, { target: { value: '200' } });
		await fireEvent.input(luck, { target: { value: '40' } });
		expect(setStat).toHaveBeenCalledWith('agi', 120);
		expect(setStat).toHaveBeenCalledWith('dex', 200);
		expect(setStat).toHaveBeenCalledWith('luck', 40);
	});
});
