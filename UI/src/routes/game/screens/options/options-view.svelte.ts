/* Options screen — the old logging popup reimagined as a full, extensible
   in-game settings screen.

   The settings shell is config-driven: a category rail (Logging is built; the
   rest are placeholders that demonstrate the shell extends cleanly) plus the
   active category's content. Adding a future category — or a new `ELogType` —
   is a one-line edit to the arrays below rather than new UI. */

import { apiSocket, ELogType, type ILogPreference } from '$lib/api';
import { playerManager } from '$lib/engine';
import { logColors } from '$components';
import { SaveFlash } from '$lib/common';
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
		id: ELogType.SkillEffect,
		group: 'combat',
		name: 'Skill Effects',
		desc: 'Buffs, debuffs, and damage/heal over time.',
		glyph: 'effect',
		color: logColors.effect
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
		id: ELogType.Proficiency,
		group: 'progression',
		name: 'Proficiency',
		desc: 'Proficiency XP, level-ups, milestones, and unlocks from won battles.',
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
}

export const SETTINGS_CATS: SettingsCatDef[] = [
	{ key: 'logging', label: 'Logging', glyph: 'logging', built: true },
	{ key: 'display', label: 'Display', glyph: 'display', built: false },
	{ key: 'audio', label: 'Audio', glyph: 'audio', built: false },
	{ key: 'gameplay', label: 'Gameplay', glyph: 'gameplay', built: false },
	{ key: 'account', label: 'Account', glyph: 'account', built: false }
];

/** Draft/baseline state is a plain `{ [ELogType]: enabled }` map. */
export type LogPrefMap = Record<number, boolean>;

/* ── reactive view-model ─────────────────────────────────────────────────
   Owns the persisted baseline (`baseline`) and the working copy (`draft`),
   derives the dirty set by comparison, and — mirroring the original
   `LogManager.saveSettingChanges` — persists only the actual diff.

   `baseline` is read live off the player manager rather than snapshotted once
   at construction: a mid-session resync (`playerManager.initialize()`, #1809)
   reassigns `logPreferences` wholesale, and the baseline must track it or the
   screen keeps showing pre-resync values and diffs against a server state that
   no longer exists. `draft` layers explicitly-touched overrides
   (`#draftOverrides`) over `baseline`, but only while `#draftOverridesPrefs` —
   the manager `logPreferences` reference those overrides were computed against
   — still matches the live one; {@link effectiveOverrides} treats them as
   empty the moment it doesn't. A not-yet-saved toggle can't be trusted to
   still apply cleanly against a baseline it was never computed against, and an
   **external** resync (not this view's own `save`, which keeps the marker in
   lockstep) is by definition the fresher truth — so it silently drops the
   toggle rather than layering it onto the new baseline. `save` instead
   consumes exactly the overrides it sent, preserving any toggle made while
   that request was in flight (#1506). */
export class OptionsView {
	category = $state<string>('logging');
	/** Log types toggled since the last save/discard, valid only against
	 *  {@link #draftOverridesPrefs} — see {@link effectiveOverrides}. */
	#draftOverrides = $state<Partial<Record<ELogType, boolean>>>({});
	/** The `playerManager.logPreferences` reference {@link #draftOverrides} was computed against;
	 *  `null` never matches, so overrides read as empty until the first toggle stamps it. */
	#draftOverridesPrefs = $state<ILogPreference[] | null>(null);
	/** Brief "Preferences saved" confirmation flash. */
	#saveFlash = new SaveFlash();
	/** True while a save request is in flight (guards against double-submit). */
	saving = $state(false);

	/** Brief "Preferences saved" confirmation flash. */
	get saved(): boolean {
		return this.#saveFlash.active;
	}

	/** Last-persisted preferences, read live off the player manager (see the class doc). */
	readonly baseline = $derived(readPlayerPrefs());

	/** {@link #draftOverrides} if they're still valid against the live manager state, otherwise
	 *  empty — a pure read (no mutation), so it's safe to call from a `$derived` (see the class doc). */
	private effectiveOverrides(): Partial<Record<ELogType, boolean>> {
		return this.#draftOverridesPrefs === playerManager.logPreferences ? this.#draftOverrides : {};
	}

	/** Working preferences: {@link baseline} with the {@link effectiveOverrides} layered on top. */
	readonly draft = $derived.by<LogPrefMap>(() => {
		const map: LogPrefMap = { ...this.baseline };
		const overrides = this.effectiveOverrides();
		for (const lt of LOG_TYPES) {
			const override = overrides[lt.id];
			if (override !== undefined) {
				map[lt.id] = override;
			}
		}
		return map;
	});

	/** Log types whose draft value differs from the persisted baseline. */
	readonly dirtyIds = $derived(LOG_TYPES.filter((lt) => this.isDirtyId(lt.id)).map((lt) => lt.id));
	readonly dirtyCount = $derived(this.dirtyIds.length);
	readonly isDirty = $derived(this.dirtyCount > 0);
	readonly enabledCount = $derived(LOG_TYPES.filter((lt) => this.draft[lt.id]).length);

	isOn(id: ELogType): boolean {
		return !!this.draft[id];
	}

	/** Whether a log type's draft value differs from the persisted baseline — the single
	 *  dirty predicate the dirty set and the changed-preferences diff both build on. */
	isDirtyId(id: ELogType): boolean {
		return this.draft[id] !== this.baseline[id];
	}

	pickCategory(key: string): void {
		this.category = key;
	}

	setOne(id: ELogType, enabled: boolean): void {
		this.#draftOverrides = { ...this.effectiveOverrides(), [id]: enabled };
		this.#draftOverridesPrefs = playerManager.logPreferences;
		this.#saveFlash.reset();
	}

	setMany(ids: ELogType[], enabled: boolean): void {
		const next = { ...this.effectiveOverrides() };
		for (const id of ids) {
			next[id] = enabled;
		}
		this.#draftOverrides = next;
		this.#draftOverridesPrefs = playerManager.logPreferences;
		this.#saveFlash.reset();
	}

	discard(): void {
		this.#draftOverrides = {};
		this.#draftOverridesPrefs = playerManager.logPreferences;
		this.#saveFlash.reset();
	}

	/** The minimal set of preferences to persist — only the changed ones.
	 *  Computed from plain state (not the `$derived` getters) so it is safe to
	 *  read from non-reactive call sites like {@link save}. */
	get changedPreferences(): ILogPreference[] {
		return LOG_TYPES.filter((lt) => this.isDirtyId(lt.id)).map((lt) => ({
			id: lt.id,
			enabled: this.draft[lt.id]
		}));
	}

	async save(): Promise<void> {
		const changed = this.changedPreferences;
		if (changed.length === 0 || this.saving) {
			return;
		}

		// The exact values being sent, so an override can be told apart below from one that was
		// re-touched while the request was in flight. A transient local (not reactive state).
		// eslint-disable-next-line svelte/prefer-svelte-reactivity
		const sentValues = new Map(changed.map((pref) => [pref.id, pref.enabled]));

		this.saving = true;
		const response = await apiSocket.sendSocketCommand('SaveLogPreferences', changed);
		this.saving = false;

		// On failure keep the change dirty (baseline unadvanced, not applied to the
		// player) so Save/Discard stay enabled and the user can retry (#701).
		if (response.error) {
			toastError('Log preferences could not be saved. Please try again.');
			return;
		}

		// Snapshot the current effective overrides (any mid-flight toggle included) before
		// applyToPlayer's reassignment below moves the resync marker out from under them.
		const currentOverrides = this.effectiveOverrides();

		// Applying to the player manager advances the live baseline (see the class doc); this
		// view's own reassignment isn't an external resync, so re-pin the overrides against it.
		applyToPlayer(changed);
		this.#draftOverridesPrefs = playerManager.logPreferences;

		// Drop an override only if it still holds exactly the value that was sent — one re-touched
		// while the request was in flight stays pending instead of being silently cleared (#1506).
		const remaining = { ...currentOverrides };
		for (const [id, sentValue] of sentValues) {
			if (remaining[id] === sentValue) {
				delete remaining[id];
			}
		}
		this.#draftOverrides = remaining;
		this.#saveFlash.flash();
	}

	dispose(): void {
		this.#saveFlash.dispose();
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

/** Merge the saved preference entries onto the player manager so `log.ts` filters by
 *  them live, leaving every preference that was not sent at its current value. */
function applyToPlayer(saved: ILogPreference[]): void {
	const merged = readPlayerPrefs();
	for (const pref of saved) {
		merged[pref.id] = pref.enabled;
	}
	playerManager.logPreferences = LOG_TYPES.map((lt) => ({ id: lt.id, enabled: !!merged[lt.id] }));
}
