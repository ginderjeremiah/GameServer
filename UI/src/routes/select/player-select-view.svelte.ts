/* Character-select screen view-model (#1070). Sits between the login page and the loading screen:
   it lists the account's characters (handed off from `Login`), lets the player create a new one
   (bounded server-side by the per-account cap), and on selection binds the session — `SelectPlayer`
   rotates the token to carry the chosen player — then runs the per-player active-session takeover
   check before entering the world.

   The API/navigation touch points are injected so the orchestration is unit-testable without real
   I/O; the `+page.svelte` wires the real `SelectPlayer`/`CreatePlayer` calls, token storage, the
   takeover confirm, and world entry. Mirrors the reactive-view-model split the other screens use. */

import type { ICreatableClass, IPlayerData, IPlayerSummary } from '$lib/api';
import { validatePlayerName } from './player-name';

export type SelectResult = { ok: true; player: IPlayerData } | { ok: false; error: string };

export type CreateResult = { ok: true; summary: IPlayerSummary } | { ok: false; error: string };

export interface PlayerSelectDeps {
	/** Binds the session to the chosen character (rotating the token) and loads it; resolves the
	 *  player on success or a surfaced error message on failure. */
	selectPlayer: (playerId: number) => Promise<SelectResult>;
	/** Creates a new character on the account, of the chosen class when the picker is active (the host
	 *  coerces a null class to its placeholder where the catalogue isn't available — see the select
	 *  screen). Resolves its summary or a surfaced error message. */
	createPlayer: (name: string, classId: number | null) => Promise<CreateResult>;
	/** Confirms the active-session takeover after selection (a per-player presence check). Returns
	 *  true to proceed into the game, false when the player declined. */
	confirmTakeover: () => Promise<boolean>;
	/** Initializes the player manager from the loaded character and navigates into the game. */
	enterWorld: (player: IPlayerData) => void;
	/** Loads the create-character class options (the bespoke `Login/CharacterCreationData` payload,
	 *  reachable pre-player-selection over HTTP). Resolves [] on failure so the picker stays hidden. */
	loadCreationData: () => Promise<ICreatableClass[]>;
}

export class PlayerSelectView {
	/** The account's characters — seeded from the login handoff, grown by character creation. */
	players = $state<IPlayerSummary[]>([]);
	/** A surfaced selection error (a failed `SelectPlayer`), cleared on the next attempt. */
	error = $state<string | null>(null);
	/** The id of the character currently being entered, or null when idle — drives the per-card
	 *  busy state and keeps the controls disabled through the navigation that follows. */
	pendingId = $state<number | null>(null);

	/** Whether the inline create-character form is open. */
	showCreate = $state(false);
	/** The in-progress new-character name. */
	newName = $state('');
	/** The creatable class options, loaded from the bespoke character-creation payload. Empty until they
	 *  load (and on failure), which keeps the picker hidden and creation on its placeholder fallback. */
	classes = $state<ICreatableClass[]>([]);
	/** The class chosen for the new character, or null before the options load / a choice is made.
	 *  Defaulted to the first option once they load. */
	selectedClassId = $state<number | null>(null);
	/** Whether a `CreatePlayer` request is in flight. */
	creating = $state(false);
	/** A surfaced create error (name rejected or cap reached), cleared on the next attempt. */
	createError = $state<string | null>(null);

	constructor(
		private readonly deps: PlayerSelectDeps,
		initial: IPlayerSummary[],
		options: { openCreateWhenEmpty?: boolean } = {}
	) {
		this.players = initial;
		// A brand-new account lands here with no characters to create its first one (the signup flow no
		// longer creates it — #1256), so open the create form immediately rather than making the player
		// hunt for the "+ New character" affordance. Opt-in, so the in-game switcher (whose list can also
		// be empty for a single-character account) isn't forced into create mode when it opens.
		if (options.openCreateWhenEmpty && initial.length === 0) {
			this.showCreate = true;
		}
		void this.loadClasses();
	}

	/** Loads the creatable class options and defaults the selection to the first one, so the create form
	 *  is submittable without an extra click. A failure leaves the list empty (picker hidden). */
	private async loadClasses(): Promise<void> {
		const classes = await this.deps.loadCreationData();
		this.classes = classes;
		if (this.selectedClassId == null && classes.length > 0) {
			this.selectedClassId = classes[0].id;
		}
	}

	/** Live validation of the new-character name, mirroring the backend rule. */
	readonly nameValidation = $derived(validatePlayerName(this.newName));

	/** True while any selection or creation is in flight — every control disables together. */
	readonly busy = $derived(this.pendingId !== null || this.creating);

	/**
	 * Enters the game as the chosen character: binds + loads it, then runs the per-player takeover
	 * check before handing off to the world. A failed bind surfaces an error and re-enables the UI.
	 * On a declined takeover this view-model just aborts and clears the busy state; what the user sees
	 * then is up to the injected `confirmTakeover` — the production `confirmSessionTakeover` logs the
	 * freshly-issued session out (a full-page redirect to login), so a decline returns to login rather
	 * than resting on the select screen. On success the busy state is held so the controls stay
	 * disabled through the navigation `enterWorld` performs.
	 */
	async select(playerId: number): Promise<void> {
		if (this.busy) {
			return;
		}
		this.error = null;
		this.pendingId = playerId;

		const result = await this.deps.selectPlayer(playerId);
		if (!result.ok) {
			this.error = result.error;
			this.pendingId = null;
			return;
		}

		if (!(await this.deps.confirmTakeover())) {
			this.pendingId = null;
			return;
		}

		this.deps.enterWorld(result.player);
	}

	/** Opens/closes the create-character form, resetting its transient state. */
	toggleCreate(): void {
		this.showCreate = !this.showCreate;
		this.createError = null;
		if (!this.showCreate) {
			this.newName = '';
		}
	}

	/** Records the class chosen in the picker, clearing any prior create error. */
	selectClass(classId: number): void {
		this.selectedClassId = classId;
		this.createError = null;
	}

	/**
	 * Creates a new character and appends it to the list. Validates the name client-side first (the
	 * backend re-validates and enforces the per-account cap as anti-cheat), then surfaces any backend
	 * failure — name rejected or cap reached — as an inline error.
	 */
	async create(): Promise<void> {
		if (this.busy) {
			return;
		}
		const validation = this.nameValidation;
		if (!validation.ok) {
			this.createError = validation.msg;
			return;
		}

		this.creating = true;
		this.createError = null;
		const result = await this.deps.createPlayer(validation.name, this.selectedClassId);
		this.creating = false;

		if (!result.ok) {
			this.createError = result.error;
			return;
		}

		this.players = [...this.players, result.summary];
		this.newName = '';
		this.showCreate = false;
	}
}
