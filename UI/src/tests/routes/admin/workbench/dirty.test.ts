import { describe, it, expect, beforeEach } from 'vitest';
import { workbenchDirty } from '$routes/admin/workbench/dirty.svelte';

beforeEach(() => workbenchDirty.set(0));

describe('workbenchDirty', () => {
	it('starts at zero', () => {
		expect(workbenchDirty.total).toBe(0);
	});

	it('reports the value it was last set to', () => {
		workbenchDirty.set(3);
		expect(workbenchDirty.total).toBe(3);

		workbenchDirty.set(0);
		expect(workbenchDirty.total).toBe(0);
	});
});
