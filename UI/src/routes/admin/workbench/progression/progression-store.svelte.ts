import { ApiRequest, EChangeType, fetchSocketData, type IChange, type IPath, type IProficiency } from '$lib/api';
import { staticData, toastError } from '$stores';
import { reference } from '../reference.svelte';
import { childChanged, canonicalEqual, resolveId, resolveNewIds } from '../save-helpers';
import { firstFree } from '../entities/helpers';
import {
	blankModifier,
	diffCatalogue,
	newPath,
	newProficiency,
	pathIdentityDto,
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
	saved = $state(false);

	// Selection / navigation.
	selectedPathId = $state<number | null>(null);
	drilledTierId = $state<number | null>(null);
	pathTab = $state<PathTab>('tiers');
	tierTab = $state<TierTab>('milestones');
	selectedLevel = $state(1);

	private nextId = -1;
	#flashTimer: ReturnType<typeof setTimeout> | undefined;

	// ── Loading / seeding ──

	async load() {
		try {
			const [paths, profs] = await Promise.all([fetchSocketData('GetPaths'), fetchSocketData('GetProficiencies')]);
			this.setData(paths, profs);
			this.selectedPathId = this.paths[0]?.id ?? null;
			this.drilledTierId = null;
			this.pathTab = 'tiers';
			this.loaded = true;
		} catch (ex) {
			toastError(ex instanceof Error ? ex.message : 'Failed to load progression data.');
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
	private pathDiff = $derived(diffCatalogue(this.paths, this.basePaths));
	private profDiff = $derived(diffCatalogue(this.profs, this.baseProfs));

	counts = $derived({
		added: this.pathDiff.added.length + this.profDiff.added.length,
		modified: this.pathDiff.modified.length + this.profDiff.modified.length
	});

	get totalChanges(): number {
		return this.counts.added + this.counts.modified;
	}

	pathStatus(path: WorkbenchPath): RecordStatus {
		const baseline = this.basePathMap[path.id];
		if (!baseline) {
			return 'added';
		}
		return canonicalEqual(path, baseline) ? 'clean' : 'modified';
	}

	profStatus(prof: WorkbenchProficiency): RecordStatus {
		const baseline = this.baseProfMap[prof.id];
		if (!baseline) {
			return 'added';
		}
		return canonicalEqual(prof, baseline) ? 'clean' : 'modified';
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

	patchPath(id: number, mutate: (draft: WorkbenchPath) => void) {
		this.paths = this.paths.map((path) => {
			if (path.id !== id) {
				return path;
			}
			const draft = clone(path);
			mutate(draft);
			return draft;
		});
		this.saved = false;
	}

	patchProf(id: number, mutate: (draft: WorkbenchProficiency) => void) {
		this.profs = this.profs.map((prof) => {
			if (prof.id !== id) {
				return prof;
			}
			const draft = clone(prof);
			mutate(draft);
			return draft;
		});
		this.saved = false;
	}

	// ── Add / reorder / retire ──

	addPath() {
		const pathId = this.nextId--;
		const tierId = this.nextId--;
		this.paths = [newPath(pathId), ...this.paths];
		this.profs = [newProficiency(tierId, pathId, 0), ...this.profs];
		this.selectedPathId = pathId;
		this.drilledTierId = null;
		this.pathTab = 'identity';
		this.saved = false;
	}

	addTier(pathId: number): number {
		const tiers = tiersOfPath(this.profs, pathId);
		const ordinal = tiers.length ? Math.max(...tiers.map((t) => t.pathOrdinal)) + 1 : 0;
		const id = this.nextId--;
		this.profs = [newProficiency(id, pathId, ordinal), ...this.profs];
		this.saved = false;
		return id;
	}

	reorderTiers(pathId: number, fromIndex: number, toIndex: number) {
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
		this.saved = false;
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
		this.paths = this.paths.filter((path) => path.id !== id);
		this.profs = this.profs.filter((prof) => prof.pathId !== id);
		if (this.selectedPathId === id) {
			this.selectedPathId = this.paths[0]?.id ?? null;
			this.drilledTierId = null;
		}
		this.saved = false;
	}

	/** Remove a never-saved tier locally; leave the drill view if it was open. */
	removeTier(id: number) {
		this.profs = this.profs.filter((prof) => prof.id !== id);
		if (this.drilledTierId === id) {
			this.drilledTierId = null;
		}
		this.saved = false;
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
		if (this.totalChanges === 0 || this.saving) {
			return;
		}
		this.saving = true;
		let committed = false;
		try {
			const baseProfMap = this.baseProfMap;
			const pathDiff = diffCatalogue(this.paths, this.basePaths);
			const profDiff = diffCatalogue(this.profs, this.baseProfs);

			// 1. Path identities — send an Edit only when the identity DTO itself changed.
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

			// 2. Resolve the persisted ids of newly-added paths before the proficiencies that FK to them.
			const freshPaths = await fetchSocketData('GetPaths');
			const pathIdMap = resolveNewIds(
				freshPaths,
				this.basePaths.map((p) => p.id),
				pathDiff.added
			);

			// 3. Proficiency identities — remap a (possibly brand-new) path id into each DTO.
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

			// 4. Resolve the persisted ids of newly-added proficiencies (for child savers + gateways).
			const freshProfs = await fetchSocketData('GetProficiencies');
			const profIdMap = resolveNewIds(
				freshProfs,
				this.baseProfs.map((p) => p.id),
				profDiff.added
			);

			// 5. Proficiency child collections — modifiers, rewards, and cross-path prerequisites.
			for (const prof of [...profDiff.added, ...profDiff.modified.map((m) => m.record)]) {
				const baseline = baseProfMap[prof.id];
				const id = resolveId(prof.id, profIdMap);
				if (childChanged(prof.levelModifiers, baseline?.levelModifiers)) {
					await ApiRequest.post('AdminTools/SetProficiencyModifiers', { id, modifiers: prof.levelModifiers });
					committed = true;
				}
				if (childChanged(prof.levelRewards, baseline?.levelRewards)) {
					await ApiRequest.post('AdminTools/SetProficiencyRewards', { id, rewards: prof.levelRewards });
					committed = true;
				}
				if (childChanged(prof.prerequisiteIds, baseline?.prerequisiteIds)) {
					const prerequisiteIds = prof.prerequisiteIds.map((pid) => resolveId(pid, profIdMap));
					await ApiRequest.post('AdminTools/SetProficiencyPrerequisites', { id, prerequisiteIds });
					committed = true;
				}
			}

			// 7. Follow the selection across any id remap, then re-seed from server truth.
			if (this.selectedPathId != null) {
				this.selectedPathId = resolveId(this.selectedPathId, pathIdMap);
			}
			if (this.drilledTierId != null) {
				this.drilledTierId = resolveId(this.drilledTierId, profIdMap);
			}
			await this.reseed();
			this.flashSaved();
		} catch (ex) {
			// Once anything has committed, our baseline is behind the server; re-seed so a retry can't
			// re-Add already-persisted records. A pre-commit failure changed nothing, so keep the edits.
			if (committed) {
				await this.reseed().catch(() => {});
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

	discard() {
		this.paths = this.basePaths.map(clone);
		this.profs = this.baseProfs.map(clone);
		this.reconcileSelection();
		this.saved = false;
	}

	private flashSaved() {
		this.saved = true;
		if (this.#flashTimer) {
			clearTimeout(this.#flashTimer);
		}
		this.#flashTimer = setTimeout(() => {
			this.saved = false;
		}, 1900);
	}

	dispose() {
		if (this.#flashTimer) {
			clearTimeout(this.#flashTimer);
		}
	}
}
