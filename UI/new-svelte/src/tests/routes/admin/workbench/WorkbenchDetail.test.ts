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
	id: number;
	name: string;
	value: number;
}

const makeConfig = (): EntityConfig<Row> => ({
	key: 'rows',
	label: 'Skills',
	singular: 'Skill',
	glyph: 'box',
	blankName: 'Unnamed',
	newItem: (id) => ({ id, name: '', value: 0 }),
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
	refresh: async () => [],
	persist: async () => []
});

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
