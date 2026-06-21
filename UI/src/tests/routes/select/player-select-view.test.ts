import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PlayerSelectView, type PlayerSelectDeps } from '$routes/select/player-select-view.svelte';
import type { IPlayerData, IPlayerSummary } from '$lib/api';

const summary = (id: number, name = `Hero${id}`): IPlayerSummary => ({
	id,
	name,
	level: 1,
	currentZoneId: 0,
	lastActivity: '2026-06-20T00:00:00Z'
});

const player = (id: number): IPlayerData => ({ id, name: `Hero${id}` }) as IPlayerData;

const makeDeps = (overrides: Partial<PlayerSelectDeps> = {}): PlayerSelectDeps => ({
	selectPlayer: vi.fn().mockResolvedValue({ ok: true, player: player(1) }),
	createPlayer: vi.fn().mockResolvedValue({ ok: true, summary: summary(2) }),
	confirmTakeover: vi.fn().mockResolvedValue(true),
	enterWorld: vi.fn(),
	...overrides
});

describe('PlayerSelectView — select', () => {
	let deps: PlayerSelectDeps;
	beforeEach(() => {
		deps = makeDeps();
	});

	it('binds the character, confirms takeover, and enters the world on success', async () => {
		const view = new PlayerSelectView(deps, [summary(1)]);

		await view.select(1);

		expect(deps.selectPlayer).toHaveBeenCalledWith(1);
		expect(deps.confirmTakeover).toHaveBeenCalledTimes(1);
		expect(deps.enterWorld).toHaveBeenCalledWith(player(1));
		expect(view.error).toBeNull();
	});

	it('surfaces a select error and re-enables the UI without entering', async () => {
		deps = makeDeps({ selectPlayer: vi.fn().mockResolvedValue({ ok: false, error: 'nope' }) });
		const view = new PlayerSelectView(deps, [summary(1)]);

		await view.select(1);

		expect(view.error).toBe('nope');
		expect(view.pendingId).toBeNull();
		expect(deps.confirmTakeover).not.toHaveBeenCalled();
		expect(deps.enterWorld).not.toHaveBeenCalled();
	});

	it('aborts entry when the takeover is declined', async () => {
		deps = makeDeps({ confirmTakeover: vi.fn().mockResolvedValue(false) });
		const view = new PlayerSelectView(deps, [summary(1)]);

		await view.select(1);

		expect(deps.enterWorld).not.toHaveBeenCalled();
		expect(view.pendingId).toBeNull();
	});

	it('ignores a second select while one is already in flight', async () => {
		let resolve!: (v: { ok: true; player: IPlayerData }) => void;
		const selectPlayer = vi.fn().mockReturnValue(new Promise((r) => (resolve = r)));
		deps = makeDeps({ selectPlayer });
		const view = new PlayerSelectView(deps, [summary(1), summary(2)]);

		const first = view.select(1);
		await view.select(2); // ignored — busy

		expect(selectPlayer).toHaveBeenCalledTimes(1);
		resolve({ ok: true, player: player(1) });
		await first;
	});
});

describe('PlayerSelectView — create', () => {
	it('appends the created character and closes the form on success', async () => {
		const deps = makeDeps();
		const view = new PlayerSelectView(deps, [summary(1)]);
		view.showCreate = true;
		view.newName = 'NewHero';

		await view.create();

		expect(deps.createPlayer).toHaveBeenCalledWith('NewHero');
		expect(view.players.map((p) => p.id)).toEqual([1, 2]);
		expect(view.showCreate).toBe(false);
		expect(view.newName).toBe('');
	});

	it('rejects an invalid name client-side without calling the backend', async () => {
		const deps = makeDeps();
		const view = new PlayerSelectView(deps, [summary(1)]);
		view.newName = '   ';

		await view.create();

		expect(deps.createPlayer).not.toHaveBeenCalled();
		expect(view.createError).toBeTruthy();
	});

	it('surfaces a backend create error (e.g. cap reached) and keeps the list unchanged', async () => {
		const deps = makeDeps({
			createPlayer: vi.fn().mockResolvedValue({ ok: false, error: 'cap reached' })
		});
		const view = new PlayerSelectView(deps, [summary(1)]);
		view.showCreate = true;
		view.newName = 'NewHero';

		await view.create();

		expect(view.createError).toBe('cap reached');
		expect(view.players).toHaveLength(1);
		// The form stays open so the player can adjust and retry.
		expect(view.showCreate).toBe(true);
	});

	it('trims the submitted name', async () => {
		const deps = makeDeps();
		const view = new PlayerSelectView(deps, [summary(1)]);
		view.newName = '  Spaced  ';

		await view.create();

		expect(deps.createPlayer).toHaveBeenCalledWith('Spaced');
	});
});
