import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EItemModType, ERarity, type IItemMod } from '$lib/api';

/* Item-mod config transforms: `newItem` defaults, the derived meta line, and the
   persist path — a child-only attribute edit must NOT hit the identity Add/Edit
   endpoint, and an untouched tag collection is skipped. `fetchSocketData`/
   `ApiRequest` are stubbed; the real `persistEntity` orchestration runs unmocked. */

const { staticData, socket, mockPost, mockFetch } = vi.hoisted(() => {
	const socket = { itemMods: [] as unknown[] };
	return {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData: {} as any,
		socket,
		mockPost: vi.fn(),
		mockFetch: vi.fn(async (command: string) => (command === 'GetItemMods' ? socket.itemMods : []))
	};
});

vi.mock('$stores', () => ({ staticData }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	class ApiRequest {
		static post = mockPost;
		static get = vi.fn();
	}
	return { ...actual, ApiRequest, fetchSocketData: mockFetch };
});

import { itemModEntity } from '$routes/admin/workbench/entities/item-mod';

/** Finds the body posted to a given AdminTools endpoint (or undefined if never called). */
const postBodyTo = (endpoint: string) => mockPost.mock.calls.find((c) => c[0] === endpoint)?.[1];

beforeEach(() => {
	mockPost.mockReset().mockResolvedValue(undefined);
	mockFetch.mockClear();
	socket.itemMods = [];
	for (const key of Object.keys(staticData)) {
		delete staticData[key];
	}
});

describe('itemModEntity', () => {
	it('newItem defaults to a Common Component with empty collections', () => {
		expect(itemModEntity.newItem(4)).toEqual({
			id: 4,
			name: '',
			description: '',
			itemModTypeId: EItemModType.Component,
			rarityId: ERarity.Common,
			attributes: [],
			tags: []
		});
	});

	it('meta shows the mod type, attribute and tag counts', () => {
		const mod: IItemMod = {
			...itemModEntity.newItem(1),
			itemModTypeId: EItemModType.Prefix,
			attributes: [{ attributeId: 0, amount: 1 }],
			tags: [1, 2]
		};
		expect(itemModEntity.meta(mod)).toEqual([
			['', 'Prefix'],
			['attr', 1],
			['tag', 2]
		]);
	});

	it('persist saves the tag set when tags change without re-sending an identity Edit', async () => {
		const baseline: IItemMod = {
			id: 0,
			name: 'Sharp',
			description: 'desc',
			itemModTypeId: EItemModType.Prefix,
			rarityId: ERarity.Rare,
			attributes: [{ attributeId: 0, amount: 1 }],
			tags: [1]
		};
		const record: IItemMod = { ...baseline, tags: [1, 2] }; // only the tag set changed
		socket.itemMods = [record];

		await itemModEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		// Identity + attributes are unchanged → only the tag endpoint is hit.
		expect(postBodyTo('AdminTools/AddEditItemMods')).toBeUndefined();
		expect(postBodyTo('AdminTools/AddEditItemModAttributes')).toBeUndefined();
		expect(postBodyTo('AdminTools/SetTagsForItemMod')).toEqual({ id: 0, tagIds: [1, 2] });
	});

	it('persist diffs attribute changes without sending an identity Edit when identity is unchanged', async () => {
		const baseline: IItemMod = {
			id: 0,
			name: 'Sharp',
			description: '',
			itemModTypeId: EItemModType.Prefix,
			rarityId: ERarity.Rare,
			attributes: [{ attributeId: 0, amount: 1 }],
			tags: [1]
		};
		const record: IItemMod = { ...baseline, attributes: [{ attributeId: 0, amount: 3 }] }; // only the bonus amount changed
		socket.itemMods = [record];

		await itemModEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		// Identity is identical once attributes/tags are stripped → no Add/Edit call.
		expect(postBodyTo('AdminTools/AddEditItemMods')).toBeUndefined();
		// The attribute diff is sent as an Edit of the single changed bonus.
		expect(postBodyTo('AdminTools/AddEditItemModAttributes')).toMatchObject({
			id: 0,
			changes: [{ item: { attributeId: 0, amount: 3 } }]
		});
		// Tags were untouched, so their endpoint is skipped.
		expect(postBodyTo('AdminTools/SetTagsForItemMod')).toBeUndefined();
	});
});
