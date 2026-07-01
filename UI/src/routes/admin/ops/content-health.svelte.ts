import { ApiRequest, EContentHealthSeverity, type IContentHealthFinding, type IContentHealthReport } from '$lib/api';

/** Semantic tone a severity badge/styling keys off (never a hard-coded colour). */
export type SeverityTone = 'error' | 'warning';

export interface SeverityMeta {
	label: string;
	tone: SeverityTone;
}

/** Display metadata for a finding severity. */
export const severityMeta = (severity: EContentHealthSeverity): SeverityMeta =>
	severity === EContentHealthSeverity.Error ? { label: 'Error', tone: 'error' } : { label: 'Warning', tone: 'warning' };

/**
 * The zero-based-id reference sets a finding's entity name is resolved from. A subset of the cached
 * admin reference data ({@link staticData}); only the kinds the lint can anchor to are needed, and the
 * kinds it can't resolve (Class / SkillRecipe — not held in the admin reference cache) fall back to the id.
 */
export interface EntityNameSources {
	zones?: { name: string }[];
	challenges?: { name: string }[];
	enemies?: { name: string }[];
	items?: { name: string }[];
	skills?: { name: string }[];
	proficiencies?: { name: string }[];
}

/**
 * Maps a finding's {@link IContentHealthFinding.entityKind} to the reference set that names it. Covers the
 * kinds the checker both emits and the admin caches hold; the other two kinds it emits (Class / SkillRecipe)
 * aren't in the admin reference cache, so they fall through to the `#id` fallback in {@link entityDisplayName}.
 */
const sourceForKind = (kind: string, sources: EntityNameSources): { name: string }[] | undefined => {
	switch (kind) {
		case 'Zone':
			return sources.zones;
		case 'Challenge':
			return sources.challenges;
		case 'Enemy':
			return sources.enemies;
		case 'Item':
			return sources.items;
		case 'Skill':
			return sources.skills;
		case 'Proficiency':
			return sources.proficiencies;
		default:
			return undefined;
	}
};

/**
 * Resolves a finding's offending record to a display name from the cached reference sets, honouring the
 * zero-based Id-as-index invariant. Falls back to `#id` when the kind isn't cached or the record is absent
 * (e.g. a dangling id) — the finding still reads clearly by kind + id + message.
 */
export const entityDisplayName = (kind: string, id: number, sources: EntityNameSources): string =>
	sourceForKind(kind, sources)?.[id]?.name ?? `#${id}`;

/** A group of findings sharing a check category, for a scannable view. */
export interface FindingGroup {
	check: string;
	/** True when any finding in the group is an error — errors sort first and accent the group. */
	hasError: boolean;
	findings: IContentHealthFinding[];
}

/**
 * Groups findings by their check category, ordering errored groups first and then by check name, so the
 * genuine breakage sits at the top. Preserves the backend's within-check ordering (by entity).
 */
export const groupFindings = (findings: IContentHealthFinding[]): FindingGroup[] => {
	const groups: FindingGroup[] = [];
	for (const finding of findings) {
		const group = groups.find((g) => g.check === finding.check);
		if (group) {
			group.findings.push(finding);
			group.hasError ||= finding.severity === EContentHealthSeverity.Error;
		} else {
			groups.push({
				check: finding.check,
				hasError: finding.severity === EContentHealthSeverity.Error,
				findings: [finding]
			});
		}
	}

	return groups.sort((a, b) => Number(b.hasError) - Number(a.hasError) || a.check.localeCompare(b.check));
};

/**
 * View-model for the admin Content Health console: fetches the whole-graph lint report over the live
 * reference caches and exposes it read-only. Purely diagnostic — it never mutates content.
 */
export class ContentHealthState {
	report = $state<IContentHealthReport | null>(null);
	loading = $state(false);
	loaded = $state(false);
	error = $state<string | null>(null);

	get findings(): IContentHealthFinding[] {
		return this.report?.findings ?? [];
	}

	get errorCount(): number {
		return this.report?.errorCount ?? 0;
	}

	get warningCount(): number {
		return this.report?.warningCount ?? 0;
	}

	get isHealthy(): boolean {
		return this.loaded && this.findings.length === 0;
	}

	get groups(): FindingGroup[] {
		return groupFindings(this.findings);
	}

	/** Runs the lint over the current caches and replaces the report. */
	async load(): Promise<boolean> {
		this.loading = true;
		this.error = null;
		try {
			this.report = await ApiRequest.get('AdminTools/GetContentHealth');
			this.loaded = true;
			return true;
		} catch (ex) {
			this.error = ex instanceof Error ? ex.message : 'Failed to load the content health report.';
			return false;
		} finally {
			this.loading = false;
		}
	}
}
