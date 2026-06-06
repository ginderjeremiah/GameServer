/* Options screen — the old logging popup reimagined as a full, extensible
   in-game settings screen.

   The settings shell is config-driven: a category rail (Logging is built; the
   rest are placeholders that demonstrate the shell extends cleanly) plus the
   active category's content. Adding a future category — or a new `ELogType` —
   is a one-line edit to the arrays below rather than new UI. */

import { apiSocket, ELogType, type ILogPreference } from '$lib/api';
import { playerManager } from '$lib/engine';
import { logColors } from '$components';
import { toastError } from '$stores';
import type { GlyphKind } from '$components';

/* ── log groups — sections keep a long list scannable as it grows ───────── */
export interface LogGroupDef {
	key: string;
	label: string;
}

export const LOG_GROUPS: LogGroupDef[] = [
	{ key: 'combat', label: 'Combat' },
	{ key: 'progression', label: 'Progression' },
	{ key: 'items', label: 'Items' },
	{ key: 'system', label: 'System' }
];

/* ── log types — mirrors `ELogType`. Glyph + colour are pulled from the same
   `logColors`/`LogGlyph` vocabulary the real combat log uses, so a row looks
   exactly like the messages it controls. ───────────────────────────────── */
export interface LogTypeDef {
	id: ELogType;
	group: string;
	name: string;
	desc: string;
	glyph: GlyphKind;
	color: string;
}

export const LOG_TYPES: LogTypeDef[] = [
	{
		id: ELogType.Damage,
		group: 'combat',
		name: 'Combat Damage',
		desc: 'Hits you deal and take during battle.',
		glyph: 'hit',
		color: logColors.player
	},
	{
		id: ELogType.EnemyDefeated,
		group: 'combat',
		name: 'Enemy Defeated',
		desc: 'Victories and your own defeats.',
		glyph: 'kill',
		color: logColors.loot
	},
	{
		id: ELogType.Exp,
		group: 'progression',
		name: 'Experience',
		desc: 'XP earned from winning battles.',
		glyph: 'crit',
		color: logColors.reward
	},
	{
		id: ELogType.LevelUp,
		group: 'progression',
		name: 'Level Up',
		desc: 'Alerts when your hero reaches a new level.',
		glyph: 'crit',
		color: logColors.reward
	},
	{
		id: ELogType.ItemFound,
		group: 'items',
		name: 'Items Found',
		desc: 'Loot dropped by enemies and zones.',
		glyph: 'loot',
		color: logColors.loot
	},
	{
		id: ELogType.Debug,
		group: 'system',
		name: 'Debug',
		desc: 'Verbose engine output, for troubleshooting.',
		glyph: 'system',
		color: logColors.system
	}
];

/* ── settings categories — Logging is built; the rest show that the shell
   extends. Flip `built` to wire up a new category. ──────────────────────── */
export type SettingsGlyphKind = 'logging' | 'display' | 'audio' | 'gameplay' | 'account';

export interface SettingsCatDef {
	key: string;
	label: string;
	glyph: SettingsGlyphKind;
	built: boolean;
	blurb?: string;
}

export const SETTINGS_CATS: SettingsCatDef[] = [
	{ key: 'logging', label: 'Logging', glyph: 'logging', built: true, blurb: 'Combat Log' },
	{ key: 'display', label: 'Display', glyph: 'display', built: false },
	{ key: 'audio', label: 'Audio', glyph: 'audio', built: false },
	{ key: 'gameplay', label: 'Gameplay', glyph: 'gameplay', built: false },
	{ key: 'account', label: 'Account', glyph: 'account', built: false }
];

/** Draft/baseline state is a plain `{ [ELogType]: enabled }` map. */
export type LogPrefMap = Record<number, boolean>;

/* ── reactive view-model ─────────────────────────────────────────────────
   Owns the working copy (`draft`) and the persisted baseline (`baseline`),
   derives the dirty set by comparison, and — mirroring the original
   `LogManager.saveSettingChanges` — persists only the actual diff. */
export class OptionsView {
	category = $state<string>('logging');
	/** Working copy edited by the toggles. */
	draft = $state<LogPrefMap>({});
	/** Last-persisted state; the dirty set is `draft` minus this. */
	baseline = $state<LogPrefMap>({});
	/** Brief "Preferences saved" confirmation flash. */
	saved = $state(false);
	/** True while a save request is in flight (guards against double-submit). */
	saving = $state(false);

	#flashTimer: ReturnType<typeof setTimeout> | undefined;

	constructor() {
		const initial = readPlayerPrefs();
		this.draft = { ...initial };
		this.baseline = { ...initial };
	}

	/** Log types whose draft value differs from the persisted baseline. */
	readonly dirtyIds = $derived(LOG_TYPES.filter((lt) => this.draft[lt.id] !== this.baseline[lt.id]).map((lt) => lt.id));
	readonly dirtyCount = $derived(this.dirtyIds.length);
	readonly isDirty = $derived(this.dirtyCount > 0);
	readonly enabledCount = $derived(LOG_TYPES.filter((lt) => this.draft[lt.id]).length);

	isOn(id: ELogType): boolean {
		return !!this.draft[id];
	}

	isDirtyId(id: ELogType): boolean {
		return this.draft[id] !== this.baseline[id];
	}

	pickCategory(key: string): void {
		this.category = key;
	}

	setOne(id: ELogType, enabled: boolean): void {
		this.draft = { ...this.draft, [id]: enabled };
		this.saved = false;
	}

	setMany(ids: ELogType[], enabled: boolean): void {
		const next = { ...this.draft };
		for (const id of ids) {
			next[id] = enabled;
		}
		this.draft = next;
		this.saved = false;
	}

	discard(): void {
		this.draft = { ...this.baseline };
		this.saved = false;
	}

	/** The minimal set of preferences to persist — only the changed ones.
	 *  Computed from plain state (not the `$derived` getters) so it is safe to
	 *  read from non-reactive call sites like {@link save}. */
	get changedPreferences(): ILogPreference[] {
		return LOG_TYPES.filter((lt) => this.draft[lt.id] !== this.baseline[lt.id]).map((lt) => ({
			id: lt.id,
			enabled: this.draft[lt.id]
		}));
	}

	async save(): Promise<void> {
		const changed = this.changedPreferences;
		if (changed.length === 0 || this.saving) {
			return;
		}

		this.saving = true;
		let ok = false;
		try {
			const response = await apiSocket.sendSocketCommand('SaveLogPreferences', changed);
			ok = !response.error;
		} catch {
			ok = false;
		} finally {
			this.saving = false;
		}

		// Apply locally regardless so the combat-log filter reflects the choice
		// for the rest of the session, then advance the baseline to clear dirty.
		applyToPlayer(this.draft);
		this.baseline = { ...this.draft };

		if (ok) {
			this.flashSaved();
		} else {
			toastError('Log preferences were applied for this session but could not be saved to the server.');
		}
	}

	private flashSaved(): void {
		this.saved = true;
		if (this.#flashTimer) {
			clearTimeout(this.#flashTimer);
		}
		this.#flashTimer = setTimeout(() => {
			this.saved = false;
		}, 1900);
	}

	dispose(): void {
		if (this.#flashTimer) {
			clearTimeout(this.#flashTimer);
		}
	}
}

/** Seed a preference map from the player's stored preferences, defaulting a
 *  missing type to enabled (matching the log writer's `?? true` fallback). */
function readPlayerPrefs(): LogPrefMap {
	const prefs: LogPrefMap = {};
	for (const lt of LOG_TYPES) {
		prefs[lt.id] = playerManager.logPreferences.find((p) => p.id === lt.id)?.enabled ?? true;
	}
	return prefs;
}

/** Push the draft map onto the player manager so `log.ts` filters by it live. */
function applyToPlayer(draft: LogPrefMap): void {
	playerManager.logPreferences = LOG_TYPES.map((lt) => ({ id: lt.id, enabled: !!draft[lt.id] }));
}
