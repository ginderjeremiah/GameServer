import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import BossAffordanceSlot from '$routes/game/screens/fight/boss/BossAffordanceSlot.svelte';
import type { BossView } from '$routes/game/screens/fight/boss/boss-view.svelte';

const makeView = (over: Partial<BossView> = {}) =>
	({
		engaged: false,
		bossName: 'Catacomb Lich',
		bossLevel: 18,
		cleared: false,
		autoFight: false,
		challenge: vi.fn(),
		retreat: vi.fn(),
		toggleAutoFight: vi.fn(),
		...over
	}) as unknown as BossView;

afterEach(cleanup);

describe('BossAffordanceSlot', () => {
	it('renders the Challenge trigger and challenges on click when available', async () => {
		const view = makeView();
		render(BossAffordanceSlot, { props: { view } });

		const btn = screen.getByTestId('challenge-boss');
		expect(btn.textContent).toContain('Challenge');
		await fireEvent.click(btn);

		expect(view.challenge).toHaveBeenCalledOnce();
	});

	it('toggles auto-fight from the trigger', async () => {
		const view = makeView();
		render(BossAffordanceSlot, { props: { view } });

		await fireEvent.click(screen.getByTestId('auto-fight-toggle'));

		expect(view.toggleAutoFight).toHaveBeenCalledWith(true);
	});

	it('renders the boss bar and retreats on click while engaged', async () => {
		const view = makeView({ engaged: true } as Partial<BossView>);
		render(BossAffordanceSlot, { props: { view } });

		expect(screen.getByTestId('boss-bar')).toBeTruthy();
		await fireEvent.click(screen.getByTestId('retreat-boss'));

		expect(view.retreat).toHaveBeenCalledOnce();
	});

	it('shows Re-challenge and the Cleared seal for a cleared zone', () => {
		const view = makeView({ cleared: true } as Partial<BossView>);
		render(BossAffordanceSlot, { props: { view } });

		expect(screen.getByTestId('challenge-boss').textContent).toContain('Re-challenge');
		expect(screen.getByTestId('cleared-seal')).toBeTruthy();
	});
});
