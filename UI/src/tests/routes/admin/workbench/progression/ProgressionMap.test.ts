import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import ProgressionMap from '$routes/admin/workbench/progression/ProgressionMap.svelte';
import type { ProgressionStore } from '$routes/admin/workbench/progression/progression-store.svelte';
import type { WorkbenchPath, WorkbenchProficiency } from '$routes/admin/workbench/progression/types';

const path = (id: number, over: Partial<WorkbenchPath> = {}): WorkbenchPath => ({
	id,
	name: `Path ${id}`,
	description: '',
	falloffBase: 0.6,
	contributions: [],
	...over
});

const tier = (
	id: number,
	pathId: number,
	ordinal: number,
	over: Partial<WorkbenchProficiency> = {}
): WorkbenchProficiency => ({
	id,
	name: `Tier ${id}`,
	description: '',
	iconPath: '',
	word: '',
	pronunciation: '',
	translation: '',
	pathId,
	pathOrdinal: ordinal,
	maxLevel: 10,
	baseXp: 100,
	xpGrowth: 1.4,
	levelModifiers: [],
	levelRewards: [],
	...over
});

const makeStore = (paths: WorkbenchPath[], profs: WorkbenchProficiency[]) =>
	({ paths, profs, selectPath: vi.fn(), drillTier: vi.fn() }) as unknown as ProgressionStore & {
		selectPath: ReturnType<typeof vi.fn>;
		drillTier: ReturnType<typeof vi.fn>;
	};

const renderMap = (store: ReturnType<typeof makeStore>) => {
	const onNavigate = vi.fn();
	const result = render(ProgressionMap, { props: { store, onNavigate } });
	return { ...result, onNavigate };
};

afterEach(cleanup);

describe('ProgressionMap', () => {
	it('renders a column per path and a node per tier', () => {
		const store = makeStore([path(0), path(1)], [tier(10, 0, 0), tier(11, 0, 1), tier(20, 1, 0)]);
		const { container } = renderMap(store);
		expect(container.querySelectorAll('.col')).toHaveLength(2);
		expect(container.querySelectorAll('.node')).toHaveLength(3);
	});

	it('tags each node with its measurable data-node id and a within-path connector', () => {
		const store = makeStore([path(0)], [tier(10, 0, 0), tier(11, 0, 1)]);
		const { container } = renderMap(store);
		expect(container.querySelector('[data-node="t10"]')).toBeTruthy();
		expect(container.querySelector('[data-node="t11"]')).toBeTruthy();
		// One connector between the two tiers (none above the first).
		expect(container.querySelectorAll('.connector')).toHaveLength(1);
	});

	it('drills into the tier in the List editor when a node is clicked', async () => {
		const store = makeStore([path(0)], [tier(10, 0, 0)]);
		const { container, onNavigate } = renderMap(store);
		await fireEvent.click(container.querySelector('[data-node="t10"]') as HTMLElement);
		expect(store.selectPath).toHaveBeenCalledWith(0);
		expect(store.drillTier).toHaveBeenCalledWith(10);
		expect(onNavigate).toHaveBeenCalledOnce();
	});

	it('opens the path (without drilling) when a column header is clicked', async () => {
		const store = makeStore([path(0)], [tier(10, 0, 0)]);
		const { container, onNavigate } = renderMap(store);
		await fireEvent.click(container.querySelector('.col-head') as HTMLElement);
		expect(store.selectPath).toHaveBeenCalledWith(0);
		expect(store.drillTier).not.toHaveBeenCalled();
		expect(onNavigate).toHaveBeenCalledOnce();
	});

	it('renders each node as a button', () => {
		const store = makeStore([path(0)], [tier(10, 0, 0)]);
		const { container } = renderMap(store);
		expect((container.querySelector('.node') as HTMLElement).tagName).toBe('BUTTON');
	});

	it('shows the empty state when there are no paths', () => {
		const { container } = renderMap(makeStore([], []));
		expect(container.querySelector('.map-empty')).toBeTruthy();
		expect(container.querySelectorAll('.col')).toHaveLength(0);
	});

	it('shows a per-column empty state for a path with no tiers', () => {
		const { container } = renderMap(makeStore([path(0)], []));
		expect(container.querySelector('.col-empty')).toBeTruthy();
		expect(container.querySelectorAll('.node')).toHaveLength(0);
	});
});
