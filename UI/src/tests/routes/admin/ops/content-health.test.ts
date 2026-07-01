import { describe, it, expect, beforeEach, vi } from 'vitest';
import type { IContentHealthFinding, IContentHealthReport } from '$lib/api';

const { getMock } = vi.hoisted(() => ({ getMock: vi.fn() }));

// Keep the real EContentHealthSeverity enum (a pure types module) and only swap the HTTP transport.
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, ApiRequest: { get: getMock } };
});

import { EContentHealthSeverity } from '$lib/api';
import {
	ContentHealthState,
	entityDisplayName,
	groupFindings,
	severityMeta
} from '$routes/admin/ops/content-health.svelte';

const finding = (overrides: Partial<IContentHealthFinding> = {}): IContentHealthFinding => ({
	severity: EContentHealthSeverity.Warning,
	check: 'OrphanSkill',
	entityKind: 'Skill',
	entityId: 3,
	message: 'Orphan skill.',
	...overrides
});

const report = (findings: IContentHealthFinding[]): IContentHealthReport => ({
	errorCount: findings.filter((f) => f.severity === EContentHealthSeverity.Error).length,
	warningCount: findings.filter((f) => f.severity === EContentHealthSeverity.Warning).length,
	findings
});

beforeEach(() => {
	getMock.mockReset();
});

describe('severityMeta', () => {
	it('maps Error to the error tone', () => {
		expect(severityMeta(EContentHealthSeverity.Error)).toEqual({ label: 'Error', tone: 'error' });
	});

	it('maps Warning to the warning tone', () => {
		expect(severityMeta(EContentHealthSeverity.Warning)).toEqual({ label: 'Warning', tone: 'warning' });
	});
});

describe('entityDisplayName', () => {
	const sources = {
		zones: [{ name: 'Start' }, { name: 'Cave' }],
		skills: [{ name: 'Punch' }, { name: 'Slash' }]
	};

	it('resolves a name by zero-based id from the matching set', () => {
		expect(entityDisplayName('Zone', 1, sources)).toBe('Cave');
		expect(entityDisplayName('Skill', 0, sources)).toBe('Punch');
	});

	it('falls back to #id when the record is absent (dangling id)', () => {
		expect(entityDisplayName('Zone', 9, sources)).toBe('#9');
	});

	it('falls back to #id for a kind not held in the reference cache', () => {
		expect(entityDisplayName('SkillRecipe', 2, sources)).toBe('#2');
		expect(entityDisplayName('Class', 0, sources)).toBe('#0');
	});
});

describe('groupFindings', () => {
	it('groups by check and orders errored groups first, then alphabetically', () => {
		const groups = groupFindings([
			finding({ check: 'OrphanSkill', severity: EContentHealthSeverity.Warning }),
			finding({ check: 'ZoneBoss', severity: EContentHealthSeverity.Error, entityKind: 'Zone', entityId: 1 }),
			finding({
				check: 'ChallengeReward',
				severity: EContentHealthSeverity.Error,
				entityKind: 'Challenge',
				entityId: 2
			})
		]);

		expect(groups.map((g) => g.check)).toEqual(['ChallengeReward', 'ZoneBoss', 'OrphanSkill']);
		expect(groups[0].hasError).toBe(true);
		expect(groups[2].hasError).toBe(false);
	});

	it('collects every finding of a check into one group, preserving order', () => {
		const groups = groupFindings([
			finding({ check: 'OrphanSkill', entityId: 3 }),
			finding({ check: 'OrphanSkill', entityId: 5 })
		]);

		expect(groups).toHaveLength(1);
		expect(groups[0].findings.map((f) => f.entityId)).toEqual([3, 5]);
	});
});

describe('ContentHealthState', () => {
	it('loads the report and exposes counts, findings, and groups', async () => {
		const findings = [
			finding({ check: 'ZoneBoss', severity: EContentHealthSeverity.Error }),
			finding({ check: 'OrphanSkill', severity: EContentHealthSeverity.Warning })
		];
		getMock.mockResolvedValue(report(findings));

		const state = new ContentHealthState();
		const ok = await state.load();

		expect(ok).toBe(true);
		expect(getMock).toHaveBeenCalledWith('AdminTools/GetContentHealth');
		expect(state.loaded).toBe(true);
		expect(state.errorCount).toBe(1);
		expect(state.warningCount).toBe(1);
		expect(state.findings).toHaveLength(2);
		expect(state.groups).toHaveLength(2);
		expect(state.isHealthy).toBe(false);
	});

	it('reports a healthy graph when there are no findings', async () => {
		getMock.mockResolvedValue(report([]));

		const state = new ContentHealthState();
		await state.load();

		expect(state.isHealthy).toBe(true);
		expect(state.errorCount).toBe(0);
		expect(state.warningCount).toBe(0);
	});

	it('captures the error message on a failed load', async () => {
		getMock.mockRejectedValue(new Error('boom'));

		const state = new ContentHealthState();
		const ok = await state.load();

		expect(ok).toBe(false);
		expect(state.error).toBe('boom');
		expect(state.loaded).toBe(false);
		expect(state.isHealthy).toBe(false);
	});
});
