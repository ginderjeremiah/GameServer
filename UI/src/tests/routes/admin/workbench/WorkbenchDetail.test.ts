import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';

vi.mock('$stores', () => ({
	staticData: {
		zones: [],
		enemies: [],
		items: [],
		skills: [],
		itemMods: [],
		attributes: [],
		challenges: [],
		challengeTypes: [],
		statisticTypes: []
	},
	toastError: vi.fn()
}));

import WorkbenchDetail from '$routes/admin/workbench/components/WorkbenchDetail.svelte';
import { EntityStore } from '$routes/admin/workbench/entity-store.svelte';
import type { EntityConfig, Identified } from '$routes/admin/workbench/entities/types';

interface Row extends Identified {
	value: number;
}

const makeConfig = (retireable = false): EntityConfig<Identified> =>
	({
		key: 'rows',
		label: 'Skills',
		singular: 'Skill',
		glyph: 'box',
		blankName: 'Unnamed',
		retireable,
		newItem: (id: number) => ({ id, name: '', value: 0 }),
		meta: () => [],
		sections: [
			{
				key: 'identity',
				label: 'Identity',
				glyph: 'box',
				kind: 'fields',
				fields: [
					{ key: 'name', label: 'Name', type: 'text' },
					{ key: 'value', label: 'Value', type: 'number' }
				]
			}
		],
		listBadge: (rec: Row) => rec.name ?? null,
		refresh: async () => [],
		persist: async () => []
	}) as unknown as EntityConfig<Identified>;

const seed: Row[] = [{ id: 1, name: 'Fireball', value: 50 }];

afterEach(cleanup);

describe('WorkbenchDetail — empty state', () => {
	it('shows the empty state message when no record is provided', () => {
		const store = new EntityStore(makeConfig(), seed);
		render(WorkbenchDetail, {
			props: {
				entity: makeConfig(),
				store,
				record: undefined,
				baseline: undefined,
				tab: 'identity',
				onTab: vi.fn(),
				onNew: vi.fn()
			}
		});
		expect(screen.getByText('No skills left')).toBeTruthy();
	});

	it('shows a "New Skill" button in the empty state', () => {
		const store = new EntityStore(makeConfig(), seed);
		render(WorkbenchDetail, {
			props: {
				entity: makeConfig(),
				store,
				record: undefined,
				baseline: undefined,
				tab: 'identity',
				onTab: vi.fn(),
				onNew: vi.fn()
			}
		});
		expect(screen.getByText(/New Skill/)).toBeTruthy();
	});
});

describe('WorkbenchDetail — with a record', () => {
	const store = () => new EntityStore(makeConfig(), seed);

	const renderDetail = (record: Row) => {
		const s = store();
		const rec = s.items.find((r) => r.id === record.id)!;
		return render(WorkbenchDetail, {
			props: {
				entity: makeConfig(),
				store: s,
				record: rec,
				baseline: s.baselineOf(rec.id),
				tab: 'identity',
				onTab: vi.fn(),
				onNew: vi.fn()
			}
		});
	};

	it('renders the record name as the heading', () => {
		renderDetail(seed[0]);
		expect(screen.getByRole('heading', { level: 2 }).textContent).toBe('Fireball');
	});

	it('renders the record id prefix', () => {
		renderDetail(seed[0]);
		expect(screen.getByText('#1')).toBeTruthy();
	});

	it('renders the section tab button', () => {
		const { container } = renderDetail(seed[0]);
		// There's a tab button AND a section title both labeled "Identity"; use the
		// .tab CSS class to target only the tab button.
		const tab = container.querySelector('.tab') as HTMLElement;
		expect(tab).toBeTruthy();
		expect(tab.textContent?.trim()).toContain('Identity');
	});

	it('Save Changes button is disabled when there are no pending changes', () => {
		renderDetail(seed[0]);
		const saveBtn = screen.getByText('Save Changes') as HTMLButtonElement;
		expect(saveBtn.disabled).toBe(true);
	});

	it('Discard button is disabled when there are no pending changes', () => {
		renderDetail(seed[0]);
		const discardBtn = screen.getByText('Discard') as HTMLButtonElement;
		expect(discardBtn.disabled).toBe(true);
	});

	it('shows the "new" spill badge for a newly added record', () => {
		const s = store();
		const newId = s.addItem();
		const newRecord = s.items.find((r) => r.id === newId)!;
		const { container } = render(WorkbenchDetail, {
			props: {
				entity: makeConfig(),
				store: s,
				record: newRecord,
				baseline: s.baselineOf(newId),
				tab: 'identity',
				onTab: vi.fn(),
				onNew: vi.fn()
			}
		});
		// The "added" spill badge renders as <span class="spill added">new</span>.
		const spillBadge = container.querySelector('.spill.added') as HTMLElement;
		expect(spillBadge).toBeTruthy();
		expect(spillBadge.textContent).toBe('new');
	});

	it('calls store.removeItem when the Delete button is clicked', async () => {
		const s = store();
		const rec = s.items[0];
		render(WorkbenchDetail, {
			props: {
				entity: makeConfig(),
				store: s,
				record: rec,
				baseline: s.baselineOf(rec.id),
				tab: 'identity',
				onTab: vi.fn(),
				onNew: vi.fn()
			}
		});
		await fireEvent.click(screen.getByText('Delete'));
		expect(s.status(rec)).toBe('deleted');
	});
});

describe('WorkbenchDetail — retire lifecycle', () => {
	const renderWith = (s: EntityStore<Identified>, rec: Identified) =>
		render(WorkbenchDetail, {
			props: {
				entity: makeConfig(true),
				store: s,
				record: rec,
				baseline: s.baselineOf(rec.id),
				tab: 'identity',
				onTab: vi.fn(),
				onNew: vi.fn()
			}
		});

	it('offers Retire (not Delete) for a saved active record and retires on click', async () => {
		const s = new EntityStore(makeConfig(true), seed);
		const rec = s.items[0];
		renderWith(s, rec);

		// Reference entities are retired, not deleted.
		expect(screen.queryByText('Delete')).toBeNull();
		await fireEvent.click(screen.getByText('Retire'));

		const updated = s.items.find((r) => r.id === rec.id)!;
		expect(s.isRetired(updated)).toBe(true);
		expect(s.status(updated)).toBe('modified');
	});

	it('shows a retired badge and offers Reinstate for a retired record', async () => {
		const retiredSeed: Row[] = [{ id: 1, name: 'Fireball', value: 50, retiredAt: '2026-01-01T00:00:00Z' }];
		const s = new EntityStore(makeConfig(true), retiredSeed);
		const rec = s.items[0];
		const { container } = renderWith(s, rec);

		expect(container.querySelector('.spill.retired')?.textContent).toBe('retired');
		await fireEvent.click(screen.getByText('Reinstate'));

		expect(s.isRetired(s.items.find((r) => r.id === rec.id)!)).toBe(false);
	});

	it('offers Remove (not Retire) for a never-saved record and drops it on click', async () => {
		const s = new EntityStore(makeConfig(true), seed);
		const newId = s.addItem();
		const rec = s.items.find((r) => r.id === newId)!;
		renderWith(s, rec);

		expect(screen.queryByText('Retire')).toBeNull();
		await fireEvent.click(screen.getByText('Remove'));
		expect(s.items.find((r) => r.id === newId)).toBeUndefined();
	});
});
