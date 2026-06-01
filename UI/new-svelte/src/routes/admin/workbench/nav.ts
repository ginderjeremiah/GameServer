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

/** Nav groups mirror the workbench entity groups (Combat / Items / World). */
export const adminGroups: AdminGroupDef[] = workbenchGroups.map((group) => ({ key: group.key, label: group.label }));

/** One sidebar item per workbench entity, placed in its group. */
export const adminTools: AdminToolDef[] = workbenchGroups.flatMap((group) =>
	group.entityKeys
		.map((entityKey) => workbenchEntities.find((entity) => entity.key === entityKey))
		.filter((entity) => !!entity)
		.map((entity) => ({
			key: entity.key,
			label: entity.label,
			group: group.key,
			glyph: entity.glyph as AdminGlyphKind
		}))
);
