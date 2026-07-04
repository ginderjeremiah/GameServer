import type { AdminGlyphKind } from '../AdminGlyph.svelte';
import { workbenchEntities, workbenchGroups } from './entities';

export interface AdminGroupDef {
	key: string;
	label: string;
}

export interface AdminToolDef {
	key: string;
	label: string;
	group: string;
	glyph: AdminGlyphKind;
}

/** Nav keys for the operations group and its ops tools (not entity Workbench surfaces). */
export const OPS_GROUP_KEY = 'ops';
export const DEAD_LETTERS_TOOL_KEY = 'dead-letters';
export const SOCKET_DEAD_LETTERS_TOOL_KEY = 'socket-dead-letters';
export const CONTENT_HEALTH_TOOL_KEY = 'content-health';

/**
 * Nav key for the bespoke Paths/Proficiencies progression editor. Like the Ops tools it renders its
 * own view (a two-level path → tier drill-down) instead of an EntityConfig-driven Workbench panel,
 * but it lives in the Progression group alongside the entity-authored surfaces.
 */
export const PROGRESSION_TOOL_KEY = 'paths';

/** Workbench nav groups mirror the entity groups (Combat / Items / World / Progression). */
const workbenchGroupDefs: AdminGroupDef[] = workbenchGroups.map((group) => ({ key: group.key, label: group.label }));

/** One sidebar item per workbench entity, placed in its group. */
const workbenchToolDefs: AdminToolDef[] = workbenchGroups.flatMap((group) =>
	group.entityKeys
		.map((entityKey) => workbenchEntities.find((entity) => entity.key === entityKey))
		.filter((entity) => !!entity)
		.map((entity) => ({
			key: entity.key,
			label: entity.label,
			group: group.key,
			glyph: entity.glyph
		}))
);

/** Bespoke (non-entity) Workbench surfaces that still live inside a Workbench group. */
const progressionToolDefs: AdminToolDef[] = [
	{ key: PROGRESSION_TOOL_KEY, label: 'Paths', group: 'progression', glyph: 'rune' }
];

/**
 * Operations/diagnostics tools — a different kind of surface from the entity-authoring Workbench, so
 * they live in their own group and render their own views rather than an EntityConfig-driven panel.
 */
const opsGroupDefs: AdminGroupDef[] = [{ key: OPS_GROUP_KEY, label: 'Ops' }];
const opsToolDefs: AdminToolDef[] = [
	{ key: DEAD_LETTERS_TOOL_KEY, label: 'Dead Letters', group: OPS_GROUP_KEY, glyph: 'inbox' },
	{ key: SOCKET_DEAD_LETTERS_TOOL_KEY, label: 'Socket Dead Letters', group: OPS_GROUP_KEY, glyph: 'bolt' },
	{ key: CONTENT_HEALTH_TOOL_KEY, label: 'Content Health', group: OPS_GROUP_KEY, glyph: 'map' }
];

export const adminGroups: AdminGroupDef[] = [...workbenchGroupDefs, ...opsGroupDefs];
export const adminTools: AdminToolDef[] = [...workbenchToolDefs, ...progressionToolDefs, ...opsToolDefs];
