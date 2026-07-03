import { describe, it, expect } from 'vitest';
import { adminGroups, adminTools, DEAD_LETTERS_TOOL_KEY, OPS_GROUP_KEY } from '$routes/admin/workbench/nav';
import { ADMIN_GLYPH_KINDS } from '$routes/admin/AdminGlyph.svelte';

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

	it('gives every tool a glyph AdminGlyph can actually draw', () => {
		// Regression guard for #1502: the Classes tool's 'gauge' glyph rendered a blank sidebar icon.
		const drawable: readonly string[] = ADMIN_GLYPH_KINDS;
		for (const tool of adminTools) {
			expect(drawable, `tool '${tool.key}' has undrawable glyph '${tool.glyph}'`).toContain(tool.glyph);
		}
	});

	it('shows the Classes tool with the gauge glyph', () => {
		expect(adminTools.find((tool) => tool.key === 'classes')?.glyph).toBe('gauge');
	});
});
