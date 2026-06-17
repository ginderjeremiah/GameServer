import { describe, it, expect } from 'vitest';
import { adminGroups, adminTools, DEAD_LETTERS_TOOL_KEY, OPS_GROUP_KEY } from '$routes/admin/workbench/nav';

describe('admin nav config', () => {
	it('exposes the Ops group alongside the workbench groups', () => {
		const ops = adminGroups.find((group) => group.key === OPS_GROUP_KEY);
		expect(ops).toEqual({ key: OPS_GROUP_KEY, label: 'Ops' });
		// The workbench groups are still present.
		expect(adminGroups.some((group) => group.key === 'combat')).toBe(true);
	});

	it('registers a Dead Letters tool in the Ops group', () => {
		const tool = adminTools.find((t) => t.key === DEAD_LETTERS_TOOL_KEY);
		expect(tool).toEqual({
			key: DEAD_LETTERS_TOOL_KEY,
			label: 'Dead Letters',
			group: OPS_GROUP_KEY,
			glyph: 'inbox'
		});
	});

	it('keeps the entity-authoring workbench tools', () => {
		expect(adminTools.some((tool) => tool.key === 'enemies')).toBe(true);
		expect(adminTools.some((tool) => tool.key === 'challenges')).toBe(true);
	});
});
