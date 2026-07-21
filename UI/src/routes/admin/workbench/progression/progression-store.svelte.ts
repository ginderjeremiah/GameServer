import {
	ApiRequest,
	EChangeType,
	fetchSocketData,
	type IChange,
	type IPath,
	type IProficiency,
	type ISetProficiencyPrerequisitesData
} from '$lib/api';
import { SaveFlash } from '$lib/common';
import { staticData, toastError } from '$stores';
import { reference } from '../reference.svelte';
import { childChanged, canonicalEqual, resolveId, resolveNewIds } from '../save-helpers';
import { firstFree } from '../entities/helpers';
import {
	blankModifier,
	newPath,
	newProficiency,
	pathIdentityDto,
	proficiencyBlockingWarnings,
	profIdentityDto,
	renumberTiers,
	tiersOfPath
} from './progression-helpers';
import { NO_SKILL, type WorkbenchPath, type WorkbenchProficiency } from './types';

const clone = <T>(value: T): T => JSON.parse(JSON.stringify(value));

export type RecordStatus = 'clean' | 'added' | 'modified';
export type PathTab = 'identity' | 'tiers';
export type TierTab = 'identity' | 'xp' | 'milestones' | 'gateways';

/** Format a one-shot retire timestamp. Not reactive state — just an ISO string. */
const nowIso = () => new Date().toISOString();

/**
 * Working store for the two cross-referenced progression catalogues (paths + their proficiency tiers),
 * driving one bespoke master–detail surface under a single save bar. Unlike the generic single-entity
 * {@link EntityStore}, it tracks both catalogues, the path→tier drill-down selection, and a combined
 * save that resolves new path ids before the proficiencies that reference them.
 */
export class ProgressionStore {
	paths = $state<WorkbenchPath[]>([]);
	profs = $state<WorkbenchProficiency[]>([]);
	private basePaths = $state<WorkbenchPath[]>([]);
	private baseProfs = $state<WorkbenchProficiency[]>([]);

	loaded = $state(false);
	saving = $state(false);
	#saveFlash = new SaveFlash();
	error = $state<string | null>(null);

	// Selection / navigation.
	selectedPathId = $state<number | null>(null);
	drilledTierId = $state<number | null>(null);
	pathTab = $state<PathTab>('tiers');
	tierTab = $state<TierTab>('milestones');
	selectedLevel = $state(1);

	private nextId = -1;

	/** Brief "Changes saved" confirmation flash. */
	get saved(): boolean {
		return this.#saveFlash.active;
	}

	// ── Loading / seeding ──

	async load() {
		this.error = null;
		try {
			const [paths, profs] = await Promise.all([fetchSocketData('GetPaths'), fetchSocketData('GetProficiencies')]);
			this.setData(paths, profs);
			this.selectedPathId = this.paths[0]?.id ?? null;
			this.drilledTierId = null;
			this.pathTab = 'tiers';
			this.loaded = true;
		} catch (ex) {
			const message = ex instanceof Error ? ex.message : 'Failed to load progression data.';
			this.error = message;
			toastError(message);
		}
	}

	/** Replace the working + baseline data from server truth. */
	private setData(paths: IPath[], profs: IProficiency[]) {
		this.paths = paths.map(clone);
		this.basePaths = paths.map(clone);
		this.profs = profs.map(clone);
		this.baseProfs = profs.map(clone);
		staticData.paths = paths;
		staticData.proficiencies = profs;
	}

	// ── Baselines / diffing ──

	// Plain Record lookups (not Map) so the reactive-class lint stays happy, mirroring EntityStore.
	private basePathMap = $derived.by<Record<number, WorkbenchPath>>(() => {
		const map: Record<number, WorkbenchPath> = {};
		for (const p of this.basePaths) {
			map[p.id] = p;
		}
		return map;
	});
	private baseProfMap = $derived.by<Record<number, WorkbenchProficiency>>(() => {
		const map: Record<number, WorkbenchProficiency> = {};
		for (const p of this.baseProfs) {
			map[p.id] = p;
		}
		return map;
	});

	/**
	 * Per-record status, memoised per record object (mirroring EntityStore.recordStates): a patch
	 * replaces only the one record it touched, so every other record is a same-reference cache hit
	 * instead of a re-canonicalized comparison against its baseline. No epoch invalidation is needed
	 * here (unlike EntityStore) — this store has no soft-delete side channel that can flip a status
	 * without also replacing the record's reference.
	 */
	private pathStateCache = new WeakMap<WorkbenchPath, RecordStatus>();
	private profStateCache = new WeakMap<WorkbenchProficiency, RecordStatus>();

	private pathStatuses = $derived.by<Record<number, RecordStatus>>(() => {
		const map: Record<number, RecordStatus> = {};
		for (const path of this.paths) {
			let status = this.pathStateCache.get(path);
			if (status === undefined) {
				const baseline = this.basePathMap[path.id];
				status = !baseline ? 'added' : canonicalEqual(path, baseline) ? 'clean' : 'modified';
				this.pathStateCache.set(path, status);
			}
			map[path.id] = status;
		}
		return map;
	});
	private profStatuses = $derived.by<Record<number, RecordStatus>>(() => {
		const map: Record<number, RecordStatus> = {};
		for (const prof of this.profs) {
			let status = this.profStateCache.get(prof);
			if (status === undefined) {
				const baseline = this.baseProfMap[prof.id];
				status = !baseline ? 'added' : canonicalEqual(prof, baseline) ? 'clean' : 'modified';
				this.profStateCache.set(prof, status);
			}
			map[prof.id] = status;
		}
		return map;
	});

	private pathDiff = $derived.by(() => {
		const added: WorkbenchPath[] = [];
		const modified: { record: WorkbenchPath; baseline: WorkbenchPath }[] = [];
		for (const path of this.paths) {
			if (this.pathStatuses[path.id] === 'added') {
				added.push(path);
			} else if (this.pathStatuses[path.id] === 'modified') {
				modified.push({ record: path, baseline: this.basePathMap[path.id] });
			}
		}
		return { added, modified };
	});
	private profDiff = $derived.by(() => {
		const added: WorkbenchProficiency[] = [];
		const modified: { record: WorkbenchProficiency; baseline: WorkbenchProficiency }[] = [];
		for (const prof of this.profs) {
			if (this.profStatuses[prof.id] === 'added') {
				added.push(prof);
			} else if (this.profStatuses[prof.id] === 'modified') {
				modified.push({ record: prof, baseline: this.baseProfMap[prof.id] });
			}
		}
		return { added, modified };
	});

	counts = $derived({
		added: this.pathDiff.added.length + this.profDiff.added.length,
		modified: this.pathDiff.modified.length + this.profDiff.modified.length
	});

	get totalChanges(): number {
		return this.counts.added + this.counts.modified;
	}

	/** True while any not-yet-saved tier (added/modified) carries an out-of-range modifier/reward
	 *  level — the one proficiency condition the backend genuinely hard-rejects (mirrors
	 *  EntityStore.hasBlockingWarnings, #2217/#2222). Gates {@link save}. */
	hasBlockingWarnings = $derived.by(() => {
		return this.profs.some((prof) => {
			const status = this.profStatuses[prof.id];
			return (status === 'added' || status === 'modified') && proficiencyBlockingWarnings(prof).length > 0;
		});
	});

	/** Always called with a record from {@link paths}, which {@link pathStatuses} covers exhaustively. */
	pathStatus(path: WorkbenchPath): RecordStatus {
		return this.pathStatuses[path.id];
	}

	/** Always called with a record from {@link profs}, which {@link profStatuses} covers exhaustively. */
	profStatus(prof: WorkbenchProficiency): RecordStatus {
		return this.profStatuses[prof.id];
	}

	isRetired(record: { retiredAt?: string | null }): boolean {
		return record.retiredAt != null;
	}

	pathBaseline(id: number): WorkbenchPath | undefined {
		return this.basePathMap[id];
	}

	profBaseline(id: number): WorkbenchProficiency | undefined {
		return this.baseProfMap[id];
	}

	// ── Derived selection ──

	get selectedPath(): WorkbenchPath | undefined {
		return this.paths.find((p) => p.id === this.selectedPathId);
	}

	get currentTiers(): WorkbenchProficiency[] {
		return this.selectedPathId == null ? [] : tiersOfPath(this.profs, this.selectedPathId);
	}

	get drilledTier(): WorkbenchProficiency | undefined {
		return this.drilledTierId == null ? undefined : this.profs.find((p) => p.id === this.drilledTierId);
	}

	// ── Navigation ──

	selectPath(id: number) {
		this.selectedPathId = id;
		this.drilledTierId = null;
		this.pathTab = 'tiers';
	}

	drillTier(id: number) {
		const tier = this.profs.find((p) => p.id === id);
		this.drilledTierId = id;
		this.tierTab = 'milestones';
		const firstReward = tier?.levelRewards.slice().sort((a, b) => a.level - b.level)[0];
		this.selectedLevel = firstReward ? firstReward.level : (tier?.maxLevel ?? 1);
	}

	back() {
		this.drilledTierId = null;
	}

	setPathTab(tab: PathTab) {
		this.pathTab = tab;
	}

	setTierTab(tab: TierTab) {
		this.tierTab = tab;
	}

	selectLevel(level: number) {
		this.selectedLevel = level;
	}

	// ── Record patches ──

	/**
	 * Edits made while a save is in flight would either be silently overwritten by the post-save
	 * reseed from server truth or land as a "clean" record whose dirty indicator never fires — so
	 * every mutator no-ops while {@link saving} is true rather than risk either.
	 */
	patchPath(id: number, mutate: (draft: WorkbenchPath) => void) {
		if (this.saving) {
			return;
		}
		this.paths = this.paths.map((path) => {
			if (path.id !== id) {
				return path;
			}
			const draft = clone(path);
			mutate(draft);
			return draft;
		});
		this.#saveFlash.reset();
	}

	patchProf(id: number, mutate: (draft: WorkbenchProficiency) => void) {
		if (this.saving) {
			return;
		}
		this.profs = this.profs.map((prof) => {
			if (prof.id !== id) {
				return prof;
			}
			const draft = clone(prof);
			mutate(draft);
			return draft;
		});
		this.#saveFlash.reset();
	}

	// ── Add / reorder / retire ──

	addPath() {
		if (this.saving) {
			return;
		}
		const pathId = this.nextId--;
		const tierId = this.nextId--;
		this.paths = [newPath(pathId), ...this.paths];
		this.profs = [newProficiency(tierId, pathId, 0), ...this.profs];
		this.selectedPathId = pathId;
		this.drilledTierId = null;
		this.pathTab = 'identity';
		this.#saveFlash.reset();
	}

	addTier(pathId: number): number {
		if (this.saving) {
			return tiersOfPath(this.profs, pathId)[0]?.id ?? 0;
		}
		const tiers = tiersOfPath(this.profs, pathId);
		const ordinal = tiers.length ? Math.max(...tiers.map((t) => t.pathOrdinal)) + 1 : 0;
		const id = this.nextId--;
		this.profs = [newProficiency(id, pathId, ordinal), ...this.profs];
		this.#saveFlash.reset();
		return id;
	}

	reorderTiers(pathId: number, fromIndex: number, toIndex: number) {
		if (this.saving) {
			return;
		}
		const tiers = tiersOfPath(this.profs, pathId);
		if (fromIndex < 0 || toIndex < 0 || fromIndex >= tiers.length || toIndex >= tiers.length || fromIndex === toIndex) {
			return;
		}
		const reordered = [...tiers];
		const [moved] = reordered.splice(fromIndex, 1);
		reordered.splice(toIndex, 0, moved);
		const renumbered = renumberTiers(reordered);
		this.profs = this.profs.map((prof) => {
			const match = renumbered.find((t) => t.id === prof.id);
			return match ? { ...prof, pathOrdinal: match.pathOrdinal } : prof;
		});
		this.#saveFlash.reset();
	}

	retirePath(id: number, retired: boolean) {
		this.patchPath(id, (draft) => {
			draft.retiredAt = retired ? nowIso() : undefined;
		});
	}

	retireProf(id: number, retired: boolean) {
		this.patchProf(id, (draft) => {
			draft.retiredAt = retired ? nowIso() : undefined;
		});
	}

	/** Remove a never-saved path (and its never-saved tiers) locally; reconcile the selection. */
	removePath(id: number) {
		if (this.saving) {
			return;
		}
		this.paths = this.paths.filter((path) => path.id !== id);
		this.profs = this.profs.filter((prof) => prof.pathId !== id);
		if (this.selectedPathId === id) {
			this.selectedPathId = this.paths[0]?.id ?? null;
			this.drilledTierId = null;
		}
		this.#saveFlash.reset();
	}

	/** Remove a never-saved tier locally; leave the drill view if it was open. */
	removeTier(id: number) {
		if (this.saving) {
			return;
		}
		this.profs = this.profs.filter((prof) => prof.id !== id);
		if (this.drilledTierId === id) {
			this.drilledTierId = null;
		}
		this.#saveFlash.reset();
	}

	resetPath(id: number) {
		const baseline = this.basePathMap[id];
		if (baseline) {
			this.patchPath(id, (draft) => Object.assign(draft, clone(baseline)));
		}
	}

	resetProf(id: number) {
		const baseline = this.baseProfMap[id];
		if (baseline) {
			this.patchProf(id, (draft) => Object.assign(draft, clone(baseline)));
		}
	}

	// ── Milestone payouts (proficiency modifiers + rewards) ──

	addModifier(profId: number, level: number) {
		this.patchProf(profId, (draft) => {
			const usedAttrs = draft.levelModifiers.filter((m) => m.level === level).map((m) => m.attributeId);
			const attributeId = firstFree(usedAttrs, reference.attributeOptions());
			draft.levelModifiers = [...draft.levelModifiers, { ...blankModifier(level), attributeId }];
		});
	}

	updateModifier(
		profId: number,
		index: number,
		patch: Partial<{ attributeId: number; modifierTypeId: number; amount: number }>
	) {
		this.patchProf(profId, (draft) => {
			const row = draft.levelModifiers[index];
			if (row) {
				draft.levelModifiers[index] = { ...row, ...patch };
			}
		});
	}

	removeModifier(profId: number, index: number) {
		this.patchProf(profId, (draft) => {
			draft.levelModifiers = draft.levelModifiers.filter((_row, i) => i !== index);
		});
	}

	/** Upsert (skillId ≥ 0) or clear (NO_SKILL) the milestone reward skill at a level. */
	setReward(profId: number, level: number, skillId: number) {
		this.patchProf(profId, (draft) => {
			const others = draft.levelRewards.filter((r) => r.level !== level);
			draft.levelRewards = skillId === NO_SKILL ? others : [...others, { level, rewardSkillId: skillId }];
		});
	}

	/** Make a level a payout by seeding a blank modifier (an attribute bonus the author then tunes). */
	addPayout(profId: number, level: number) {
		this.addModifier(profId, level);
	}

	/** Drop every payout (modifiers + reward) at a level. */
	removePayout(profId: number, level: number) {
		this.patchProf(profId, (draft) => {
			draft.levelModifiers = draft.levelModifiers.filter((m) => m.level !== level);
			draft.levelRewards = draft.levelRewards.filter((r) => r.level !== level);
		});
	}

	// ── Gateways (cross-path prerequisites) ──

	addPrerequisite(profId: number, prerequisiteId: number) {
		this.patchProf(profId, (draft) => {
			if (!draft.prerequisiteIds.includes(prerequisiteId)) {
				draft.prerequisiteIds = [...draft.prerequisiteIds, prerequisiteId];
			}
		});
	}

	removePrerequisite(profId: number, prerequisiteId: number) {
		this.patchProf(profId, (draft) => {
			draft.prerequisiteIds = draft.prerequisiteIds.filter((id) => id !== prerequisiteId);
		});
	}

	// ── Save / discard ──

	async save() {
		if (this.totalChanges === 0 || this.saving || this.hasBlockingWarnings) {
			return;
		}
		this.saving = true;
		let committed = false;
		const baseProfMap = this.baseProfMap;
		const pathDiff = this.pathDiff;
		const profDiff = this.profDiff;

		// Recovery tracking for a partial-failure rebase (#2238, porting #2207/#2218's EntityStore
		// pattern): what this save's pipeline has actually written by the point a later step throws.
		// A path settles atomically with its one identity batch (it has no child collections); a
		// proficiency settles once its identity batch *and* every one of its own changed child
		// collections (modifiers/rewards/prerequisites) has posted — tracked per persisted id since
		// the child writes run after each catalogue's new ids are resolved. The id maps double as the
		// "did this save's own pipeline resolve every add's persisted id" check the recovery pass needs.
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- transient lookup, not held state
		let pathIdMap = new Map<number, number>();
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- transient lookup, not held state
		let profIdMap = new Map<number, number>();
		let pathIdentityWritten = false;
		let profIdentityWritten = false;
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- transient lookup, not held state
		const modifiersWritten = new Set<number>();
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- transient lookup, not held state
		const rewardsWritten = new Set<number>();
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- transient lookup, not held state
		const prerequisitesWritten = new Set<number>();

		try {
			// 1. Cross-path prerequisite changes that touch only already-persisted tiers need no id remap,
			// so — only when this save is also retiring a path — they're posted immediately, before path
			// identities. This lets a save that both retires a path and removes the now-offending gateway
			// prerequisite (on some other, already-persisted tier) succeed in one shot: the retire guard
			// (AdminPaths.FindRetiredPathGatingLiveGateway) validates against the live cache, which this
			// write has already reloaded by the time the path save below runs (see #1776). Splitting into
			// two POSTs narrows the "single combined batch" cycle-safety guarantee below (step 7), so it's
			// scoped to exactly the case that needs it: a non-retiring save always keeps every prerequisite
			// change in one batch, preserving the full invariant.
			const hasPendingRetire = pathDiff.modified.some(
				({ record, baseline }) => record.retiredAt != null && baseline.retiredAt == null
			);
			const allPrerequisiteChanges = [...profDiff.added, ...profDiff.modified.map((m) => m.record)]
				.filter((prof) => childChanged(prof.prerequisiteIds, baseProfMap[prof.id]?.prerequisiteIds))
				.map((prof) => ({ prof, prerequisiteIds: prof.prerequisiteIds }));
			const resolvableNow = hasPendingRetire
				? allPrerequisiteChanges.filter(
						({ prof, prerequisiteIds }) => prof.id >= 0 && prerequisiteIds.every((id) => id >= 0)
					)
				: [];
			if (resolvableNow.length) {
				const changes: ISetProficiencyPrerequisitesData[] = resolvableNow.map(({ prof, prerequisiteIds }) => ({
					id: prof.id,
					prerequisiteIds
				}));
				await ApiRequest.post('AdminTools/SetProficiencyPrerequisites', changes);
				committed = true;
				for (const { prof } of resolvableNow) {
					prerequisitesWritten.add(prof.id);
				}
			}

			// 2. Proficiency child-collection removals that resolve an about-to-shrink MaxLevel are posted
			// before the proficiency identities below (step 4). The backend's shrunken-MaxLevel guard
			// (AdminProficiencies.FindShrunkenMaxLevelViolation) validates a MaxLevel edit against the
			// proficiency's *currently-persisted* modifiers/rewards, so an operator who lowers MaxLevel and
			// removes the now-out-of-range payout in the same save would otherwise be rejected — the guard
			// would see the still-persisted payout, since the child setters (step 6) run after the identity
			// edit. Scoped to exactly the profs where the removal actually clears the violation; a shrink
			// that leaves an offending payout in place still reaches the identity edit unsplit, so the
			// backend correctly rejects it (see #1827/#1804).
			const shrinksPastPersistedPayout = ({
				record,
				baseline
			}: {
				record: WorkbenchProficiency;
				baseline: WorkbenchProficiency;
			}) => {
				if (record.maxLevel >= baseline.maxLevel) {
					return false;
				}
				const payoutLevel = (prof: WorkbenchProficiency) =>
					[...prof.levelModifiers, ...prof.levelRewards].map((row) => row.level);
				const persistedOffends = payoutLevel(baseline).some((level) => level > record.maxLevel);
				const stillOffends = payoutLevel(record).some((level) => level > record.maxLevel);
				return persistedOffends && !stillOffends;
			};
			const earlyChildRemovals = profDiff.modified.filter(shrinksPastPersistedPayout);
			for (const { record, baseline } of earlyChildRemovals) {
				if (childChanged(record.levelModifiers, baseline.levelModifiers)) {
					await ApiRequest.post('AdminTools/SetProficiencyModifiers', {
						id: record.id,
						modifiers: record.levelModifiers
					});
					committed = true;
					modifiersWritten.add(record.id);
				}
				if (childChanged(record.levelRewards, baseline.levelRewards)) {
					await ApiRequest.post('AdminTools/SetProficiencyRewards', { id: record.id, rewards: record.levelRewards });
					committed = true;
					rewardsWritten.add(record.id);
				}
			}
			// eslint-disable-next-line svelte/prefer-svelte-reactivity -- transient lookup, not held state
			const earlyChildRemovalIds = new Set(earlyChildRemovals.map(({ record }) => record.id));

			// 3. Path identities — send an Edit only when the identity DTO itself changed.
			const pathChanges: IChange<ReturnType<typeof pathIdentityDto>>[] = [
				...pathDiff.added.map((p) => ({ changeType: EChangeType.Add, item: pathIdentityDto(p) })),
				...pathDiff.modified
					.filter(({ record, baseline }) => !canonicalEqual(pathIdentityDto(record), pathIdentityDto(baseline)))
					.map(({ record }) => ({ changeType: EChangeType.Edit, item: pathIdentityDto(record) }))
			];
			if (pathChanges.length) {
				await ApiRequest.post('AdminTools/AddEditPaths', pathChanges);
				committed = true;
			}
			pathIdentityWritten = true;

			// 4. Resolve the persisted ids of newly-added paths before the proficiencies that FK to them.
			// Identity-content matching (not just position) keeps this correct when another admin's
			// concurrent add lands in the same refetch (#1856).
			const freshPaths = await fetchSocketData('GetPaths');
			pathIdMap = resolveNewIds(
				freshPaths,
				this.basePaths.map((p) => p.id),
				pathDiff.added,
				pathIdentityDto
			);

			// 5. Proficiency identities — remap a (possibly brand-new) path id into each DTO.
			const toProfDto = (prof: WorkbenchProficiency) => ({
				...profIdentityDto(prof),
				pathId: resolveId(prof.pathId, pathIdMap)
			});
			const profChanges: IChange<ReturnType<typeof toProfDto>>[] = [
				...profDiff.added.map((p) => ({ changeType: EChangeType.Add, item: toProfDto(p) })),
				...profDiff.modified
					.filter(({ record, baseline }) => !canonicalEqual(profIdentityDto(record), profIdentityDto(baseline)))
					.map(({ record }) => ({ changeType: EChangeType.Edit, item: toProfDto(record) }))
			];
			if (profChanges.length) {
				await ApiRequest.post('AdminTools/AddEditProficiencies', profChanges);
				committed = true;
			}
			profIdentityWritten = true;

			// 6. Resolve the persisted ids of newly-added proficiencies (for child savers + gateways).
			// Identity-content matching keeps this correct under the same concurrent-add race (#1856).
			// Uses toProfDto (not the raw profIdentityDto) so a proficiency added under a path that's
			// also new in this save keys on the path's *resolved* id on both sides — profDiff.added's
			// record still carries the path's local negative id, which would otherwise never match the
			// server's real pathId and silently fall back to positional pairing.
			const freshProfs = await fetchSocketData('GetProficiencies');
			profIdMap = resolveNewIds(
				freshProfs,
				this.baseProfs.map((p) => p.id),
				profDiff.added,
				toProfDto
			);

			// 7. Proficiency child collections — modifiers and rewards per tier, minus the ones already
			// posted early in step 2.
			for (const prof of [...profDiff.added, ...profDiff.modified.map((m) => m.record)]) {
				if (earlyChildRemovalIds.has(prof.id)) {
					continue;
				}
				const baseline = baseProfMap[prof.id];
				const id = resolveId(prof.id, profIdMap);
				if (childChanged(prof.levelModifiers, baseline?.levelModifiers)) {
					await ApiRequest.post('AdminTools/SetProficiencyModifiers', { id, modifiers: prof.levelModifiers });
					committed = true;
					modifiersWritten.add(id);
				}
				if (childChanged(prof.levelRewards, baseline?.levelRewards)) {
					await ApiRequest.post('AdminTools/SetProficiencyRewards', { id, rewards: prof.levelRewards });
					committed = true;
					rewardsWritten.add(id);
				}
			}

			// 8. Every prerequisite change not already posted in step 1 (the common case: everything, when
			// this save isn't retiring a path) collected into one combined batch: the backend validates a
			// batch against its final combined graph, so a gateway swap spanning two tiers (one drops an
			// edge while the other gains the reverse) can't be false-rejected as a cycle depending on which
			// tier's post happens to land first. Only when step 1 *did* split some changes out early (a
			// pending retire) can a swap spanning the two groups still be false-rejected — an intentionally
			// narrow, retire-only residual of the fuller invariant this batch otherwise preserves.
			// eslint-disable-next-line svelte/prefer-svelte-reactivity -- transient lookup, not held state
			const resolvedEarly = new Set(resolvableNow);
			const lateChanges = allPrerequisiteChanges.filter((change) => !resolvedEarly.has(change));
			if (lateChanges.length) {
				const prerequisiteChanges: ISetProficiencyPrerequisitesData[] = lateChanges.map(
					({ prof, prerequisiteIds }) => ({
						id: resolveId(prof.id, profIdMap),
						prerequisiteIds: prerequisiteIds.map((pid) => resolveId(pid, profIdMap))
					})
				);
				await ApiRequest.post('AdminTools/SetProficiencyPrerequisites', prerequisiteChanges);
				committed = true;
				for (const { prof } of lateChanges) {
					prerequisitesWritten.add(resolveId(prof.id, profIdMap));
				}
			}

			// 9. Follow the selection across any id remap, then re-seed from server truth.
			this.followSelectionRemap(pathIdMap, profIdMap);
			await this.reseed();
			this.#saveFlash.flash();
		} catch (ex) {
			// Once anything has committed, our baseline is behind the server for the records this save's
			// pipeline actually reached — rebase just those against server truth rather than blanket-
			// reseeding both catalogues, so a sibling path/tier this save never touched (or hadn't reached
			// yet) keeps its own pending edit for a clean retry (#2238). A pre-commit failure changed
			// nothing, so the edits are already exactly as the admin left them.
			if (committed) {
				await this.rebaseAfterPartialFailure({
					pathDiff,
					profDiff,
					baseProfMap,
					pathIdMap,
					profIdMap,
					pathIdentityWritten,
					profIdentityWritten,
					modifiersWritten,
					rewardsWritten,
					prerequisitesWritten
				}).catch(() => {});
			}
			toastError(ex instanceof Error ? ex.message : 'Failed to save changes.');
		} finally {
			this.saving = false;
		}
	}

	private async reseed() {
		const [paths, profs] = await Promise.all([fetchSocketData('GetPaths'), fetchSocketData('GetProficiencies')]);
		this.setData(paths, profs);
		this.reconcileSelection();
	}

	private reconcileSelection() {
		if (!this.paths.some((p) => p.id === this.selectedPathId)) {
			this.selectedPathId = this.paths[0]?.id ?? null;
			this.drilledTierId = null;
		}
		const drilled = this.drilledTier;
		if (!drilled || drilled.pathId !== this.selectedPathId) {
			this.drilledTierId = null;
		}
	}

	/** Follow the selection across this save's id remap (a newly-added record now has a persisted id). */
	private followSelectionRemap(pathIdMap: Map<number, number>, profIdMap: Map<number, number>) {
		if (this.selectedPathId != null) {
			this.selectedPathId = resolveId(this.selectedPathId, pathIdMap);
		}
		if (this.drilledTierId != null) {
			this.drilledTierId = resolveId(this.drilledTierId, profIdMap);
		}
	}

	/**
	 * Rebases one catalogue's diff against its fresh server copy: a record this save fully settled
	 * (per `isSettled`) — including every record this save's diff never touched, which has nothing
	 * pending to lose — takes the fresh copy (remapped from a local negative id onto its persisted
	 * one), while an add/edit the failure struck before finishing keeps its current local edit,
	 * remapped onto its persisted id if the identity batch already resolved one. A record only in
	 * `fresh` (a concurrent add from another admin) is appended. Mirrors EntityStore's per-entity
	 * rebase (`rebaseAfterPartialFailure`), generalized over the two catalogues this store manages.
	 */
	private rebaseCatalogue<T extends { id: number }>(
		current: T[],
		diff: { added: T[]; modified: { record: T; baseline: T }[] },
		fresh: T[],
		idMap: Map<number, number>,
		isSettled: (record: T, persistedId: number) => boolean
	): T[] {
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- transient lookup, not held state
		const freshById = new Map(fresh.map((record) => [record.id, record]));
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- transient lookup, not held state
		const pendingIds = new Set([...diff.added.map((r) => r.id), ...diff.modified.map((m) => m.record.id)]);
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- transient lookup, not held state
		const claimedIds = new Set<number>();
		const rebased: T[] = [];

		for (const record of current) {
			const pending = pendingIds.has(record.id);
			const persistedId = record.id < 0 ? idMap.get(record.id) : record.id;
			const settled = !pending || (persistedId !== undefined && isSettled(record, persistedId));

			if (settled) {
				const freshRecord = persistedId !== undefined ? freshById.get(persistedId) : undefined;
				if (freshRecord) {
					rebased.push(clone(freshRecord));
					claimedIds.add(freshRecord.id);
				}
				continue;
			}

			const rebasedRecord =
				persistedId !== undefined && persistedId !== record.id ? { ...clone(record), id: persistedId } : clone(record);
			rebased.push(rebasedRecord);
			if (persistedId !== undefined) {
				claimedIds.add(persistedId);
			}
		}

		for (const record of fresh) {
			if (!claimedIds.has(record.id)) {
				rebased.push(clone(record));
			}
		}

		return rebased;
	}

	/**
	 * A partial-failure recovery pass (#2238): rebases each catalogue's own diff against server truth
	 * instead of blanket-reseeding both, so an edit this save's pipeline never reached survives for a
	 * clean retry. Always re-fetches both catalogues fresh rather than reusing whatever the pipeline
	 * itself already read (which may be exactly the stale/failed read that triggered recovery). Falls
	 * back to a full reseed from that fresh data whenever a rebase can't be trusted — a committed add
	 * whose persisted id this save's own pipeline never resolved (rebasing would keep it pending under
	 * its local id, and a retry would re-Add a duplicate) — and to leaving local state untouched if
	 * even the fresh refetch fails (nothing to rebase against; the original save error is what
	 * surfaces).
	 */
	private async rebaseAfterPartialFailure(recovery: {
		pathDiff: { added: WorkbenchPath[]; modified: { record: WorkbenchPath; baseline: WorkbenchPath }[] };
		profDiff: {
			added: WorkbenchProficiency[];
			modified: { record: WorkbenchProficiency; baseline: WorkbenchProficiency }[];
		};
		baseProfMap: Record<number, WorkbenchProficiency>;
		pathIdMap: Map<number, number>;
		profIdMap: Map<number, number>;
		pathIdentityWritten: boolean;
		profIdentityWritten: boolean;
		modifiersWritten: Set<number>;
		rewardsWritten: Set<number>;
		prerequisitesWritten: Set<number>;
	}) {
		let freshPaths: IPath[];
		let freshProfs: IProficiency[];
		try {
			[freshPaths, freshProfs] = await Promise.all([fetchSocketData('GetPaths'), fetchSocketData('GetProficiencies')]);
		} catch {
			// Nothing to rebase against; leave local state as-is so the original save error is what surfaces.
			return;
		}

		const { pathDiff, profDiff, baseProfMap, pathIdMap, profIdMap, pathIdentityWritten, profIdentityWritten } =
			recovery;
		const { modifiersWritten, rewardsWritten, prerequisitesWritten } = recovery;

		// A rebase remaps every added record through its id map; if the refetch never resolved one, the
		// add's identity write still committed under a persisted id this save never learned, so rebasing
		// would keep it pending under its local id and a retry would re-Add a duplicate.
		const pathAddsResolved = pathDiff.added.every((path) => pathIdMap.has(path.id));
		const profAddsResolved = profDiff.added.every((prof) => profIdMap.has(prof.id));
		if (!pathAddsResolved || !profAddsResolved) {
			this.setData(freshPaths, freshProfs);
			this.reconcileSelection();
			return;
		}

		this.paths = this.rebaseCatalogue(this.paths, pathDiff, freshPaths, pathIdMap, () => pathIdentityWritten);
		this.profs = this.rebaseCatalogue(this.profs, profDiff, freshProfs, profIdMap, (prof, persistedId) => {
			if (!profIdentityWritten) {
				return false;
			}
			const baseline = baseProfMap[prof.id];
			if (childChanged(prof.levelModifiers, baseline?.levelModifiers) && !modifiersWritten.has(persistedId)) {
				return false;
			}
			if (childChanged(prof.levelRewards, baseline?.levelRewards) && !rewardsWritten.has(persistedId)) {
				return false;
			}
			if (childChanged(prof.prerequisiteIds, baseline?.prerequisiteIds) && !prerequisitesWritten.has(persistedId)) {
				return false;
			}
			return true;
		});
		this.basePaths = freshPaths.map(clone);
		this.baseProfs = freshProfs.map(clone);
		staticData.paths = freshPaths;
		staticData.proficiencies = freshProfs;
		this.followSelectionRemap(pathIdMap, profIdMap);
		this.reconcileSelection();
	}

	discard() {
		this.paths = this.basePaths.map(clone);
		this.profs = this.baseProfs.map(clone);
		this.reconcileSelection();
		this.#saveFlash.reset();
	}

	dispose() {
		this.#saveFlash.dispose();
	}
}
