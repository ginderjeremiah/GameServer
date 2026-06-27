import { describe, it, expect } from 'vitest';
import { mapColumns, tierNodeId } from '$routes/admin/workbench/progression/progression-map';
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

describe('tierNodeId', () => {
	it('prefixes the tier id with t', () => {
		expect(tierNodeId(7)).toBe('t7');
		expect(tierNodeId(0)).toBe('t0');
	});
});

describe('mapColumns', () => {
	it('builds one column per path, in path order', () => {
		const paths = [path(0), path(1)];
		const profs = [tier(10, 0, 0), tier(11, 1, 0)];
		const cols = mapColumns(paths, profs);
		expect(cols.map((c) => c.id)).toEqual([0, 1]);
		expect(cols[0].name).toBe('Path 0');
	});

	it('orders a path’s nodes ascending by ordinal regardless of input order', () => {
		const profs = [tier(12, 0, 2), tier(10, 0, 0), tier(11, 0, 1)];
		const [col] = mapColumns([path(0)], profs);
		expect(col.nodes.map((n) => n.id)).toEqual([10, 11, 12]);
		expect(col.nodes.map((n) => n.ordinal)).toEqual([0, 1, 2]);
	});

	it('only includes a path’s own tiers in its column', () => {
		const profs = [tier(10, 0, 0), tier(20, 1, 0)];
		const cols = mapColumns([path(0), path(1)], profs);
		expect(cols[0].nodes.map((n) => n.id)).toEqual([10]);
		expect(cols[1].nodes.map((n) => n.id)).toEqual([20]);
	});

	it('propagates retired state from the record retiredAt', () => {
		const cols = mapColumns(
			[path(0, { retiredAt: '2026-01-01T00:00:00Z' })],
			[tier(10, 0, 0, { retiredAt: '2026-01-01T00:00:00Z' }), tier(11, 0, 1)]
		);
		expect(cols[0].retired).toBe(true);
		expect(cols[0].nodes[0].retired).toBe(true);
		expect(cols[0].nodes[1].retired).toBe(false);
	});

	it('carries the node display fields (nodeId, name, cap)', () => {
		const [col] = mapColumns([path(0)], [tier(10, 0, 0, { name: 'Fire', maxLevel: 8 })]);
		expect(col.nodes[0]).toMatchObject({ nodeId: 't10', name: 'Fire', maxLevel: 8 });
	});

	it('yields an empty node list for a path with no tiers', () => {
		const [col] = mapColumns([path(0)], []);
		expect(col.nodes).toEqual([]);
	});
});
