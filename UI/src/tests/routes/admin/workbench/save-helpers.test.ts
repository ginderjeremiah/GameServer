import { describe, it, expect, vi } from 'vitest';
import {
	EAttribute,
	EChangeType,
	EModifierType,
	ESkillEffectTarget,
	type IChange,
	type IItemModSlot,
	type ISkillEffect
} from '$lib/api';
import {
	attributeChanges,
	canonicalEqual,
	modSlotChanges,
	persistEntity,
	PersistFailedError,
	skillEffectChanges
} from '../../../../routes/admin/workbench/save-helpers';
import type { Identified, SaveDiff } from '../../../../routes/admin/workbench/entities/types';

describe('canonicalEqual', () => {
	it('is insensitive to object key order', () => {
		expect(canonicalEqual({ a: 1, b: 2 }, { b: 2, a: 1 })).toBe(true);
	});

	it('treats null, undefined, and an absent optional as equal', () => {
		expect(canonicalEqual({ a: 1, b: null }, { a: 1 })).toBe(true);
		expect(canonicalEqual({ a: 1, b: undefined }, { a: 1 })).toBe(true);
		expect(canonicalEqual({ a: 1, b: null }, { a: 1, b: undefined })).toBe(true);
	});

	it('distinguishes a real value (including 0) from null/absent', () => {
		expect(canonicalEqual({ a: 1, b: 0 }, { a: 1 })).toBe(false);
		expect(canonicalEqual({ a: 1, b: '' }, { a: 1 })).toBe(false);
	});

	it('compares nested arrays and objects structurally', () => {
		expect(canonicalEqual({ xs: [{ a: 1, b: 2 }] }, { xs: [{ b: 2, a: 1 }] })).toBe(true);
		expect(canonicalEqual([1, 2], [2, 1])).toBe(false);
	});
});

describe('attributeChanges', () => {
	it('emits Add / Edit / Delete keyed by attributeId', () => {
		const current = [
			{ attributeId: 0, amount: 5 },
			{ attributeId: 1, amount: 3 }
		];
		const baseline = [
			{ attributeId: 0, amount: 2 },
			{ attributeId: 2, amount: 9 }
		];
		const changes = attributeChanges(current, baseline, 'amount');
		expect(changes).toEqual([
			{ changeType: EChangeType.Edit, item: { attributeId: 0, amount: 5 } },
			{ changeType: EChangeType.Add, item: { attributeId: 1, amount: 3 } },
			{ changeType: EChangeType.Delete, item: { attributeId: 2, amount: 9 } }
		]);
	});

	it('treats a missing baseline as all-added and reads the configured value key', () => {
		const changes = attributeChanges([{ attributeId: 4, multiplier: 1.5 }], undefined, 'multiplier');
		expect(changes).toEqual([{ changeType: EChangeType.Add, item: { attributeId: 4, amount: 1.5 } }]);
	});
});

describe('modSlotChanges', () => {
	it('adds new slots, edits changed types, and deletes removed slots', () => {
		const current: IItemModSlot[] = [
			{ id: 1, itemId: 5, itemModSlotTypeId: 2 },
			{ id: 0, itemId: 0, itemModSlotTypeId: 3 }
		];
		const baseline: IItemModSlot[] = [
			{ id: 1, itemId: 5, itemModSlotTypeId: 1 },
			{ id: 2, itemId: 5, itemModSlotTypeId: 1 }
		];
		const changes = modSlotChanges(current, baseline, 5);
		expect(changes).toEqual([
			{ changeType: EChangeType.Edit, item: { id: 1, itemId: 5, itemModSlotTypeId: 2 } },
			{ changeType: EChangeType.Add, item: { id: 0, itemId: 5, itemModSlotTypeId: 3 } },
			{ changeType: EChangeType.Delete, item: { id: 2, itemId: 5, itemModSlotTypeId: 1 } }
		]);
	});

	it('normalises a negative client id to 0 on the Add payload', () => {
		const changes = modSlotChanges([{ id: -2, itemId: 0, itemModSlotTypeId: 3 }], [], 5);
		expect(changes).toEqual([{ changeType: EChangeType.Add, item: { id: 0, itemId: 5, itemModSlotTypeId: 3 } }]);
	});
});

const makeEffect = (id: number, overrides: Partial<ISkillEffect> = {}): ISkillEffect => ({
	id,
	target: ESkillEffectTarget.Opponent,
	attributeId: EAttribute.Strength,
	modifierTypeId: EModifierType.Additive,
	amount: 10,
	durationMs: 3000,
	scalingAttributeId: EAttribute.Strength,
	scalingAmount: 0,
	...overrides
});

describe('skillEffectChanges', () => {
	it('adds new effects (id <= 0), edits changed effects, and deletes removed effects', () => {
		const current: ISkillEffect[] = [
			makeEffect(1, { amount: 15 }),
			makeEffect(0, { attributeId: EAttribute.Toughness })
		];
		const baseline: ISkillEffect[] = [makeEffect(1), makeEffect(2, { attributeId: EAttribute.Intellect })];
		const changes = skillEffectChanges(current, baseline);
		expect(changes).toEqual([
			{ changeType: EChangeType.Edit, item: makeEffect(1, { amount: 15 }) },
			{ changeType: EChangeType.Add, item: makeEffect(0, { attributeId: EAttribute.Toughness }) },
			{ changeType: EChangeType.Delete, item: makeEffect(2, { attributeId: EAttribute.Intellect }) }
		]);
	});

	it('treats a missing baseline as all-added', () => {
		const current: ISkillEffect[] = [makeEffect(0)];
		const changes = skillEffectChanges(current, undefined);
		expect(changes).toEqual([{ changeType: EChangeType.Add, item: makeEffect(0) }]);
	});

	it('normalises a negative client id to 0 on the Add payload', () => {
		const changes = skillEffectChanges([makeEffect(-3, { amount: 7 })], []);
		expect(changes).toEqual([{ changeType: EChangeType.Add, item: makeEffect(0, { amount: 7 }) }]);
	});

	it('emits no changes when current and baseline are identical', () => {
		const effects = [makeEffect(1), makeEffect(2, { amount: 5 })];
		expect(skillEffectChanges(effects, effects)).toEqual([]);
	});

	it('emits an Edit when any field changes', () => {
		expect(skillEffectChanges([makeEffect(1, { target: ESkillEffectTarget.Self })], [makeEffect(1)])).toHaveLength(1);
		expect(
			skillEffectChanges([makeEffect(1, { modifierTypeId: EModifierType.Multiplicative })], [makeEffect(1)])
		).toHaveLength(1);
		expect(skillEffectChanges([makeEffect(1, { durationMs: 5000 })], [makeEffect(1)])).toHaveLength(1);
	});
});

interface Row extends Identified {
	id: number;
	name: string;
}

describe('persistEntity', () => {
	it('posts the primary diff, resolves new ids, and runs child savers against real ids', async () => {
		const diff: SaveDiff<Row> = {
			added: [
				{ id: -1, name: 'A' },
				{ id: -2, name: 'B' }
			],
			modified: [{ record: { id: 0, name: 'X' }, baseline: { id: 0, name: 'x' } }],
			deleted: [{ id: 1, name: 'D' }],
			existingIds: [0, 1]
		};

		// After the primary save, record 1 is gone and the two adds receive ids 2 and 3.
		const fresh: Row[] = [
			{ id: 0, name: 'X' },
			{ id: 2, name: 'A' },
			{ id: 3, name: 'B' }
		];

		let primaryChanges: IChange<Row>[] = [];
		const postPrimary = vi.fn(async (changes: IChange<Row>[]) => {
			primaryChanges = changes;
		});
		const refresh = vi.fn(async () => fresh);
		const childCalls: { id: number; name: string; hasBaseline: boolean }[] = [];

		const result = await persistEntity<Row, Row>({
			diff,
			toPrimaryDto: (record) => ({ id: record.id, name: record.name }),
			postPrimary,
			refresh,
			childSavers: [
				async (id, record, baseline) => {
					childCalls.push({ id, name: record.name, hasBaseline: baseline !== undefined });
				}
			]
		});

		// Primary call carries the full diff in Add → Edit → Delete order.
		expect(postPrimary).toHaveBeenCalledOnce();
		expect(primaryChanges.map((c) => c.changeType)).toEqual([
			EChangeType.Add,
			EChangeType.Add,
			EChangeType.Edit,
			EChangeType.Delete
		]);

		// Added records map positionally to the lowest new ids; modified keeps its id.
		expect(childCalls).toEqual([
			{ id: 2, name: 'A', hasBaseline: false },
			{ id: 3, name: 'B', hasBaseline: false },
			{ id: 0, name: 'X', hasBaseline: true }
		]);

		// refresh runs after the primary save and again after children.
		expect(refresh).toHaveBeenCalledTimes(2);
		expect(result).toEqual(fresh);
	});

	it('skips the primary call when nothing is added, edited, or deleted', async () => {
		const refresh = vi.fn(async () => [] as Row[]);
		const postPrimary = vi.fn(async () => undefined);
		await persistEntity<Row, Row>({
			diff: { added: [], modified: [], deleted: [], existingIds: [] },
			toPrimaryDto: (r) => r,
			postPrimary,
			refresh
		});
		expect(postPrimary).not.toHaveBeenCalled();
	});

	it('does not send an identity Edit when only a child collection changed', async () => {
		interface ChildRow extends Identified {
			id: number;
			name: string;
			kids: number[];
		}
		// Identity (id + name) is unchanged; only the child collection (`kids`, which
		// toPrimaryDto strips) differs, so this record must persist through its child
		// saver alone — the Add/Edit endpoint should not be touched.
		const diff: SaveDiff<ChildRow> = {
			added: [],
			modified: [{ record: { id: 0, name: 'X', kids: [1, 2] }, baseline: { id: 0, name: 'X', kids: [1] } }],
			deleted: [],
			existingIds: [0]
		};
		const postPrimary = vi.fn(async () => undefined);
		const refresh = vi.fn(async () => [{ id: 0, name: 'X', kids: [1, 2] }] as ChildRow[]);
		const childIds: number[] = [];

		await persistEntity<ChildRow, { id: number; name: string }>({
			diff,
			toPrimaryDto: (r) => ({ id: r.id, name: r.name }),
			postPrimary,
			refresh,
			childSavers: [
				async (id) => {
					childIds.push(id);
				}
			]
		});

		expect(postPrimary).not.toHaveBeenCalled();
		expect(childIds).toEqual([0]);
	});

	it('propagates a pre-commit (postPrimary) failure raw, not as PersistFailedError', async () => {
		const diff: SaveDiff<Row> = { added: [{ id: -1, name: 'A' }], modified: [], deleted: [], existingIds: [] };
		const cause = new Error('primary batch 500');
		await expect(
			persistEntity<Row, Row>({
				diff,
				toPrimaryDto: (r) => r,
				postPrimary: async () => {
					throw cause;
				},
				refresh: async () => []
			})
		).rejects.toBe(cause);
	});

	it('wraps a post-commit (child saver) failure as PersistFailedError', async () => {
		const diff: SaveDiff<Row> = { added: [{ id: -1, name: 'A' }], modified: [], deleted: [], existingIds: [] };
		await expect(
			persistEntity<Row, Row>({
				diff,
				toPrimaryDto: (r) => r,
				postPrimary: async () => undefined,
				refresh: async () => [{ id: 1, name: 'A' }],
				childSavers: [
					async () => {
						throw new Error('child saver 500');
					}
				]
			})
		).rejects.toBeInstanceOf(PersistFailedError);
	});

	it('wraps a post-commit refresh failure as PersistFailedError', async () => {
		const diff: SaveDiff<Row> = { added: [{ id: -1, name: 'A' }], modified: [], deleted: [], existingIds: [] };
		await expect(
			persistEntity<Row, Row>({
				diff,
				toPrimaryDto: (r) => r,
				postPrimary: async () => undefined,
				refresh: async () => {
					throw new Error('refresh 500');
				}
			})
		).rejects.toBeInstanceOf(PersistFailedError);
	});
});
