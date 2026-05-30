import type { Component } from 'svelte';
import type { AdminGlyphKind } from '../AdminGlyph.svelte';
import {
	AddEditEnemies,
	SetAttributeDistributions,
	SetEnemySkills,
	AddEditItems,
	AddEditSkills,
	SetSkillMultipliers,
	AddEditZones,
	SetZoneEnemies
} from './';

export interface AdminGroupDef {
	key: string;
	label: string;
}

export interface AdminToolDef {
	key: string;
	label: string;
	group: string;
	glyph: AdminGlyphKind;
	component: Component;
}

/** Entity types become sidebar groups, mirroring the game's NavSidebar pattern. */
export const adminGroups: AdminGroupDef[] = [
	{ key: 'enemies', label: 'Enemies' },
	{ key: 'items', label: 'Items' },
	{ key: 'skills', label: 'Skills' },
	{ key: 'zones', label: 'Zones' }
];

/** Each tool is a sidebar item under its entity group. */
export const adminTools: AdminToolDef[] = [
	{ key: 'addEnemies', label: 'Add/Edit Enemies', group: 'enemies', glyph: 'skull', component: AddEditEnemies },
	{ key: 'attrDist', label: 'Set Attribute Distributions', group: 'enemies', glyph: 'bars', component: SetAttributeDistributions },
	{ key: 'enemySkills', label: 'Set Enemy Skills', group: 'enemies', glyph: 'rune', component: SetEnemySkills },
	{ key: 'addItems', label: 'Add/Edit Items', group: 'items', glyph: 'box', component: AddEditItems },
	{ key: 'addSkills', label: 'Add/Edit Skills', group: 'skills', glyph: 'bolt', component: AddEditSkills },
	{ key: 'skillMult', label: 'Set Skill Multipliers', group: 'skills', glyph: 'multiply', component: SetSkillMultipliers },
	{ key: 'addZones', label: 'Add/Edit Zones', group: 'zones', glyph: 'map', component: AddEditZones },
	{ key: 'zoneEnemies', label: 'Set Zone Enemies', group: 'zones', glyph: 'pin', component: SetZoneEnemies }
];
