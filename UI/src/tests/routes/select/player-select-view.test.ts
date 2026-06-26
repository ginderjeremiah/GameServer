import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PlayerSelectView, type PlayerSelectDeps } from '$routes/select/player-select-view.svelte';
import type { ICreatableClass, IPlayerData, IPlayerSummary } from '$lib/api';

const summary = (id: number, name = `Hero${id}`): IPlayerSummary => ({
	id,
	name,
	level: 1,
	currentZoneId: 0,
	lastActivity: '2026-06-20T00:00:00Z'
});

const player = (id: number): IPlayerData => ({ id, name: `Hero${id}` }) as IPlayerData;

const creatable = (id: number): ICreatableClass =>
	({
		id,
		name: `Class${id}`,
		starterSkills: [],
		starterEquipment: [],
		attributeDistributions: []
	}) as unknown as ICreatableClass;

const makeDeps = (overrides: Partial<PlayerSelectDeps> = {}): PlayerSelectDeps => ({
	selectPlayer: vi.fn().mockResolvedValue({ ok: true, player: player(1) }),
	createPlayer: vi.fn().mockResolvedValue({ ok: true, summary: summary(2) }),
	confirmTakeover: vi.fn().mockResolvedValue(true),
	enterWorld: vi.fn(),
	loadCreationData: vi.fn().mockResolvedValue([]),
	...overrides
});

/** Flush microtasks so the constructor's fire-and-forget class load settles. */
const flush = () => new Promise((resolve) => setTimeout(resolve));

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

describe('PlayerSelectView — open-create-when-empty', () => {
	it('opens the create form when the list is empty and the option is set (first-character signup)', () => {
		const view = new PlayerSelectView(makeDeps(), [], { openCreateWhenEmpty: true });

		expect(view.showCreate).toBe(true);
	});

	it('leaves the create form closed when characters already exist', () => {
		const view = new PlayerSelectView(makeDeps(), [summary(1)], { openCreateWhenEmpty: true });

		expect(view.showCreate).toBe(false);
	});

	it('leaves the create form closed for an empty list when the option is off (the switcher)', () => {
		const view = new PlayerSelectView(makeDeps(), []);

		expect(view.showCreate).toBe(false);
	});
});

describe('PlayerSelectView — class loading', () => {
	it('loads the creatable classes and defaults the selection to the first', async () => {
		const deps = makeDeps({ loadCreationData: vi.fn().mockResolvedValue([creatable(3), creatable(5)]) });
		const view = new PlayerSelectView(deps, [summary(1)]);

		await flush();

		expect(view.classes.map((c) => c.id)).toEqual([3, 5]);
		expect(view.selectedClassId).toBe(3);
	});

	it('leaves the selection null when no classes load', async () => {
		const view = new PlayerSelectView(makeDeps(), [summary(1)]);

		await flush();

		expect(view.classes).toEqual([]);
		expect(view.selectedClassId).toBeNull();
	});

	it('clears the loading flag once the options resolve', async () => {
		const view = new PlayerSelectView(makeDeps(), [summary(1)]);
		expect(view.classesLoading).toBe(true);

		await flush();

		expect(view.classesLoading).toBe(false);
	});

	it('retries the class load after a failed (empty) load', async () => {
		const loadCreationData = vi
			.fn()
			.mockResolvedValueOnce([])
			.mockResolvedValueOnce([creatable(4)]);
		const view = new PlayerSelectView(makeDeps({ loadCreationData }), [summary(1)]);
		await flush();
		expect(view.classes).toEqual([]);

		view.retryLoadClasses();
		await flush();

		expect(view.classes.map((c) => c.id)).toEqual([4]);
		expect(view.selectedClassId).toBe(4);
		expect(loadCreationData).toHaveBeenCalledTimes(2);
	});
});

describe('PlayerSelectView — create', () => {
	it('sends the name and chosen class, appends the character, and closes the form on success', async () => {
		const deps = makeDeps();
		const view = new PlayerSelectView(deps, [summary(1)]);
		view.showCreate = true;
		view.newName = 'NewHero';
		view.selectClass(3);

		await view.create();

		expect(deps.createPlayer).toHaveBeenCalledWith('NewHero', 3);
		expect(view.players.map((p) => p.id)).toEqual([1, 2]);
		expect(view.showCreate).toBe(false);
		expect(view.newName).toBe('');
	});

	it('rejects an invalid name client-side without calling the backend', async () => {
		const deps = makeDeps();
		const view = new PlayerSelectView(deps, [summary(1)]);
		view.newName = '   ';
		view.selectClass(0);

		await view.create();

		expect(deps.createPlayer).not.toHaveBeenCalled();
		expect(view.createError).toBeTruthy();
	});

	it('blocks creation when no class is selected and surfaces an error', async () => {
		const deps = makeDeps();
		const view = new PlayerSelectView(deps, [summary(1)]);
		view.newName = 'NewHero';
		// selectedClassId stays null — e.g. the class catalogue failed to load, so the picker is hidden.
		// A class is mandatory now (no placeholder fallback), so creation is blocked client-side.

		await view.create();

		expect(deps.createPlayer).not.toHaveBeenCalled();
		expect(view.createError).toBeTruthy();
	});

	it('selectClass records the choice and clears a prior create error', () => {
		const view = new PlayerSelectView(makeDeps(), [summary(1)]);
		view.createError = 'stale';

		view.selectClass(2);

		expect(view.selectedClassId).toBe(2);
		expect(view.createError).toBeNull();
	});

	it('surfaces a backend create error (e.g. cap reached) and keeps the list unchanged', async () => {
		const deps = makeDeps({
			createPlayer: vi.fn().mockResolvedValue({ ok: false, error: 'cap reached' })
		});
		const view = new PlayerSelectView(deps, [summary(1)]);
		view.showCreate = true;
		view.newName = 'NewHero';
		view.selectClass(0);

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
		view.selectClass(1);

		await view.create();

		expect(deps.createPlayer).toHaveBeenCalledWith('Spaced', 1);
	});
});
