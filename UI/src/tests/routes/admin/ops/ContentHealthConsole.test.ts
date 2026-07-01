import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent, waitFor } from '@testing-library/svelte';
import type { IContentHealthFinding, IContentHealthReport } from '$lib/api';

const { getMock, toastErrorMock } = vi.hoisted(() => ({ getMock: vi.fn(), toastErrorMock: vi.fn() }));

vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, ApiRequest: { get: getMock } };
});
vi.mock('$stores', () => ({
	toastError: toastErrorMock,
	// The view resolves finding names off the admin reference caches; a fixed snapshot is enough here.
	staticData: {
		zones: [{ name: 'Start Zone' }, { name: 'Cave' }],
		challenges: [{ name: 'Clear the Cave' }],
		enemies: [],
		items: [],
		skills: [{ name: 'Punch' }],
		proficiencies: []
	}
}));

import ContentHealthConsole from '$routes/admin/ops/ContentHealthConsole.svelte';
import { EContentHealthSeverity } from '$lib/api';

const finding = (overrides: Partial<IContentHealthFinding> = {}): IContentHealthFinding => ({
	severity: EContentHealthSeverity.Warning,
	check: 'OrphanSkill',
	entityKind: 'Skill',
	entityId: 0,
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
	toastErrorMock.mockReset();
});

afterEach(cleanup);

describe('ContentHealthConsole', () => {
	it('loads on mount and shows the healthy empty state when there are no findings', async () => {
		getMock.mockResolvedValue(report([]));
		render(ContentHealthConsole);

		await waitFor(() => expect(screen.getByTestId('ch-healthy')).toBeTruthy());
		expect(getMock).toHaveBeenCalledWith('AdminTools/GetContentHealth');
		expect(screen.queryByTestId('ch-group')).toBeNull();
		expect(screen.getByTestId('ch-summary').textContent).toContain('graph healthy');
	});

	it('renders counts, grouped findings, severity badges, and resolved entity names', async () => {
		getMock.mockResolvedValue(
			report([
				finding({ severity: EContentHealthSeverity.Error, check: 'ZoneBoss', entityKind: 'Zone', entityId: 1 }),
				finding({ severity: EContentHealthSeverity.Warning, check: 'OrphanSkill', entityKind: 'Skill', entityId: 0 }),
				finding({
					severity: EContentHealthSeverity.Warning,
					check: 'ClassStarterSkill',
					entityKind: 'Class',
					entityId: 5
				})
			])
		);
		render(ContentHealthConsole);

		await waitFor(() => expect(screen.getAllByTestId('ch-finding').length).toBe(3));
		// Singular/plural counts.
		expect(screen.getByTestId('ch-error-count').textContent).toBe('1 error');
		expect(screen.getByTestId('ch-warning-count').textContent).toBe('2 warnings');
		// One group per check.
		expect(screen.getAllByTestId('ch-group').length).toBe(3);
		// The errored group sorts first, so its severity badge leads.
		expect(screen.getAllByTestId('ch-severity')[0].textContent).toBe('Error');
		// Name resolution wires nameSources through: cached kinds resolve to their record name
		// (the #id fallback for uncached kinds is covered in the logic-module test).
		expect(screen.getByText('Cave')).toBeTruthy();
		expect(screen.getByText('Punch')).toBeTruthy();
	});

	it('toasts the error and shows the error panel when the load fails', async () => {
		getMock.mockRejectedValue(new Error('lint unavailable'));
		render(ContentHealthConsole);

		await waitFor(() => expect(toastErrorMock).toHaveBeenCalledWith('lint unavailable'));
		expect(screen.getByTestId('ch-error').textContent).toBe('lint unavailable');
		// The healthy state must not show on a failed load — the graph state is unknown, not clean.
		expect(screen.queryByTestId('ch-healthy')).toBeNull();
	});

	it('re-runs the lint when Refresh is clicked', async () => {
		getMock.mockResolvedValue(report([]));
		render(ContentHealthConsole);

		await waitFor(() => expect(screen.getByTestId('ch-healthy')).toBeTruthy());
		await fireEvent.click(screen.getByTestId('ch-refresh'));

		await waitFor(() => expect(getMock).toHaveBeenCalledTimes(2));
	});
});
