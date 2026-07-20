import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EChangeType, EDamageType, EItemCategory, ERarity } from '$lib/api';
import type { FieldsSectionConfig, Warning } from '$routes/admin/workbench/entities/types';

/* Item config transforms specific to the WeaponType field (#1372, relaxed to any leaf by #1456): the
   `newItem` default, the no-stranding validation warning (a weapon needs both a weapon type and a granted
   skill; only a weapon may carry a type), the any-leaf picker, and the persist normalisation of the "None"
   sentinel (-1) ↔ undefined. `fetchSocketData`/`ApiRequest` are stubbed; the real `persistEntity`
   orchestration runs unmocked. */

const { staticData, socket, mockPost, mockFetch } = vi.hoisted(() => {
	const socket = { items: [] as unknown[] };
	return {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData: {} as any,
		socket,
		mockPost: vi.fn(),
		mockFetch: vi.fn(async (command: string) => (command === 'GetItems' ? socket.items : []))
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

import { itemEntity, type WorkbenchItem } from '$routes/admin/workbench/entities/item';
import { reference } from '$routes/admin/workbench/reference.svelte';

/** Finds the body posted to a given AdminTools endpoint (or undefined if never called). */
const postBodyTo = (endpoint: string) => mockPost.mock.calls.find((c) => c[0] === endpoint)?.[1];

/** Runs the identity section's no-stranding warning against a record. */
const identityWarn = (rec: WorkbenchItem): string | Warning | null => {
	const section = itemEntity.sections.find((s) => s.key === 'identity') as FieldsSectionConfig<WorkbenchItem>;
	return section.warn?.(rec) ?? null;
};

beforeEach(() => {
	mockPost.mockReset().mockResolvedValue(undefined);
	mockFetch.mockClear();
	socket.items = [];
	for (const key of Object.keys(staticData)) {
		delete staticData[key];
	}
});

describe('itemEntity weapon type', () => {
	it('newItem defaults to a non-weapon Helm with no weapon type (-1 sentinel)', () => {
		const created = itemEntity.newItem(4);
		expect(created.itemCategoryId).toBe(EItemCategory.Helm);
		expect(created.weaponType).toBe(-1);
		expect(created.grantedSkillId).toBe(-1);
	});

	it('warns a weapon missing a weapon type, blocking Save (backend-enforced)', () => {
		const weapon: WorkbenchItem = { ...itemEntity.newItem(1), itemCategoryId: EItemCategory.Weapon };
		expect(identityWarn(weapon)).toEqual({ message: 'Weapon needs a weapon type', blocking: true });
	});

	it('warns a weapon missing a granted skill, blocking Save (backend-enforced)', () => {
		const weapon: WorkbenchItem = {
			...itemEntity.newItem(1),
			itemCategoryId: EItemCategory.Weapon,
			weaponType: EDamageType.Sword
		};
		expect(identityWarn(weapon)).toEqual({ message: 'Weapon needs a granted skill', blocking: true });
	});

	it('accepts a weapon that declares both a weapon type and a granted skill', () => {
		const weapon: WorkbenchItem = {
			...itemEntity.newItem(1),
			itemCategoryId: EItemCategory.Weapon,
			weaponType: EDamageType.Sword,
			grantedSkillId: 5
		};
		expect(identityWarn(weapon)).toBeNull();
	});

	it('warns a non-weapon that declares a weapon type, blocking Save (backend-enforced)', () => {
		const helm: WorkbenchItem = {
			...itemEntity.newItem(1),
			itemCategoryId: EItemCategory.Helm,
			weaponType: EDamageType.Sword
		};
		expect(identityWarn(helm)).toEqual({ message: 'Only weapons have a weapon type', blocking: true });
	});

	it('accepts a clean non-weapon item', () => {
		expect(identityWarn(itemEntity.newItem(1))).toBeNull();
	});

	it('weaponTypeOptions offers None plus every damage-type leaf, martial or caster', () => {
		const options = reference.weaponTypeOptions();
		expect(options[0]).toEqual({ value: -1, text: 'None' });

		const offered = options.slice(1).map((o) => o.value);
		expect(offered).toContain(EDamageType.Sword);
		expect(offered).toContain(EDamageType.Unarmed);
		// A caster weapon declares its element directly — Physical and the elementals are offered too.
		expect(offered).toContain(EDamageType.Physical);
		expect(offered).toContain(EDamageType.Fire);
	});

	it('persist sends a weapon type through and maps the None sentinel back to undefined', async () => {
		const weapon: WorkbenchItem = {
			...itemEntity.newItem(-1),
			name: 'Iron Sword',
			itemCategoryId: EItemCategory.Weapon,
			rarityId: ERarity.Common,
			iconPath: 'items/sword.png',
			weaponType: EDamageType.Sword,
			grantedSkillId: 3
		};
		socket.items = [{ ...weapon, id: 7 }];

		await itemEntity.persist({ added: [weapon], modified: [], deleted: [], existingIds: [] });

		const addCall = postBodyTo('AdminTools/AddEditItems');
		expect(addCall[0].changeType).toBe(EChangeType.Add);
		// The weapon type rides the identity DTO as its enum value; child collections are stripped.
		expect(addCall[0].item.weaponType).toBe(EDamageType.Sword);
		expect(addCall[0].item.grantedSkillId).toBe(3);
		expect(addCall[0].item.attributes).toEqual([]);
	});

	it('persist maps a non-weapon None weapon type to undefined', async () => {
		const helm: WorkbenchItem = { ...itemEntity.newItem(-1), name: 'Plain Helm' };
		socket.items = [{ ...helm, id: 7 }];

		await itemEntity.persist({ added: [helm], modified: [], deleted: [], existingIds: [] });

		const addCall = postBodyTo('AdminTools/AddEditItems');
		expect(addCall[0].item.weaponType).toBeUndefined();
	});
});
